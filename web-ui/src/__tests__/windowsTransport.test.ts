import { describe, test, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { isWindowsLoopbackDashboard, parseDashboardMessage, useHostBridge } from '../hooks/useHostBridge';
import { sampleDashboardWithTurn } from '../fixtures/sampleDashboard';

describe('Windows Direct-Host Transport', () => {
  test('isWindowsLoopbackDashboard accurately detects Windows loopback asset URLs', () => {
    expect(isWindowsLoopbackDashboard('http://127.0.0.1:8765', '/assets/index.html')).toBe(true);
    expect(isWindowsLoopbackDashboard('http://localhost:8765', '/assets/')).toBe(true);
    expect(isWindowsLoopbackDashboard('http://127.0.0.1:8765', '/assets')).toBe(true);
    expect(isWindowsLoopbackDashboard('http://[::1]:8765', '/assets/index.html')).toBe(true);

    // Negative tests: wrong port, HTTPS, wrong host, wrong path
    expect(isWindowsLoopbackDashboard('http://127.0.0.1:8080', '/assets/index.html')).toBe(false);
    expect(isWindowsLoopbackDashboard('http://localhost:3000', '/assets/index.html')).toBe(false);
    expect(isWindowsLoopbackDashboard('https://127.0.0.1:8765', '/assets/index.html')).toBe(false);
    expect(isWindowsLoopbackDashboard('https://localhost:8765', '/assets/index.html')).toBe(false);
    expect(isWindowsLoopbackDashboard('https://appassets.androidplatform.net', '/assets/index.html')).toBe(false);
    expect(isWindowsLoopbackDashboard('http://example.com:8765', '/assets/index.html')).toBe(false);
    expect(isWindowsLoopbackDashboard('http://127.0.0.1:8765', '/api/v1/dashboard')).toBe(false);
  });

  test('parseDashboardMessage parses direct DashboardSnapshot object cleanly', () => {
    const parsed = parseDashboardMessage(sampleDashboardWithTurn);
    expect(parsed).not.toBeNull();
    expect(parsed?.connected).toBe(true);
    expect(parsed?.dashboard.current?.payload.project).toBe('AgentDeck');
  });

  describe('useHostBridge on Windows loopback', () => {
    const originalLocation = window.location;
    let mockEventSourceClose: ReturnType<typeof vi.fn>;

    beforeEach(() => {
      mockEventSourceClose = vi.fn();

      // Mock window.location for Windows loopback dashboard
      Object.defineProperty(window, 'location', {
        value: {
          origin: 'http://127.0.0.1:8765',
          hostname: '127.0.0.1',
          pathname: '/assets/index.html',
          protocol: 'http:',
        },
        writable: true,
      });

      // Mock EventSource
      class MockEventSource {
        url: string;
        onmessage: ((event: MessageEvent) => void) | null = null;
        onerror: (() => void) | null = null;
        constructor(url: string) {
          this.url = url;
        }
        close() {
          (mockEventSourceClose as () => void)();
        }
      }
      vi.stubGlobal('EventSource', MockEventSource);
    });

    afterEach(() => {
      Object.defineProperty(window, 'location', {
        value: originalLocation,
        writable: true,
      });
      vi.restoreAllMocks();
      vi.unstubAllGlobals();
    });

    test('initial fetch and SSE subscribe on Windows loopback dashboard', async () => {
      const fetchSpy = vi.spyOn(window, 'fetch').mockImplementation((url) => {
        if (url === '/api/v1/dashboard') {
          return Promise.resolve(
            new Response(JSON.stringify(sampleDashboardWithTurn), {
              status: 200,
              headers: { 'Content-Type': 'application/json' },
            })
          );
        }
        return Promise.reject(new Error('Unknown endpoint'));
      });

      const { result } = renderHook(() => useHostBridge());

      await act(async () => {
        await Promise.resolve();
      });

      expect(fetchSpy).toHaveBeenCalledWith('/api/v1/dashboard');
      expect(result.current.connected).toBe(true);
      expect(result.current.dashboard.current?.payload.project).toBe('AgentDeck');
    });

    test('sendAction posts valid envelope to /api/v1/actions on Windows loopback dashboard', async () => {
      let postedBody: any = null;
      vi.spyOn(window, 'fetch').mockImplementation((url, init) => {
        if (url === '/api/v1/dashboard') {
          return Promise.resolve(
            new Response(JSON.stringify(sampleDashboardWithTurn), {
              status: 200,
            })
          );
        }
        if (url === '/api/v1/actions' && init?.method === 'POST') {
          postedBody = JSON.parse(init.body as string);
          return Promise.resolve(
            new Response(
              JSON.stringify({
                type: 'action_result',
                id: 'msg_01',
                timestamp: '2026-08-12T00:00:00Z',
                payload: { accepted: true, action: 'approve' },
              }),
              { status: 200 }
            )
          );
        }
        return Promise.reject(new Error('Unknown endpoint'));
      });

      const { result } = renderHook(() => useHostBridge());

      await act(async () => {
        result.current.sendAction('approve', 'target_999');
      });

      expect(postedBody).not.toBeNull();
      expect(postedBody.version).toBe('1.0');
      expect(postedBody.type).toBe('action');
      expect(postedBody.payload.action).toBe('approve');
      expect(postedBody.payload.target_id).toBe('target_999');
    });

    test('sendAction handles HTTP error response by setting connected to false and clearing pending action', async () => {
      const consoleWarnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
      vi.spyOn(window, 'fetch').mockImplementation((url, init) => {
        if (url === '/api/v1/dashboard') {
          return Promise.resolve(
            new Response(JSON.stringify(sampleDashboardWithTurn), {
              status: 200,
            })
          );
        }
        if (url === '/api/v1/actions' && init?.method === 'POST') {
          return Promise.resolve(
            new Response('Internal Server Error', { status: 500 })
          );
        }
        return Promise.reject(new Error('Unknown endpoint'));
      });

      const { result } = renderHook(() => useHostBridge());

      await act(async () => {
        await Promise.resolve();
      });
      expect(result.current.connected).toBe(true);

      await act(async () => {
        result.current.sendAction('approve', 'target_999');
      });

      expect(consoleWarnSpy).toHaveBeenCalledWith(
        'Failed to post action to /api/v1/actions:',
        expect.any(Error)
      );
      expect(result.current.connected).toBe(false);
      expect(result.current.pendingAction).toBeNull();
    });
  });
});
