import { describe, test, expect, vi } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import {
  isAllowedInitEvent,
  isAllowedDirectWindowMessage,
  useHostBridge,
  APP_ASSETS_ORIGIN,
} from '../hooks/useHostBridge';

describe('Trust Policy & MessagePort Lifecycle', () => {
  describe('isAllowedInitEvent', () => {
    test('appassets page + empty native init origin + transferred port => allowed', () => {
      expect(isAllowedInitEvent('', APP_ASSETS_ORIGIN, true)).toBe(true);
      expect(isAllowedInitEvent('https://appassets.androidplatform.net', APP_ASSETS_ORIGIN, true)).toBe(true);
    });

    test('non-appassets page + same-origin init + port => allowed', () => {
      expect(isAllowedInitEvent('http://localhost:3000', 'http://localhost:3000', true)).toBe(true);
    });

    test('non-appassets page + empty origin => rejected', () => {
      expect(isAllowedInitEvent('', 'http://localhost:3000', true)).toBe(false);
    });

    test('non-appassets page + cross-origin init => rejected', () => {
      expect(isAllowedInitEvent('http://evil.com', 'http://localhost:3000', true)).toBe(false);
    });

    test('init without port => rejected regardless of origin', () => {
      expect(isAllowedInitEvent('', APP_ASSETS_ORIGIN, false)).toBe(false);
      expect(isAllowedInitEvent('http://localhost:3000', 'http://localhost:3000', false)).toBe(false);
    });
  });

  describe('isAllowedDirectWindowMessage', () => {
    test('same non-empty origin => allowed', () => {
      expect(isAllowedDirectWindowMessage('http://localhost:3000', 'http://localhost:3000')).toBe(true);
      expect(isAllowedDirectWindowMessage(APP_ASSETS_ORIGIN, APP_ASSETS_ORIGIN)).toBe(true);
    });

    test('empty or cross-origin => rejected', () => {
      expect(isAllowedDirectWindowMessage('', 'http://localhost:3000')).toBe(false);
      expect(isAllowedDirectWindowMessage('http://localhost:8080', 'http://localhost:3000')).toBe(false);
      expect(isAllowedDirectWindowMessage('', '')).toBe(false);
    });
  });

  describe('AgentDeckBootstrap Ready Handshake', () => {
    test('calls AgentDeckBootstrap.postMessage with {"type":"ready"} after setting up listener', () => {
      const order: string[] = [];
      const originalAddEventListener = window.addEventListener;

      const addEventListenerSpy = vi.spyOn(window, 'addEventListener').mockImplementation((type, listener, options) => {
        order.push(`addEventListener:${type}`);
        return originalAddEventListener.call(window, type, listener, options);
      });

      const postMessageSpy = vi.fn((msg: string) => {
        order.push(`bootstrap:${msg}`);
      });

      window.AgentDeckBootstrap = { postMessage: postMessageSpy };

      try {
        renderHook(() => useHostBridge());

        expect(postMessageSpy).toHaveBeenCalledTimes(1);
        expect(postMessageSpy).toHaveBeenCalledWith(JSON.stringify({ type: 'ready' }));
        expect(order).toEqual(['addEventListener:message', `bootstrap:${JSON.stringify({ type: 'ready' })}`]);
      } finally {
        delete window.AgentDeckBootstrap;
        addEventListenerSpy.mockRestore();
      }
    });

    test('does not throw when AgentDeckBootstrap object is absent (browser fallback)', () => {
      delete window.AgentDeckBootstrap;

      expect(() => {
        renderHook(() => useHostBridge());
      }).not.toThrow();
    });
  });

  describe('MessagePort callback assignment order', () => {
    test('assigns port.onmessage before calling port.start()', () => {
      renderHook(() => useHostBridge());

      const order: string[] = [];

      let _onmessage: ((ev: MessageEvent) => void) | null = null;

      const mockPort = {
        set onmessage(fn: ((ev: MessageEvent) => void) | null) {
          order.push('set_onmessage');
          _onmessage = fn;
        },
        get onmessage(): ((ev: MessageEvent) => void) | null {
          return _onmessage;
        },
        start: vi.fn(() => {
          order.push('start');
        }),
        postMessage: vi.fn(),
        close: vi.fn(),
      } as unknown as MessagePort;

      act(() => {
        const initEvent = new MessageEvent('message', {
          data: JSON.stringify({ type: 'agentdeck:init' }),
          origin: window.location.origin,
          ports: [mockPort],
        });
        window.dispatchEvent(initEvent);
      });

      expect(order).toEqual(['set_onmessage', 'start']);
      expect(mockPort.start).toHaveBeenCalledTimes(1);
    });
  });
});
