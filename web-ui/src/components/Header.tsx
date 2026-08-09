import React from 'react';
import { StatePayload } from '../types/dashboard';
import { formatDecimalPercent, normalizeRefreshText, usageColor } from '../utils/format';

import { RobotIcon } from './RobotIcon';

interface HeaderProps {
  connected: boolean;
  activePayload: StatePayload | null;
}

export const Header: React.FC<HeaderProps> = ({ connected, activePayload }) => {
  const codexUsage = activePayload?.codex_usage;
  const antigravityUsage = activePayload?.antigravity_usage;

  // Format Gemini 5-hour
  let geminiText: string | null = null;
  let geminiPercent: number | null = null;
  if (antigravityUsage) {
    if (antigravityUsage.gemini_five_hour_remaining_percent !== null && antigravityUsage.gemini_five_hour_remaining_percent !== undefined) {
      geminiPercent = antigravityUsage.gemini_five_hour_remaining_percent;
      const pct = formatDecimalPercent(geminiPercent);
      geminiText = `Gemini ${pct}%/5H`;
      const ref = normalizeRefreshText(antigravityUsage.gemini_five_hour_refresh_text);
      if (ref) {
        geminiText += ` · ${ref}`;
      }
    } else if (antigravityUsage.five_hour_remaining_percent !== null && antigravityUsage.five_hour_remaining_percent !== undefined) {
      geminiPercent = antigravityUsage.five_hour_remaining_percent;
      const pct = formatDecimalPercent(geminiPercent);
      geminiText = `Gemini ${pct}%/5H`;
      const ref = normalizeRefreshText(antigravityUsage.five_hour_refresh_text);
      if (ref) {
        geminiText += ` · ${ref}`;
      }
    }
  }

  // Format Claude 5-hour
  let claudeText: string | null = null;
  let claudePercent: number | null = null;
  if (antigravityUsage) {
    const isClaudeDisabled =
      antigravityUsage.claude_five_hour_remaining_percent == null &&
      antigravityUsage.claude_five_hour_refresh_text?.trim().toLowerCase() === 'disabled';

    if (antigravityUsage.claude_five_hour_remaining_percent !== null && antigravityUsage.claude_five_hour_remaining_percent !== undefined) {
      claudePercent = antigravityUsage.claude_five_hour_remaining_percent;
      const pct = formatDecimalPercent(claudePercent);
      claudeText = `Claude ${pct}%/5H`;
      const ref = normalizeRefreshText(antigravityUsage.claude_five_hour_refresh_text);
      if (ref) {
        claudeText += ` · ${ref}`;
      }
    } else if (isClaudeDisabled) {
      claudeText = 'Claude disabled/5H';
      claudePercent = null;
    }
  }

  // Format Codex usage
  let codexText: string | null = null;
  let codexPercent: number | null = null;
  if (codexUsage) {
    if (codexUsage.weekly_remaining_percent !== null && codexUsage.weekly_remaining_percent !== undefined) {
      codexPercent = codexUsage.weekly_remaining_percent;
      const pct = formatDecimalPercent(codexPercent);
      const ref = normalizeRefreshText(codexUsage.reset_text);
      codexText = `${pct}%`;
      if (ref) {
        codexText += ` · ${ref}`;
      }
    } else if (codexUsage.reset_text) {
      codexText = normalizeRefreshText(codexUsage.reset_text) || codexUsage.reset_text;
    }
  }

  return (
    <header className="app-header" aria-label="AgentDesk Header">
      <div className="header-brand">
        <span className="brand-logo" aria-hidden="true">
          <RobotIcon size={22} />
        </span>
      </div>

      <div className="header-usage-row">
        {codexText && (
          <div className="usage-item" title="Codex Usage">
            <span className="badge badge-neon">CODEX</span>
            <span
              className="usage-value"
              style={codexPercent !== null ? { color: usageColor(codexPercent) } : undefined}
            >{codexText}</span>
          </div>
        )}

        {(geminiText || claudeText) && (
          <div className="usage-item" title="Antigravity Usage">
            <span className="badge badge-purple">ANTIGRAVITY</span>
            <div className="antigravity-compact">
              {geminiText && (
                <span
                  className="usage-value"
                  style={geminiPercent !== null ? { color: usageColor(geminiPercent) } : undefined}
                >{geminiText}</span>
              )}
              {geminiText && claudeText && <span className="usage-sep">·</span>}
              {claudeText && (
                <span
                  className="usage-value"
                  style={claudePercent !== null ? { color: usageColor(claudePercent) } : undefined}
                >{claudeText}</span>
              )}
            </div>
          </div>
        )}
      </div>

      <div className="header-connection" role="status" aria-label={connected ? "連接狀態：本機在線" : "連接狀態：等待 Bridge"}>
        <span className={`connection-dot ${connected ? 'connected' : 'disconnected'}`} aria-hidden="true" />
        <span className="connection-text">{connected ? '本機在線' : '等待 Bridge'}</span>
      </div>
    </header>
  );
};
