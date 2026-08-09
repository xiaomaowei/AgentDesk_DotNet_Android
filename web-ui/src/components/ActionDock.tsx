import React from 'react';
import { StatePayload, AllowedAction } from '../types/dashboard';

interface ActionDockProps {
  payload: StatePayload | null;
  pendingAction: AllowedAction | null;
  onAction: (action: 'approve' | 'reject', targetId: string | null) => void;
}

export const ActionDock: React.FC<ActionDockProps> = ({
  payload,
  pendingAction,
  onAction,
}) => {
  if (!payload || !payload.requires_action) {
    return null;
  }

  const targetId = payload.target_id;
  const isPendingApprove = pendingAction === 'approve';
  const isPendingReject = pendingAction === 'reject';

  return (
    <div className="action-dock" role="region" aria-label="待核准操作">
      <button
        type="button"
        className="btn btn-reject"
        disabled={pendingAction !== null}
        onClick={() => onAction('reject', targetId)}
        aria-label="拒絕此操作"
      >
        {isPendingReject ? '處理中...' : '✕ 拒絕'}
      </button>

      <button
        type="button"
        className="btn btn-approve"
        disabled={pendingAction !== null}
        onClick={() => onAction('approve', targetId)}
        aria-label="核准此操作"
      >
        {isPendingApprove ? '處理中...' : '✓ 核准'}
      </button>
    </div>
  );
};
