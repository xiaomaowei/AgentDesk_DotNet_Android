import { describe, test, expect, vi } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { parseDashboardMessage, useHostBridge } from '../hooks/useHostBridge';
import { sampleDashboardWithTurn, sampleDashboardLegacy } from '../fixtures/sampleDashboard';

describe('Host Message Contract & Runtime Validation', () => {
  test('valid JSON dashboard message is parsed correctly', () => {
    const rawMessage = JSON.stringify({
      type: 'dashboard',
      dashboard: sampleDashboardWithTurn,
      connected: true,
    });

    const parsed = parseDashboardMessage(rawMessage);
    expect(parsed).not.toBeNull();
    expect(parsed?.connected).toBe(true);
    expect(parsed?.dashboard.current?.payload.project).toBe('AgentDeck');
    expect(parsed?.dashboard.projects).toHaveLength(2);
  });

  test('valid legacy dashboard without current_turn is parsed correctly', () => {
    const rawMessage = JSON.stringify({
      type: 'dashboard',
      dashboard: sampleDashboardLegacy,
      connected: true,
    });

    const parsed = parseDashboardMessage(rawMessage);
    expect(parsed).not.toBeNull();
    expect(parsed?.dashboard.current?.payload.current_turn).toBeNull();
    expect(parsed?.dashboard.current?.payload.recent_events).toHaveLength(2);
  });

  test('malformed dashboard JSON or primitive input fails closed', () => {
    const consoleSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});

    expect(parseDashboardMessage('invalid json {')).toBeNull();
    expect(parseDashboardMessage(123)).toBeNull();
    expect(parseDashboardMessage(null)).toBeNull();
    expect(parseDashboardMessage(JSON.stringify({ type: 'wrong_type' }))).toBeNull();
    expect(parseDashboardMessage(JSON.stringify({ type: 'dashboard' }))).toBeNull();

    consoleSpy.mockRestore();
  });

  test('malformed current envelope, payload, current_turn or items fail closed', () => {
    const consoleSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});

    // Malformed envelope missing version/id
    expect(
      parseDashboardMessage({
        type: 'dashboard',
        dashboard: {
          current: { payload: sampleDashboardWithTurn.current?.payload },
          projects: [],
        },
      })
    ).toBeNull();

    // Malformed payload missing required agent string
    expect(
      parseDashboardMessage({
        type: 'dashboard',
        dashboard: {
          current: {
            version: '1.0',
            type: 'state',
            id: 'e1',
            payload: { project: 'AgentDeck', status: 'idle', message: 'msg', elapsed: 0, requires_action: false, actions: [] },
          },
          projects: [],
        },
      })
    ).toBeNull();

    // Malformed current_turn item phase
    const malformedTurn = {
      ...sampleDashboardWithTurn,
      current: {
        ...sampleDashboardWithTurn.current!,
        payload: {
          ...sampleDashboardWithTurn.current!.payload,
          current_turn: {
            id: 't1',
            started_at: '10:00:00',
            prompt: 'Prompt',
            items: [
              {
                id: 'i1',
                timestamp: '10:00:01',
                kind: 'tool',
                phase: 'invalid_phase', // Invalid phase
                label: 'Tool',
                content: null,
              },
            ],
          },
        },
      },
    };

    expect(
      parseDashboardMessage({
        type: 'dashboard',
        dashboard: malformedTurn,
      })
    ).toBeNull();

    consoleSpy.mockRestore();
  });

  test('strict origin validation rejects differing origin and empty origin', () => {
    const { result } = renderHook(() => useHostBridge());

    // Window origin in test jsdom is window.location.origin
    const validOrigin = window.location.origin;

    // 1. Differing origin (e.g. localhost:8080 vs localhost:3000)
    act(() => {
      window.dispatchEvent(
        new MessageEvent('message', {
          data: JSON.stringify({
            type: 'dashboard',
            dashboard: sampleDashboardWithTurn,
            connected: true,
          }),
          origin: 'http://localhost:8080',
        })
      );
    });
    expect(result.current.connected).toBe(false);
    expect(result.current.dashboard.current).toBeNull();

    // 2. Empty origin
    act(() => {
      window.dispatchEvent(
        new MessageEvent('message', {
          data: JSON.stringify({
            type: 'dashboard',
            dashboard: sampleDashboardWithTurn,
            connected: true,
          }),
          origin: '',
        })
      );
    });
    expect(result.current.connected).toBe(false);
    expect(result.current.dashboard.current).toBeNull();

    // 3. Exact matching origin succeeds
    act(() => {
      window.dispatchEvent(
        new MessageEvent('message', {
          data: JSON.stringify({
            type: 'dashboard',
            dashboard: sampleDashboardWithTurn,
            connected: true,
          }),
          origin: validOrigin,
        })
      );
    });
    expect(result.current.connected).toBe(true);
    expect(result.current.dashboard.current?.payload.project).toBe('AgentDeck');
  });
});
