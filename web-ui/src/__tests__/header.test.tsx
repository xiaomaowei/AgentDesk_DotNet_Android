import { describe, test, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Header } from '../components/Header';
import { ProjectSelector } from '../components/ProjectSelector';
import { ActiveStatusBar } from '../components/ActiveStatusBar';
import { formatDecimalPercent, normalizeRefreshText, usageColor } from '../utils/format';
import { StatePayload } from '../types/dashboard';
import { sampleDashboardWithTurn } from '../fixtures/sampleDashboard';

describe('Header & Format Logic Tests', () => {
  describe('formatDecimalPercent', () => {
    test('preserves up to two decimals and trims unnecessary trailing zeros', () => {
      expect(formatDecimalPercent(63.56)).toBe('63.56');
      expect(formatDecimalPercent(78.19)).toBe('78.19');
      expect(formatDecimalPercent(63.50)).toBe('63.5');
      expect(formatDecimalPercent(60.00)).toBe('60');
      expect(formatDecimalPercent(63.5678)).toBe('63.57');
    });

    test('never rounds away valid decimals', () => {
      expect(formatDecimalPercent(63.56)).not.toBe('64');
      expect(formatDecimalPercent(78.19)).not.toBe('78');
    });
  });

  describe('normalizeRefreshText', () => {
    test('normalizes known refresh prefixes to compact Re: without altering remaining value', () => {
      expect(normalizeRefreshText('Refreshes in 4h 30m')).toBe('Re:4h 30m');
      expect(normalizeRefreshText('Refreshes in 1h32m')).toBe('Re:1h32m');
      expect(normalizeRefreshText('Refreshes 2h')).toBe('Re:2h');
      expect(normalizeRefreshText('Re: 3h')).toBe('Re:3h');
      expect(normalizeRefreshText('Re:4h30m')).toBe('Re:4h30m');
    });

    test('converts Quota available to short understandable value', () => {
      expect(normalizeRefreshText('Quota available')).toBe('Quota avail');
      expect(normalizeRefreshText('QUOTA AVAILABLE')).toBe('Quota avail');
    });

    test('handles empty or missing inputs gracefully', () => {
      expect(normalizeRefreshText('')).toBe('');
      expect(normalizeRefreshText(null)).toBe('');
      expect(normalizeRefreshText(undefined)).toBe('');
    });
  });

  describe('usageColor', () => {
    test('0% maps to red (hue 0)', () => {
      expect(usageColor(0)).toBe('hsl(0.0, 85%, 55%)');
    });

    test('50% maps to yellow (hue 60)', () => {
      expect(usageColor(50)).toBe('hsl(60.0, 85%, 55%)');
    });

    test('100% maps to green (hue 120)', () => {
      expect(usageColor(100)).toBe('hsl(120.0, 85%, 55%)');
    });

    test('clamps values below 0 to red', () => {
      expect(usageColor(-10)).toBe('hsl(0.0, 85%, 55%)');
    });

    test('clamps values above 100 to green', () => {
      expect(usageColor(150)).toBe('hsl(120.0, 85%, 55%)');
    });

    test('intermediate value produces intermediate hue', () => {
      // 25% → hue = 25 * 1.2 = 30
      expect(usageColor(25)).toBe('hsl(30.0, 85%, 55%)');
    });
  });

  describe('Header Rendering Contracts', () => {
    test('renders brand logo, removes brand text "AgentDeck", and maintains connection presence', () => {
      render(<Header connected={true} activePayload={null} />);
      expect(screen.queryByText('AgentDeck')).not.toBeInTheDocument();
      expect(screen.getByTestId('robot-mark-svg')).toBeInTheDocument();
      expect(screen.getByText('本機在線')).toBeInTheDocument();

      render(<Header connected={false} activePayload={null} />);
      expect(screen.getByText('等待 Bridge')).toBeInTheDocument();
    });

    test('renders Gemini and Claude values simultaneously with decimal preservation', () => {
      const payload: StatePayload = {
        agent: 'antigravity',
        project: 'Test',
        status: 'idle',
        message: 'ready',
        elapsed: 0,
        requires_action: false,
        actions: [],
        target_id: null,
        conversation_tokens: 100,
        antigravity_usage: {
          weekly_remaining_percent: 90,
          weekly_refresh_text: 'Refreshes in 5d',
          five_hour_remaining_percent: null,
          five_hour_refresh_text: '',
          gemini_five_hour_remaining_percent: 63.56,
          gemini_five_hour_refresh_text: 'Refreshes in 4h 30m',
          claude_five_hour_remaining_percent: 78.19,
          claude_five_hour_refresh_text: 'Refreshes in 1h 32m',
        },
      };

      const { container } = render(<Header connected={true} activePayload={payload} />);

      // Gemini decimal preservation and compaction (full name, not abbreviation)
      expect(screen.getByText(/Gemini 63\.56%\/5H · Re:4h 30m/)).toBeInTheDocument();
      expect(screen.queryByText(/Ge \d/)).not.toBeInTheDocument();

      // Claude decimal preservation and compaction (full name, not abbreviation)
      expect(screen.getByText(/Claude 78\.19%\/5H · Re:1h 32m/)).toBeInTheDocument();
      expect(screen.queryByText(/Cl \d/)).not.toBeInTheDocument();

      // Both rendered in DOM
      expect(container.querySelector('.antigravity-compact')).toBeInTheDocument();
    });

    test('renders Claude disabled state as exact visible text "Claude disabled/5H" without fake percent or color style', () => {
      const payload: StatePayload = {
        agent: 'antigravity',
        project: 'Test',
        status: 'idle',
        message: 'ready',
        elapsed: 0,
        requires_action: false,
        actions: [],
        target_id: null,
        conversation_tokens: 100,
        antigravity_usage: {
          weekly_remaining_percent: 50,
          weekly_refresh_text: '',
          five_hour_remaining_percent: 95,
          five_hour_refresh_text: 'Refreshes in 10m',
          gemini_five_hour_remaining_percent: 95,
          gemini_five_hour_refresh_text: 'Refreshes in 10m',
          claude_five_hour_remaining_percent: null,
          claude_five_hour_refresh_text: 'disabled',
        },
      };

      const { container } = render(<Header connected={true} activePayload={payload} />);

      expect(screen.getByText('Claude disabled/5H')).toBeInTheDocument();
      expect(screen.queryByText(/Claude 0%/)).not.toBeInTheDocument();
      expect(container.querySelector('.antigravity-compact')).toHaveTextContent('Gemini 95%/5H · Re:10m·Claude disabled/5H');

      const claudeSpan = screen.getByText('Claude disabled/5H').closest('span');
      expect(claudeSpan?.style.color).toBeFalsy();
    });

    test('does not render Claude when percent and disabled state are both absent', () => {
      const payload: StatePayload = {
        agent: 'antigravity',
        project: 'Test',
        status: 'idle',
        message: 'ready',
        elapsed: 0,
        requires_action: false,
        actions: [],
        target_id: null,
        conversation_tokens: 100,
        antigravity_usage: {
          weekly_remaining_percent: 50,
          weekly_refresh_text: '',
          five_hour_remaining_percent: 95,
          five_hour_refresh_text: 'Refreshes in 10m',
          gemini_five_hour_remaining_percent: 95,
          gemini_five_hour_refresh_text: 'Refreshes in 10m',
          claude_five_hour_remaining_percent: null,
          claude_five_hour_refresh_text: '',
        },
      };

      render(<Header connected={true} activePayload={payload} />);

      expect(screen.queryByText(/Claude/)).not.toBeInTheDocument();
    });

    test('renders Gemini disabled state as exact visible text "Gemini disabled/5H"', () => {
      const payload: StatePayload = {
        agent: 'antigravity',
        project: 'Test',
        status: 'idle',
        message: 'ready',
        elapsed: 0,
        requires_action: false,
        actions: [],
        target_id: null,
        conversation_tokens: 100,
        antigravity_usage: {
          weekly_remaining_percent: 50,
          weekly_refresh_text: '',
          five_hour_remaining_percent: 95,
          five_hour_refresh_text: 'Refreshes in 10m',
          gemini_five_hour_remaining_percent: null,
          gemini_five_hour_refresh_text: 'disabled',
          claude_five_hour_remaining_percent: 78.19,
          claude_five_hour_refresh_text: 'Refreshes in 1h 32m',
        },
      };

      render(<Header connected={true} activePayload={payload} />);

      expect(screen.getByText('Gemini disabled/5H')).toBeInTheDocument();
      expect(screen.queryByText(/Gemini 0%/)).not.toBeInTheDocument();
    });

    test('Codex, Gemini, and Claude usage-value spans each receive color computed from their percent', () => {
      const payload: StatePayload = {
        agent: 'antigravity',
        project: 'Test',
        status: 'idle',
        message: 'ready',
        elapsed: 0,
        requires_action: false,
        actions: [],
        target_id: null,
        conversation_tokens: 100,
        codex_usage: {
          weekly_remaining_percent: 100,
          reset_text: 'Re: 8/16 18:00',
          reset_date: '8/16 18:00',
          reset_available: 0,
        },
        antigravity_usage: {
          weekly_remaining_percent: 80,
          weekly_refresh_text: '',
          five_hour_remaining_percent: null,
          five_hour_refresh_text: '',
          gemini_five_hour_remaining_percent: 50,
          gemini_five_hour_refresh_text: '',
          claude_five_hour_remaining_percent: 0,
          claude_five_hour_refresh_text: '',
        },
      };

      render(<Header connected={true} activePayload={payload} />);

      const codexSpan = screen.getByText(/100%/).closest('span');
      const geminiSpan = screen.getByText(/Gemini 50%\/5H/).closest('span');
      const claudeSpan = screen.getByText(/Claude 0%\/5H/).closest('span');

      expect(codexSpan?.style.color).toBeTruthy();
      expect(geminiSpan?.style.color).toBeTruthy();
      expect(claudeSpan?.style.color).toBeTruthy();

      expect(codexSpan?.style.color).not.toBe(geminiSpan?.style.color);
      expect(geminiSpan?.style.color).not.toBe(claudeSpan?.style.color);
      expect(codexSpan?.style.color).not.toBe(claudeSpan?.style.color);
    });

    test('renders Codex usage as "<percent>%/1W Re: <local reset date and time>" with no Reset/Resets word', () => {
      const payload: StatePayload = {
        agent: 'codex',
        project: 'Test',
        status: 'idle',
        message: 'ready',
        elapsed: 0,
        requires_action: false,
        actions: [],
        target_id: null,
        conversation_tokens: 100,
        codex_usage: {
          weekly_remaining_percent: 41,
          reset_text: 'Re: 8/16 18:00',
          reset_date: '8/16 18:00',
          reset_available: 1,
        },
      };

      const { container } = render(<Header connected={true} activePayload={payload} />);
      const codexEl = screen.getByText('41%/1W Re: 8/16 18:00');
      expect(codexEl).toBeInTheDocument();
      expect(container).not.toHaveTextContent(/Reset/i);
    });
  });

  describe('ProjectSelector Rendering Contracts', () => {
    test('renders project name and conversation_name without a visual status pill, while retaining status in aria-label', () => {
      const dummySelect = vi.fn();
      render(
        <ProjectSelector
          projects={sampleDashboardWithTurn.projects}
          activeId="envelope-active-1"
          onSelectProject={dummySelect}
        />
      );

      // Retains project names
      expect(screen.getByText('AgentDeck')).toBeInTheDocument();
      expect(screen.getByText('Secondary App')).toBeInTheDocument();

      // Renders conversation_name
      expect(screen.getByText('Task 2 H5 UI')).toBeInTheDocument();
      expect(screen.getByText('Background Worker')).toBeInTheDocument();

      // Project cards no longer render visual status text or status pills
      const projectsList = screen.getByRole('list');
      expect(projectsList.querySelector('.status-pill')).not.toBeInTheDocument();
      expect(projectsList).not.toHaveTextContent(/需要確認|執行中|已完成|錯誤|等待中|待命/);

      // Accessible aria-label includes project name, conversation_name, and status
      const btn = screen.getByRole('button', { name: '切換至專案 AgentDeck (Task 2 H5 UI)，狀態：需要確認' });
      expect(btn).toBeInTheDocument();
    });
  });

  describe('ActiveStatusBar Simplification Contracts', () => {
    test('removes agent tag, project title, and conversation subtitle, while keeping status mark, elapsed, tokens, and models', () => {
      const activePayload = sampleDashboardWithTurn.current!.payload;
      const { container } = render(<ActiveStatusBar payload={activePayload} />);

      // Status mark is retained
      expect(screen.getByText('需要確認')).toBeInTheDocument();

      // Metrics retained
      expect(screen.getByText('經過時間')).toBeInTheDocument();
      expect(screen.getByText('00:02:22')).toBeInTheDocument(); // 142 seconds formatted
      expect(screen.getByText('對話 Token')).toBeInTheDocument();
      expect(screen.getByText('12,450')).toBeInTheDocument();

      // Models retained
      expect(screen.getByText('Sol High')).toBeInTheDocument();
      expect(screen.getByText('Gemini 3.6 Flash')).toBeInTheDocument();

      // Agent tag, project title, conversation subtitle, and redundant status-top-row wrapper removed
      expect(container.querySelector('.status-top-row')).not.toBeInTheDocument();
      expect(container.querySelector('.agent-tag')).not.toBeInTheDocument();
      expect(container.querySelector('.project-title')).not.toBeInTheDocument();
      expect(container.querySelector('.conversation-subtitle')).not.toBeInTheDocument();
      expect(screen.queryByText('CODEX')).not.toBeInTheDocument();
      expect(screen.queryByText('Task 2 H5 UI')).not.toBeInTheDocument();
    });

    test('renders status hero, icon, compact metrics, and all models for active payload', () => {
      const activePayload = sampleDashboardWithTurn.current!.payload;
      const { container } = render(<ActiveStatusBar payload={activePayload} />);

      // Status hero container exists with action variant
      const hero = container.querySelector('.status-hero.status-hero-action');
      expect(hero).toBeInTheDocument();
      expect(hero).toHaveTextContent('需要確認');

      // Status icon is rendered inside hero with aria-hidden
      const icon = hero?.querySelector('svg.status-hero-icon');
      expect(icon).toBeInTheDocument();
      expect(icon).toHaveAttribute('aria-hidden', 'true');

      // Compact metrics
      expect(container.querySelector('.status-metrics-compact')).toBeInTheDocument();
      expect(screen.getByText('經過時間')).toBeInTheDocument();
      expect(screen.getByText('00:02:22')).toBeInTheDocument();
      expect(screen.getByText('對話 Token')).toBeInTheDocument();
      expect(screen.getByText('12,450')).toBeInTheDocument();

      // Models array complete display
      const modelsRow = container.querySelector('.status-models-row');
      expect(modelsRow).toBeInTheDocument();
      expect(screen.getByText('Sol High')).toBeInTheDocument();
      expect(screen.getByText('Gemini 3.6 Flash')).toBeInTheDocument();
    });

    test('maps all 6 status states to their corresponding status hero text and icons', () => {
      const basePayload: StatePayload = {
        agent: 'antigravity',
        project: 'Test',
        status: 'working',
        message: 'ready',
        elapsed: 10,
        requires_action: false,
        actions: [],
        target_id: null,
        conversation_tokens: 500,
        models: ['Model A'],
      };

      // working
      const { rerender, container } = render(<ActiveStatusBar payload={basePayload} />);
      expect(container.querySelector('.status-hero-working')).toBeInTheDocument();
      expect(screen.getByText('執行中')).toBeInTheDocument();

      // requires_action
      rerender(<ActiveStatusBar payload={{ ...basePayload, requires_action: true }} />);
      expect(container.querySelector('.status-hero-action')).toBeInTheDocument();
      expect(screen.getByText('需要確認')).toBeInTheDocument();

      // completed
      rerender(<ActiveStatusBar payload={{ ...basePayload, status: 'completed' }} />);
      expect(container.querySelector('.status-hero-completed')).toBeInTheDocument();
      expect(screen.getByText('已完成')).toBeInTheDocument();

      // error
      rerender(<ActiveStatusBar payload={{ ...basePayload, status: 'error' }} />);
      expect(container.querySelector('.status-hero-error')).toBeInTheDocument();
      expect(screen.getByText('錯誤')).toBeInTheDocument();

      // waiting
      rerender(<ActiveStatusBar payload={{ ...basePayload, status: 'waiting' }} />);
      expect(container.querySelector('.status-hero-waiting')).toBeInTheDocument();
      expect(screen.getByText('等待中')).toBeInTheDocument();

      // idle
      rerender(<ActiveStatusBar payload={{ ...basePayload, status: 'idle' }} />);
      expect(container.querySelector('.status-hero-idle')).toBeInTheDocument();
      expect(screen.getByText('待命')).toBeInTheDocument();
    });
  });
});
