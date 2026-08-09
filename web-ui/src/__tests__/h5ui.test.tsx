import { describe, test, expect } from 'vitest';
import { render, screen, act } from '@testing-library/react';
import App from '../App';
import { Header } from '../components/Header';
import { ProjectSelector } from '../components/ProjectSelector';
import { TurnItemView } from '../components/TurnItemView';
import { ConversationView } from '../components/ConversationView';
import { ProgressStepper } from '../components/ProgressStepper';
import { StateEnvelope, StatePayload } from '../types/dashboard';
import { sampleDashboardWithTurn } from '../fixtures/sampleDashboard';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

describe('H5 Hybrid UI Regression Tests (Acceptance Criteria A-F)', () => {
  test('a. Header renders fixed SVG robot mark and contains no text emoji', () => {
    const { container } = render(<Header connected={true} activePayload={null} />);
    const svgIcon = screen.getByTestId('robot-mark-svg');
    expect(svgIcon).toBeInTheDocument();
    expect(container.textContent).not.toContain('🤖');
  });

  test('b. ProjectSelector displays conversation_name, hides empty value, and builds complete aria-label', () => {
    const projects: StateEnvelope[] = [
      {
        version: '1.0',
        type: 'state',
        id: 'p1',
        timestamp: null,
        payload: {
          agent: 'codex',
          project: 'ProjectA',
          conversation_name: 'Feature X',
          status: 'working',
          message: '',
          elapsed: 0,
          requires_action: false,
          actions: [],
          target_id: null,
          conversation_tokens: 0,
        },
      },
      {
        version: '1.0',
        type: 'state',
        id: 'p2',
        timestamp: null,
        payload: {
          agent: 'codex',
          project: 'ProjectB',
          conversation_name: '', // Empty
          status: 'idle',
          message: '',
          elapsed: 0,
          requires_action: false,
          actions: [],
          target_id: null,
          conversation_tokens: 0,
        },
      },
    ];

    const { container } = render(
      <ProjectSelector projects={projects} activeId="p1" onSelectProject={() => {}} />
    );

    // Displays conversation_name for p1
    expect(screen.getByText('Feature X')).toBeInTheDocument();

    // p2 has empty conversation_name -> project-conv-name span is not rendered for p2
    const items = container.querySelectorAll('.project-item-main');
    expect(items[0].querySelector('.project-conv-name')).toBeInTheDocument();
    expect(items[1].querySelector('.project-conv-name')).toBeNull();

    // aria-label for p1 includes conversation_name and status
    expect(
      screen.getByRole('button', { name: '切換至專案 ProjectA (Feature X)，狀態：執行中' })
    ).toBeInTheDocument();

    // aria-label for p2 omits conversation_name
    expect(
      screen.getByRole('button', { name: '切換至專案 ProjectB，狀態：待命' })
    ).toBeInTheDocument();
  });

  test('c. tool/command event uses compact muted ellipsis structure without emoji or English kind badge; reply uses primary class', () => {
    const toolItemView = render(
      <TurnItemView
        item={{
          id: 't1',
          timestamp: '10:00:00',
          kind: 'tool',
          phase: 'running',
          label: 'PreToolUse: git status',
          content: 'git status --short',
        }}
      />
    );
    const compactToolEl = toolItemView.container.querySelector('.compact-tool-event');
    expect(compactToolEl).toBeInTheDocument();

    // Check no emoji 🛠️ and terminal icon SVG exists
    expect(toolItemView.container.textContent).not.toContain('🛠️');
    expect(compactToolEl?.querySelector('svg.tool-event-icon')).toBeInTheDocument();
    expect(compactToolEl?.querySelector('.tool-event-text')).toHaveClass('tool-event-text');
    expect(compactToolEl).toHaveAttribute('title', 'PreToolUse: git status: git status --short');
    expect(compactToolEl).toHaveAttribute('aria-label', '工具活動: PreToolUse: git status (執行中)');

    const commentaryView = render(
      <TurnItemView
        item={{
          id: 'c1',
          timestamp: '10:00:00',
          kind: 'commentary',
          phase: 'completed',
          label: 'Reply',
          content: 'Hello assistant response',
        }}
      />
    );
    expect(commentaryView.container.querySelector('.turn-commentary')).toBeInTheDocument();
    expect(screen.getByText('Hello assistant response')).toBeInTheDocument();

    // Legacy recent_events test
    const legacyPayload: StatePayload = {
      agent: 'codex',
      project: 'P',
      status: 'idle',
      message: '',
      elapsed: 0,
      requires_action: false,
      actions: [],
      target_id: null,
      conversation_tokens: 0,
      recent_events: [
        { kind: 'command', label: 'git status', content: 'On branch main' },
        { kind: 'reply', label: 'Response', content: 'All clean' },
      ],
    };
    const legacyView = render(<ConversationView payload={legacyPayload} />);

    // Command event has terminal icon SVG and NO English kind badge
    const commandRow = legacyView.container.querySelector('.compact-event-row');
    expect(commandRow).toBeInTheDocument();
    expect(commandRow?.querySelector('svg.event-terminal-icon')).toBeInTheDocument();
    expect(commandRow?.querySelector('.event-kind')).toBeNull(); // No English kind badge
    expect(screen.getByText('git status: On branch main')).toBeInTheDocument();

    // Legacy reply item uses legacy-reply-item primary class
    const replyItem = legacyView.container.querySelector('.legacy-reply-item');
    expect(replyItem).toBeInTheDocument();
    expect(screen.getByText('All clean')).toBeInTheDocument();
  });

  test('d. ProgressStepper displays steps, clamps current_step, fallback on completed status, hides when no steps', () => {
    const steps = ['Step 1', 'Step 2', 'Step 3'];

    // Normal rendering
    const { rerender } = render(
      <ProgressStepper steps={steps} currentStep={2} status="working" />
    );
    expect(screen.getByLabelText(/任務步驟進度 \(第 2 \/ 3 步\)/)).toBeInTheDocument();
    expect(screen.getByText(/步驟 2\/3：/)).toBeInTheDocument();
    expect(screen.getByText('Step 2')).toBeInTheDocument();

    // Current step clamping upper bound
    rerender(<ProgressStepper steps={steps} currentStep={99} status="working" />);
    expect(screen.getByLabelText(/任務步驟進度 \(第 3 \/ 3 步\)/)).toBeInTheDocument();
    expect(screen.getByText('Step 3')).toBeInTheDocument();

    // Current step clamping lower bound
    rerender(<ProgressStepper steps={steps} currentStep={-5} status="working" />);
    expect(screen.getByLabelText(/任務步驟進度 \(第 1 \/ 3 步\)/)).toBeInTheDocument();
    expect(screen.getByText('Step 1')).toBeInTheDocument();

    // Completed fallback when currentStep is missing
    rerender(<ProgressStepper steps={steps} currentStep={null} status="completed" />);
    expect(screen.getByLabelText(/任務步驟進度 \(第 3 \/ 3 步\)/)).toBeInTheDocument();
    expect(screen.getByText('Step 3')).toBeInTheDocument();

    // Missing step fallback on non-completed status -> step 1
    rerender(<ProgressStepper steps={steps} currentStep={null} status="working" />);
    expect(screen.getByLabelText(/任務步驟進度 \(第 1 \/ 3 步\)/)).toBeInTheDocument();
    expect(screen.getByText('Step 1')).toBeInTheDocument();

    // Empty steps -> renders nothing
    const emptyView = render(<ProgressStepper steps={[]} currentStep={1} status="working" />);
    expect(emptyView.container.firstChild).toBeNull();
    const nullView = render(<ProgressStepper steps={null} currentStep={1} status="working" />);
    expect(nullView.container.firstChild).toBeNull();
  });

  test('e. App positions ProgressStepper between ActiveStatusBar and ConversationView', () => {
    const dashboardWithSteps = {
      ...sampleDashboardWithTurn,
      current: {
        ...sampleDashboardWithTurn.current!,
        payload: {
          ...sampleDashboardWithTurn.current!.payload,
          steps: ['Plan', 'Execute', 'Verify'],
          current_step: 2,
        },
      },
    };

    const { container } = render(<App />);
    act(() => {
      window.dispatchEvent(
        new MessageEvent('message', {
          data: JSON.stringify({
            type: 'dashboard',
            dashboard: dashboardWithSteps,
            connected: true,
          }),
          origin: window.location.origin,
        })
      );
    });

    const mainContent = container.querySelector('.content-section')!;
    const children = Array.from(mainContent.children);

    const statusBarIdx = children.findIndex((el) => el.classList.contains('active-status-card'));
    const stepperIdx = children.findIndex((el) => el.classList.contains('progress-stepper-card'));
    const conversationIdx = children.findIndex((el) => el.classList.contains('conversation-viewport'));

    expect(statusBarIdx).toBeGreaterThan(-1);
    expect(stepperIdx).toBeGreaterThan(statusBarIdx);
    expect(conversationIdx).toBeGreaterThan(stepperIdx);
  });

  test('f. Responsive CSS enforces 3-layer nowrap in landscape media query block and compact sidebar width', () => {
    const currentFilePath = fileURLToPath(import.meta.url);
    const cssPath = path.resolve(path.dirname(currentFilePath), '../index.css');
    const cssContent = fs.readFileSync(cssPath, 'utf-8');

    // Extract landscape media query block
    const mediaMatch = cssContent.match(/@media[^{]+\(orientation:\s*landscape\)[^{]*\{([\s\S]+?\n\})/);
    expect(mediaMatch).not.toBeNull();
    const mediaBlock = mediaMatch![1];

    // Assert flex-wrap: nowrap on all 3 layers inside landscape block
    const usageRowMatch = mediaBlock.match(/\.header-usage-row\s*\{([^}]+)\}/);
    expect(usageRowMatch).not.toBeNull();
    expect(usageRowMatch![1]).toContain('flex-wrap: nowrap');

    const usageItemMatch = mediaBlock.match(/\.usage-item\s*\{([^}]+)\}/);
    expect(usageItemMatch).not.toBeNull();
    expect(usageItemMatch![1]).toContain('flex-wrap: nowrap');

    const compactMatch = mediaBlock.match(/\.antigravity-compact\s*\{([^}]+)\}/);
    expect(compactMatch).not.toBeNull();
    expect(compactMatch![1]).toContain('flex-wrap: nowrap');

    // Landscape sidebar width compact 200-220px (210px)
    expect(cssContent).toContain('width: 210px;');
  });
});
