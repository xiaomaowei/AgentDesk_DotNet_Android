import React from 'react';
import { AlertCircle, Loader2, CheckCircle2, XCircle, Clock, PauseCircle } from 'lucide-react';
import { StatePayload } from '../types/dashboard';

interface ActiveStatusBarProps {
  payload: StatePayload | null;
}

function formatElapsed(seconds: number): string {
  const safe = Math.max(0, seconds || 0);
  const h = Math.floor(safe / 3600);
  const m = Math.floor((safe % 3600) / 60);
  const s = safe % 60;
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${pad(h)}:${pad(m)}:${pad(s)}`;
}

export const ActiveStatusBar: React.FC<ActiveStatusBarProps> = ({ payload }) => {
  if (!payload) {
    return (
      <div className="active-status-card empty-status">
        <div className="status-label">無使用中專案</div>
        <div className="status-sub">等待本機 Bridge 連線與任務發起</div>
      </div>
    );
  }

  const getStatusConfig = () => {
    if (payload.requires_action) {
      return {
        text: '需要確認',
        type: 'action',
        Icon: AlertCircle,
      };
    }
    switch (payload.status) {
      case 'working':
        return { text: '執行中', type: 'working', Icon: Loader2 };
      case 'completed':
        return { text: '已完成', type: 'completed', Icon: CheckCircle2 };
      case 'error':
        return { text: '錯誤', type: 'error', Icon: XCircle };
      case 'waiting':
        return { text: '等待中', type: 'waiting', Icon: Clock };
      case 'idle':
      default:
        return { text: '待命', type: 'idle', Icon: PauseCircle };
    }
  };

  const { text: statusText, type: statusType, Icon: StatusIcon } = getStatusConfig();

  return (
    <div className="active-status-card" role="region" aria-label="目前任務狀態">
      <div className={`status-hero status-hero-${statusType}`}>
        <StatusIcon className="status-hero-icon" aria-hidden="true" size={24} />
        <span className="status-hero-text">{statusText}</span>
      </div>

      <div className="status-details">
        <div className="status-metrics-compact">
          <div className="metric-inline">
            <span className="metric-label">經過時間</span>
            <span className="metric-value value-cyan">{formatElapsed(payload.elapsed)}</span>
          </div>

          <div className="metric-inline">
            <span className="metric-label">對話 Token</span>
            <span className="metric-value value-blue">
              {payload.conversation_tokens !== null && payload.conversation_tokens !== undefined
                ? payload.conversation_tokens.toLocaleString()
                : '--'}
            </span>
          </div>
        </div>

        {payload.models && payload.models.length > 0 && (
          <div className="status-models-row" aria-label="模型標籤">
            <span className="models-label">模型:</span>
            {payload.models.map((m, idx) => (
              <span key={idx} className="model-badge">
                {m}
              </span>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};
