import { describe, test, expect } from 'vitest';
import { render, screen, act } from '@testing-library/react';
import App from '../App';
import { sampleDashboardWithTurn } from '../fixtures/sampleDashboard';

describe('Responsive Same-DOM Contract', () => {
  test('uses identical DOM structure for both portrait and landscape viewports', () => {
    const { container } = render(<App />);

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

    const root = container.querySelector('.app-root');
    expect(root).toBeInTheDocument();

    const header = container.querySelector('.app-header');
    expect(header).toBeInTheDocument();

    const mainLayout = container.querySelector('.app-main-layout');
    expect(mainLayout).toBeInTheDocument();

    const sidebar = container.querySelector('.sidebar-section');
    expect(sidebar).toBeInTheDocument();

    const content = container.querySelector('.content-section');
    expect(content).toBeInTheDocument();

    const statusCard = container.querySelector('.active-status-card');
    expect(statusCard).toBeInTheDocument();

    const viewport = container.querySelector('.conversation-viewport');
    expect(viewport).toBeInTheDocument();

    const actionDock = container.querySelector('.action-dock');
    expect(actionDock).toBeInTheDocument();

    // Verify accessible labels exist
    expect(screen.getByLabelText('AgentDesk Header')).toBeInTheDocument();
    expect(screen.getByLabelText('專案列表')).toBeInTheDocument();
    expect(screen.getByLabelText('目前任務狀態')).toBeInTheDocument();
    expect(screen.getByLabelText('對話與事件進度')).toBeInTheDocument();
  });

  test('header landscape layout enforces 3-layer nowrap and compact sidebar width', async () => {
    // Import CSS file content to verify CSS contract rules directly
    const fs = await import('fs');
    const path = await import('path');
    const { fileURLToPath } = await import('url');
    const currentFilePath = fileURLToPath(import.meta.url);
    const cssPath = path.resolve(path.dirname(currentFilePath), '../index.css');
    const cssContent = fs.readFileSync(cssPath, 'utf-8');

    // Rule 1: .app-header must use display: grid and portrait grid-template-areas
    const headerBlockMatch = cssContent.match(/\.app-header\s*\{([^}]+)\}/);
    expect(headerBlockMatch).not.toBeNull();
    const headerBlock = headerBlockMatch![1];
    expect(headerBlock).toContain('display: grid');
    expect(headerBlock).toContain('grid-template-areas');
    expect(headerBlock).toContain('"brand connection"');
    expect(headerBlock).toContain('"usage usage"');
    expect(headerBlock).toContain('minmax(0, 1fr)');

    // Rule 2: Extract landscape media query block
    const mediaMatch = cssContent.match(/@media[^{]+\(orientation:\s*landscape\)[^{]*\{([\s\S]+?\n\})/);
    expect(mediaMatch).not.toBeNull();
    const mediaBlock = mediaMatch![1];

    expect(mediaBlock).toContain('"brand usage connection"');

    // Rule 3: Enforce flex-wrap: nowrap on all 3 layers in landscape
    const usageRowMatch = mediaBlock.match(/\.header-usage-row\s*\{([^}]+)\}/);
    expect(usageRowMatch).not.toBeNull();
    expect(usageRowMatch![1]).toContain('flex-wrap: nowrap');

    const usageItemMatch = mediaBlock.match(/\.usage-item\s*\{([^}]+)\}/);
    expect(usageItemMatch).not.toBeNull();
    expect(usageItemMatch![1]).toContain('flex-wrap: nowrap');

    const compactMatch = mediaBlock.match(/\.antigravity-compact\s*\{([^}]+)\}/);
    expect(compactMatch).not.toBeNull();
    expect(compactMatch![1]).toContain('flex-wrap: nowrap');

    // Rule 4: landscape sidebar width is compact (210px)
    expect(cssContent).toContain('width: 210px;');
  });

  test('active status bar responsive layout enforces landscape flex row and compact flex wrap rules', async () => {
    const fs = await import('fs');
    const path = await import('path');
    const { fileURLToPath } = await import('url');
    const currentFilePath = fileURLToPath(import.meta.url);
    const cssPath = path.resolve(path.dirname(currentFilePath), '../index.css');
    const cssContent = fs.readFileSync(cssPath, 'utf-8');

    // Rule 1: .active-status-card portrait column layout
    const statusCardMatch = cssContent.match(/\.active-status-card\s*\{([^}]+)\}/);
    expect(statusCardMatch).not.toBeNull();
    expect(statusCardMatch![1]).toContain('flex-direction: column');

    // Rule 2: landscape flex row media query rule for .active-status-card
    expect(cssContent).toContain('flex-direction: row');
    expect(cssContent).toContain('.active-status-card');

    // Rule 3: .status-hero is flex with icon/text alignment
    const heroMatch = cssContent.match(/\.status-hero\s*\{([^}]+)\}/);
    expect(heroMatch).not.toBeNull();
    expect(heroMatch![1]).toContain('display: flex');
    expect(heroMatch![1]).toContain('align-items: center');

    // Rule 4: .status-metrics-compact and .status-models-row enforce wrap to prevent overflow
    const metricsMatch = cssContent.match(/\.status-metrics-compact\s*\{([^}]+)\}/);
    expect(metricsMatch).not.toBeNull();
    expect(metricsMatch![1]).toContain('flex-wrap: wrap');

    const modelsMatch = cssContent.match(/\.status-models-row\s*\{([^}]+)\}/);
    expect(modelsMatch).not.toBeNull();
    expect(modelsMatch![1]).toContain('flex-wrap: wrap');
  });
});
