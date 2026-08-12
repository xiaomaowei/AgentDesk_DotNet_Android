import { useEffect, useState, useRef, useCallback } from 'react';
import {
  DashboardData,
  HostDashboardMessage,
  H5ActionPayload,
  AllowedAction,
  StateEnvelope,
  StatePayload,
  CurrentTurn,
  TurnItem,
  RecentEvent,
  CodexUsage,
  AntigravityUsage,
} from '../types/dashboard';

declare global {
  interface Window {
    AgentDeckBootstrap?: {
      postMessage: (message: string) => void;
    };
  }
}

export interface UseHostBridgeReturn {
  dashboard: DashboardData;
  connected: boolean;
  sendAction: (action: AllowedAction, targetId: string | null) => void;
  pendingAction: AllowedAction | null;
}

export const APP_ASSETS_ORIGIN = 'https://appassets.androidplatform.net';

export function isAllowedInitEvent(
  eventOrigin: string,
  pageOrigin: string,
  hasPort: boolean
): boolean {
  if (!hasPort) return false;
  if (pageOrigin === APP_ASSETS_ORIGIN) {
    // In production on appassets, Android's postWebMessage targetOrigin restricts the receiver.
    // Native init event.origin may be empty or implementation-specific.
    return true;
  }
  // Browser dev/tests on a non-appassets origin: require non-empty event.origin matching pageOrigin
  return Boolean(eventOrigin) && eventOrigin === pageOrigin;
}

export function isAllowedDirectWindowMessage(
  eventOrigin: string,
  pageOrigin: string
): boolean {
  return Boolean(eventOrigin) && Boolean(pageOrigin) && eventOrigin === pageOrigin;
}

function isObject(val: unknown): val is Record<string, unknown> {
  return typeof val === 'object' && val !== null && !Array.isArray(val);
}

function isString(val: unknown): val is string {
  return typeof val === 'string';
}

function isNumber(val: unknown): val is number {
  return typeof val === 'number' && !isNaN(val);
}

function isBoolean(val: unknown): val is boolean {
  return typeof val === 'boolean';
}

function validateTurnItem(val: unknown): TurnItem | null {
  if (!isObject(val)) return null;
  if (
    !isString(val.id) ||
    !isString(val.timestamp) ||
    !isString(val.kind) ||
    !isString(val.phase) ||
    !isString(val.label)
  ) {
    return null;
  }
  const validKinds = ['commentary', 'tool', 'approval', 'final'];
  const validPhases = ['running', 'completed', 'waiting', 'delivered'];
  if (!validKinds.includes(val.kind) || !validPhases.includes(val.phase)) {
    return null;
  }
  const content = isString(val.content) ? val.content : null;
  return {
    id: val.id,
    timestamp: val.timestamp,
    kind: val.kind as TurnItem['kind'],
    phase: val.phase as TurnItem['phase'],
    label: val.label,
    content,
  };
}

function validateCurrentTurn(val: unknown): CurrentTurn | null {
  if (val === null || val === undefined) return null;
  if (!isObject(val)) return null;
  if (!isString(val.id) || !isString(val.started_at) || !isString(val.prompt) || !Array.isArray(val.items)) {
    return null;
  }
  const items: TurnItem[] = [];
  for (const itemVal of val.items) {
    const item = validateTurnItem(itemVal);
    if (!item) return null;
    items.push(item);
  }
  return {
    id: val.id,
    started_at: val.started_at,
    prompt: val.prompt,
    items,
  };
}

function validateRecentEvent(val: unknown): RecentEvent | null {
  if (!isObject(val)) return null;
  if (!isString(val.kind) || !isString(val.label)) return null;
  const content = isString(val.content) ? val.content : null;
  return {
    kind: val.kind,
    label: val.label,
    content,
  };
}

function validateCodexUsage(val: unknown): CodexUsage | null {
  if (val === null || val === undefined) return null;
  if (!isObject(val)) return null;
  return {
    weekly_remaining_percent: isNumber(val.weekly_remaining_percent) ? val.weekly_remaining_percent : null,
    reset_text: isString(val.reset_text) ? val.reset_text : '',
    reset_date: isString(val.reset_date) ? val.reset_date : '',
    reset_available: isNumber(val.reset_available) ? val.reset_available : null,
  };
}

function validateAntigravityUsage(val: unknown): AntigravityUsage | null {
  if (val === null || val === undefined) return null;
  if (!isObject(val)) return null;
  return {
    weekly_remaining_percent: isNumber(val.weekly_remaining_percent) ? val.weekly_remaining_percent : null,
    weekly_refresh_text: isString(val.weekly_refresh_text) ? val.weekly_refresh_text : '',
    five_hour_remaining_percent: isNumber(val.five_hour_remaining_percent) ? val.five_hour_remaining_percent : null,
    five_hour_refresh_text: isString(val.five_hour_refresh_text) ? val.five_hour_refresh_text : '',
    gemini_five_hour_remaining_percent: isNumber(val.gemini_five_hour_remaining_percent) ? val.gemini_five_hour_remaining_percent : null,
    gemini_five_hour_refresh_text: isString(val.gemini_five_hour_refresh_text) ? val.gemini_five_hour_refresh_text : '',
    claude_five_hour_remaining_percent: isNumber(val.claude_five_hour_remaining_percent) ? val.claude_five_hour_remaining_percent : null,
    claude_five_hour_refresh_text: isString(val.claude_five_hour_refresh_text) ? val.claude_five_hour_refresh_text : '',
  };
}

function validateStatePayload(val: unknown): StatePayload | null {
  if (!isObject(val)) return null;
  if (
    !isString(val.agent) ||
    !isString(val.project) ||
    !isString(val.status) ||
    !isString(val.message) ||
    !isNumber(val.elapsed) ||
    !isBoolean(val.requires_action) ||
    !Array.isArray(val.actions)
  ) {
    return null;
  }
  const actions: string[] = [];
  for (const act of val.actions) {
    if (isString(act)) actions.push(act);
    else return null;
  }

  const target_id = isString(val.target_id) ? val.target_id : null;
  const conversation_tokens = isNumber(val.conversation_tokens) ? val.conversation_tokens : null;
  const conversation_name = isString(val.conversation_name) ? val.conversation_name : undefined;
  const usage_remaining_percent = isNumber(val.usage_remaining_percent) ? val.usage_remaining_percent : null;

  let steps: string[] | null = null;
  if (Array.isArray(val.steps)) {
    steps = val.steps.filter(isString);
  }
  const current_step = isNumber(val.current_step) ? val.current_step : null;

  let models: string[] | null = null;
  if (Array.isArray(val.models)) {
    models = val.models.filter(isString);
  }

  let recent_events: RecentEvent[] | null = null;
  if (Array.isArray(val.recent_events)) {
    recent_events = [];
    for (const evVal of val.recent_events) {
      const ev = validateRecentEvent(evVal);
      if (!ev) return null;
      recent_events.push(ev);
    }
  }

  const codex_usage = validateCodexUsage(val.codex_usage);
  const antigravity_usage = validateAntigravityUsage(val.antigravity_usage);

  let current_turn: CurrentTurn | null = null;
  if (val.current_turn !== undefined && val.current_turn !== null) {
    current_turn = validateCurrentTurn(val.current_turn);
    if (!current_turn) return null;
  }

  return {
    agent: val.agent,
    project: val.project,
    conversation_name,
    status: val.status,
    message: val.message,
    elapsed: val.elapsed,
    requires_action: val.requires_action,
    actions,
    target_id,
    conversation_tokens,
    usage_remaining_percent,
    steps,
    current_step,
    models,
    codex_usage,
    antigravity_usage,
    recent_events,
    current_turn,
  };
}

function validateStateEnvelope(val: unknown): StateEnvelope | null {
  if (!isObject(val)) return null;
  if (!isString(val.version) || !isString(val.type) || !isString(val.id)) {
    return null;
  }
  const timestamp = isString(val.timestamp) ? val.timestamp : null;
  const payload = validateStatePayload(val.payload);
  if (!payload) return null;

  return {
    version: val.version,
    type: val.type,
    id: val.id,
    timestamp,
    payload,
  };
}

export function isWindowsLoopbackDashboard(
  locationOrigin: string = typeof window !== 'undefined' ? window.location.origin : '',
  pathname: string = typeof window !== 'undefined' ? window.location.pathname : ''
): boolean {
  if (!locationOrigin) return false;
  try {
    const url = new URL(locationOrigin);
    if (url.protocol !== 'http:') return false;
    if (url.port !== '8765') return false;
    const host = url.hostname;
    const isLoopback = host === '127.0.0.1' || host === 'localhost' || host === '[::1]' || host === '::1';
    const isAssetsPath = pathname.startsWith('/assets/') || pathname === '/assets';
    return isLoopback && isAssetsPath;
  } catch {
    return false;
  }
}

export function parseDashboardMessage(raw: unknown): HostDashboardMessage | null {
  try {
    let parsed: unknown = raw;
    if (typeof raw === 'string') {
      parsed = JSON.parse(raw);
    }
    if (!isObject(parsed)) return null;

    if (parsed.current !== undefined || parsed.projects !== undefined) {
      const currentEnvelope =
        parsed.current !== null && parsed.current !== undefined
          ? validateStateEnvelope(parsed.current)
          : null;

      if (parsed.current !== null && parsed.current !== undefined && !currentEnvelope) {
        return null;
      }

      if (!Array.isArray(parsed.projects)) return null;
      const projects: StateEnvelope[] = [];
      for (const projVal of parsed.projects) {
        const envelope = validateStateEnvelope(projVal);
        if (!envelope) return null;
        projects.push(envelope);
      }

      return {
        type: 'dashboard',
        dashboard: { current: currentEnvelope, projects },
        connected: true,
      };
    }

    if (parsed.type !== 'dashboard') return null;
    if (!isObject(parsed.dashboard)) return null;

    const currentEnvelope =
      parsed.dashboard.current !== null && parsed.dashboard.current !== undefined
        ? validateStateEnvelope(parsed.dashboard.current)
        : null;

    if (parsed.dashboard.current !== null && parsed.dashboard.current !== undefined && !currentEnvelope) {
      return null;
    }

    if (!Array.isArray(parsed.dashboard.projects)) return null;
    const projects: StateEnvelope[] = [];
    for (const projVal of parsed.dashboard.projects) {
      const envelope = validateStateEnvelope(projVal);
      if (!envelope) return null;
      projects.push(envelope);
    }

    const connected = typeof parsed.connected === 'boolean' ? parsed.connected : false;

    return {
      type: 'dashboard',
      dashboard: { current: currentEnvelope, projects },
      connected,
    };
  } catch (err) {
    console.warn('Failed to parse dashboard message:', err);
    return null;
  }
}

export function useHostBridge(
  initialData?: DashboardData,
  initialConnected: boolean = false
): UseHostBridgeReturn {
  const [dashboard, setDashboard] = useState<DashboardData>(initialData || { current: null, projects: [] });
  const [connected, setConnected] = useState<boolean>(initialConnected);
  const [pendingAction, setPendingAction] = useState<AllowedAction | null>(null);

  const portRef = useRef<MessagePort | null>(null);

  const handleMessageData = useCallback((data: unknown) => {
    const parsed = parseDashboardMessage(data);
    if (parsed) {
      setDashboard(parsed.dashboard);
      setConnected(parsed.connected);
      setPendingAction(null);
    }
  }, []);

  useEffect(() => {
    if (isWindowsLoopbackDashboard()) {
      let isCancelled = false;

      fetch('/api/v1/dashboard')
        .then((res) => {
          if (!res.ok) throw new Error(`HTTP error ${res.status}`);
          return res.json();
        })
        .then((data) => {
          if (!isCancelled) {
            handleMessageData(data);
          }
        })
        .catch((err) => {
          console.warn('Failed to fetch initial dashboard state:', err);
          if (!isCancelled) {
            setConnected(false);
          }
        });

      let eventSource: EventSource | null = null;
      try {
        eventSource = new EventSource('/api/v1/events');
        eventSource.onmessage = (event: MessageEvent) => {
          if (isCancelled) return;
          const dataStr = event.data ? String(event.data).trim() : '';
          if (dataStr && !dataStr.startsWith(':')) {
            handleMessageData(dataStr);
          }
        };
        eventSource.onerror = () => {
          if (!isCancelled) {
            setConnected(false);
          }
        };
      } catch (err) {
        console.warn('Failed to initialize EventSource:', err);
      }

      return () => {
        isCancelled = true;
        if (eventSource) {
          eventSource.close();
        }
      };
    }

    const handleWindowMessage = (event: MessageEvent) => {
      const pageOrigin = window.location.origin || '';

      let parsedData: unknown = event.data;
      if (typeof parsedData === 'string') {
        try {
          parsedData = JSON.parse(parsedData);
        } catch {
          // ignore invalid json string on window message
        }
      }

      // Check for port init message
      if (isObject(parsedData) && parsedData.type === 'agentdeck:init') {
        const hasPort = Boolean(event.ports && event.ports[0]);
        if (isAllowedInitEvent(event.origin, pageOrigin, hasPort)) {
          const port = event.ports[0];
          portRef.current = port;
          // Assign port.onmessage BEFORE calling port.start() to prevent racing
          port.onmessage = (portEvent: MessageEvent) => {
            handleMessageData(portEvent.data);
          };
          port.start();
        }
        return;
      }

      // Dev / Test fallback: direct same-origin window message dashboard event
      if (isObject(parsedData) && parsedData.type === 'dashboard') {
        if (isAllowedDirectWindowMessage(event.origin, pageOrigin)) {
          handleMessageData(event.data);
        }
      }
    };

    window.addEventListener('message', handleWindowMessage);

    // Bootstrap handshake: notify native host that H5 window listener is ready
    if (typeof window !== 'undefined' && window.AgentDeckBootstrap?.postMessage) {
      try {
        window.AgentDeckBootstrap.postMessage(JSON.stringify({ type: 'ready' }));
      } catch (err) {
        console.warn('Failed to post ready message to AgentDeckBootstrap:', err);
      }
    }

    return () => {
      window.removeEventListener('message', handleWindowMessage);
      if (portRef.current) {
        try {
          portRef.current.close();
        } catch {
          // safe close fallback
        }
        portRef.current = null;
      }
    };
  }, [handleMessageData]);

  const sendAction = useCallback(
    (action: AllowedAction, targetId: string | null) => {
      setPendingAction(action);
      const payload: H5ActionPayload = {
        type: 'action',
        action,
        target_id: targetId,
      };
      const payloadStr = JSON.stringify(payload);

      if (isWindowsLoopbackDashboard()) {
        const envelope = {
          version: '1.0',
          type: 'action',
          id: `act_${Math.random().toString(36).substring(2, 10)}`,
          timestamp: new Date().toISOString(),
          payload: {
            action,
            target_id: targetId,
          },
        };
        fetch('/api/v1/actions', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify(envelope),
        })
          .then((res) => {
            if (!res.ok) {
              throw new Error(`HTTP error ${res.status}`);
            }
          })
          .catch((err) => {
            console.warn('Failed to post action to /api/v1/actions:', err);
            setConnected(false);
          })
          .finally(() => {
            setPendingAction(null);
          });
      } else if (portRef.current) {
        portRef.current.postMessage(payloadStr);
      } else {
        // Dev/Test fallback postMessage
        window.postMessage(payloadStr, window.location.origin || '*');
      }
    },
    []
  );

  return {
    dashboard,
    connected,
    sendAction,
    pendingAction,
  };
}
