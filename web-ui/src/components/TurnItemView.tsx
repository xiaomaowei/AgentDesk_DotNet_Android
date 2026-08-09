import React from 'react';
import { SquareTerminal } from 'lucide-react';
import { TurnItem } from '../types/dashboard';
import { SafeMarkdown } from './SafeMarkdown';

interface TurnItemViewProps {
  item: TurnItem;
}

export const TurnItemView: React.FC<TurnItemViewProps> = ({ item }) => {
  if (item.kind === 'commentary') {
    return (
      <div className="turn-item turn-commentary">
        <SafeMarkdown content={item.content || item.label} />
      </div>
    );
  }

  if (item.kind === 'final') {
    return (
      <div className="turn-item turn-final" role="region" aria-label="最終答案">
        <div className="final-header">
          <span className="final-badge">✓ 最終答案</span>
          <span className="final-timestamp">{item.timestamp}</span>
        </div>
        <div className="final-body">
          <SafeMarkdown content={item.content || item.label} />
        </div>
      </div>
    );
  }

  if (item.kind === 'approval') {
    return (
      <div className="turn-item turn-approval" role="alert">
        <div className="approval-header">
          <span className="approval-badge">⚠️ 需要確認</span>
          <span className="approval-timestamp">{item.timestamp}</span>
        </div>
        <div className="approval-label">{item.label}</div>
        {item.content && <div className="approval-content">{item.content}</div>}
      </div>
    );
  }

  // item.kind === 'tool' (Compact muted single-line event with SquareTerminal icon)
  const phaseLabel =
    item.phase === 'running'
      ? '執行中'
      : item.phase === 'completed'
      ? '已完成'
      : item.phase === 'waiting'
      ? '等待中'
      : '已傳送';

  const fullText = item.content ? `${item.label}: ${item.content}` : item.label;

  return (
    <div
      className={`turn-item turn-tool compact-tool-event phase-${item.phase}`}
      title={fullText}
      aria-label={`工具活動: ${item.label} (${phaseLabel})`}
    >
      <SquareTerminal className="tool-event-icon" size={14} aria-hidden="true" />
      <span className="tool-event-text">{fullText}</span>
      <span className={`phase-badge phase-${item.phase}`}>{phaseLabel}</span>
    </div>
  );
};
