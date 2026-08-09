export type AllowedAction = 'approve' | 'reject' | 'select_project';

export interface TurnItem {
  id: string;
  timestamp: string;
  kind: 'commentary' | 'tool' | 'approval' | 'final';
  phase: 'running' | 'completed' | 'waiting' | 'delivered';
  label: string;
  content: string | null;
}

export interface CurrentTurn {
  id: string;
  started_at: string;
  prompt: string;
  items: TurnItem[];
}

export interface RecentEvent {
  kind: 'command' | 'tool' | 'reply' | 'status' | string;
  label: string;
  content: string | null;
}

export interface CodexUsage {
  weekly_remaining_percent: number | null;
  reset_text: string;
  reset_date: string;
  reset_available: number | null;
}

export interface AntigravityUsage {
  weekly_remaining_percent: number | null;
  weekly_refresh_text: string;
  five_hour_remaining_percent: number | null;
  five_hour_refresh_text: string;
  gemini_five_hour_remaining_percent: number | null;
  gemini_five_hour_refresh_text: string;
  claude_five_hour_remaining_percent: number | null;
  claude_five_hour_refresh_text: string;
}

export interface StatePayload {
  agent: string;
  project: string;
  conversation_name?: string;
  status: 'idle' | 'working' | 'waiting' | 'completed' | 'error' | string;
  message: string;
  elapsed: number;
  requires_action: boolean;
  actions: string[];
  target_id: string | null;
  conversation_tokens: number | null;
  usage_remaining_percent?: number | null;
  steps?: string[] | null;
  current_step?: number | null;
  models?: string[] | null;
  codex_usage?: CodexUsage | null;
  antigravity_usage?: AntigravityUsage | null;
  recent_events?: RecentEvent[] | null;
  current_turn?: CurrentTurn | null;
}

export interface StateEnvelope {
  version: string;
  type: string;
  id: string;
  timestamp: string | null;
  payload: StatePayload;
}

export interface DashboardData {
  current: StateEnvelope | null;
  projects: StateEnvelope[];
}

export interface HostDashboardMessage {
  type: 'dashboard';
  dashboard: DashboardData;
  connected: boolean;
}

export interface H5ActionPayload {
  type: 'action';
  action: AllowedAction;
  target_id: string | null;
}

export interface HostInitMessage {
  type: 'agentdeck:init';
}
