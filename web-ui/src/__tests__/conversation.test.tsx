import { describe, test, expect, vi } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import App from '../App';
import { ConversationView } from '../components/ConversationView';
import { SafeMarkdown } from '../components/SafeMarkdown';
import { sampleDashboardWithTurn, sampleDashboardLegacy } from '../fixtures/sampleDashboard';
import { StatePayload } from '../types/dashboard';

describe('Conversation & Component Rendering', () => {
  test('renders user prompt and current_turn items chronologically', () => {
    render(<ConversationView payload={sampleDashboardWithTurn.current!.payload} />);

    expect(screen.getByText('提問')).toBeInTheDocument();
    expect(screen.getByText('Test Prompt')).toBeInTheDocument();
    expect(screen.getByText('Analyzing codebase structure...')).toBeInTheDocument();
    expect(screen.getByText(/PreToolUse: git status/)).toBeInTheDocument();
    expect(screen.getByText(/PostToolUse: git status/)).toBeInTheDocument();
    expect(screen.getByText('✓ 最終答案')).toBeInTheDocument();
    expect(screen.getByText(/Task completed successfully/)).toBeInTheDocument();
  });

  test('renders tool item as compact single-line event with title and label', () => {
    const { container } = render(<ConversationView payload={sampleDashboardWithTurn.current!.payload} />);

    // Tool items are compact single line events
    const toolEvents = container.querySelectorAll('.compact-tool-event');
    expect(toolEvents.length).toBeGreaterThan(0);

    // Shows label and content combined in single line muted text
    expect(screen.getByText('PreToolUse: git status: git status --short')).toBeInTheDocument();
    expect(screen.getByText('PostToolUse: git status: M web-ui/src/App.tsx')).toBeInTheDocument();
  });

  test('renders legacy recent_events when current_turn is null', () => {
    render(<ConversationView payload={sampleDashboardLegacy.current!.payload} />);

    expect(screen.getByText('最新事件')).toBeInTheDocument();
    expect(screen.getByText(/git status: On branch main/)).toBeInTheDocument();
    expect(screen.getByText('Working tree is clean.')).toBeInTheDocument();
  });

  test('safe Markdown skips raw HTML and prevents external link navigation', () => {
    const htmlContent = 'Check [Link](https://example.com) and <script>alert("xss")</script>';
    render(<SafeMarkdown content={htmlContent} />);

    // Raw script tag should not be rendered in DOM
    expect(document.querySelector('script')).toBeNull();

    const link = screen.getByRole('link', { name: 'Link' });
    expect(link).toHaveAttribute('target', '_blank');
    expect(link).toHaveAttribute('rel', 'noopener noreferrer');

    const clickEvent = new MouseEvent('click', { cancelable: true, bubbles: true });
    link.dispatchEvent(clickEvent);
    expect(clickEvent.defaultPrevented).toBe(true);
  });

  test('App renders Approve and Reject buttons and triggers action message', () => {
    const postMessageSpy = vi.spyOn(window, 'postMessage');

    render(<App />);

    // Dispatch dashboard event with action required synchronously
    act(() => {
      window.dispatchEvent(
        new MessageEvent('message', {
          data: JSON.stringify({
            type: 'dashboard',
            dashboard: sampleDashboardWithTurn,
            connected: true,
          }),
          origin: window.location.origin,
        })
      );
    });

    // Check Approve & Reject buttons
    const approveBtn = screen.getByRole('button', { name: '核准此操作' });
    const rejectBtn = screen.getByRole('button', { name: '拒絕此操作' });

    expect(approveBtn).toBeInTheDocument();
    expect(rejectBtn).toBeInTheDocument();

    fireEvent.click(approveBtn);

    expect(postMessageSpy).toHaveBeenCalledWith(
      JSON.stringify({
        type: 'action',
        action: 'approve',
        target_id: 'target-action-99',
      }),
      expect.any(String)
    );

    postMessageSpy.mockRestore();
  });

  test('App handles project selection action', () => {
    const postMessageSpy = vi.spyOn(window, 'postMessage');

    render(<App />);

    act(() => {
      window.dispatchEvent(
        new MessageEvent('message', {
          data: JSON.stringify({
            type: 'dashboard',
            dashboard: sampleDashboardWithTurn,
            connected: true,
          }),
          origin: window.location.origin,
        })
      );
    });

    const project2Btn = screen.getByRole('button', { name: (name) => name.includes('Secondary App') });
    fireEvent.click(project2Btn);

    expect(postMessageSpy).toHaveBeenCalledWith(
      JSON.stringify({
        type: 'action',
        action: 'select_project',
        target_id: 'envelope-project-2',
      }),
      expect.any(String)
    );

    postMessageSpy.mockRestore();
  });

  test('detects same-count tool phase update while scrolled away and shows badge', () => {
    const initialPayload = sampleDashboardWithTurn.current!.payload;
    const { container, rerender } = render(<ConversationView payload={initialPayload} />);
    const scrollContainer = container.querySelector('.conversation-container')!;

    // Simulate user scrolled away from bottom
    Object.defineProperty(scrollContainer, 'scrollTop', { value: 0, configurable: true });
    Object.defineProperty(scrollContainer, 'scrollHeight', { value: 1000, configurable: true });
    Object.defineProperty(scrollContainer, 'clientHeight', { value: 300, configurable: true });
    fireEvent.scroll(scrollContainer);

    expect(screen.queryByText(/筆新進度/)).not.toBeInTheDocument();

    // Update same-count tool item phase from 'running' to 'completed'
    const updatedPayload: StatePayload = {
      ...initialPayload,
      current_turn: {
        ...initialPayload.current_turn!,
        items: initialPayload.current_turn!.items.map((item) =>
          item.id === 't1' ? { ...item, phase: 'completed' } : item
        ),
      },
    };

    rerender(<ConversationView payload={updatedPayload} />);

    // Badge should now appear with at least 1 new progress
    expect(screen.getByText(/1 筆新進度/)).toBeInTheDocument();
  });

  test('detects same-count new turn update while scrolled away and shows badge', () => {
    const initialPayload = sampleDashboardWithTurn.current!.payload;
    const { container, rerender } = render(<ConversationView payload={initialPayload} />);
    const scrollContainer = container.querySelector('.conversation-container')!;

    // Simulate user scrolled away from bottom
    Object.defineProperty(scrollContainer, 'scrollTop', { value: 0, configurable: true });
    Object.defineProperty(scrollContainer, 'scrollHeight', { value: 1000, configurable: true });
    Object.defineProperty(scrollContainer, 'clientHeight', { value: 300, configurable: true });
    fireEvent.scroll(scrollContainer);

    // New turn with same item count
    const newTurnPayload: StatePayload = {
      ...initialPayload,
      current_turn: {
        id: 'turn-002', // New turn ID
        started_at: '10:05:00',
        prompt: 'New turn prompt with same item count',
        items: initialPayload.current_turn!.items,
      },
    };

    rerender(<ConversationView payload={newTurnPayload} />);

    expect(screen.getByText(/1 筆新進度/)).toBeInTheDocument();
  });
});
