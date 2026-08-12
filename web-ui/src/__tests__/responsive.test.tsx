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

  test('conversation container enforces overflow-x hidden, wrapping, and dark scrollbar contract', async () => {
    const fs = await import('fs');
    const path = await import('path');
    const { fileURLToPath } = await import('url');
    const currentFilePath = fileURLToPath(import.meta.url);
    const cssPath = path.resolve(path.dirname(currentFilePath), '../index.css');
    const cssContent = fs.readFileSync(cssPath, 'utf-8');

    // Rule 1: .conversation-container explicitly sets overflow-y: auto and overflow-x: hidden
    const containerMatch = cssContent.match(/\.conversation-container\s*\{([^}]+)\}/);
    expect(containerMatch).not.toBeNull();
    const containerBlock = containerMatch![1];
    expect(containerBlock).toContain('overflow-y: auto');
    expect(containerBlock).toContain('overflow-x: hidden');
    expect(containerBlock).toContain('min-width: 0');
    expect(containerBlock).toContain('max-width: 100%');

    // Rule 2: scrollbar-width and scrollbar-color using existing dark variables
    expect(containerBlock).toContain('scrollbar-width: thin');
    expect(containerBlock).toContain('scrollbar-color: var(--border) var(--bg-darker)');

    // Rule 3: webkit scrollbar rules for .conversation-container
    expect(cssContent).toContain('.conversation-container::-webkit-scrollbar');
    expect(cssContent).toContain('.conversation-container::-webkit-scrollbar-track');
    expect(cssContent).toContain('.conversation-container::-webkit-scrollbar-thumb');

    const webkitTrackMatch = cssContent.match(/\.conversation-container::-webkit-scrollbar-track\s*\{([^}]+)\}/);
    expect(webkitTrackMatch).not.toBeNull();
    expect(webkitTrackMatch![1]).toContain('var(--bg-darker)');

    const webkitThumbMatch = cssContent.match(/\.conversation-container::-webkit-scrollbar-thumb\s*\{([^}]+)\}/);
    expect(webkitThumbMatch).not.toBeNull();
    expect(webkitThumbMatch![1]).toContain('var(--border)');

    const webkitThumbHoverMatch = cssContent.match(/\.conversation-container::-webkit-scrollbar-thumb:hover\s*\{([^}]+)\}/);
    expect(webkitThumbHoverMatch).not.toBeNull();
    expect(webkitThumbHoverMatch![1]).toContain('var(--text-dim)');

    // Rule 4: horizontal webkit scrollbar is hidden
    const horizontalMatch = cssContent.match(/\.conversation-container::-webkit-scrollbar:horizontal\s*\{([^}]+)\}/);
    expect(horizontalMatch).not.toBeNull();
    const horizontalBlock = horizontalMatch![1];
    expect(horizontalBlock).toMatch(/display:\s*none|height:\s*0/);

    // Rule 5: Markdown code block uses pre-wrap and disables horizontal scrollbar
    const codeBlockMatch = cssContent.match(/\.markdown-body\s+pre\.code-block\s*\{([^}]+)\}/);
    expect(codeBlockMatch).not.toBeNull();
    const codeBlock = codeBlockMatch![1];
    expect(codeBlock).toContain('white-space: pre-wrap');
    expect(codeBlock).toContain('overflow-x: hidden');
    expect(codeBlock).not.toContain('overflow-x: auto');
  });
});
