import React from 'react';

interface NewProgressBadgeProps {
  count: number;
  onClick: () => void;
}

export const NewProgressBadge: React.FC<NewProgressBadgeProps> = ({ count, onClick }) => {
  if (count <= 0) return null;

  return (
    <button
      type="button"
      className="new-progress-badge"
      onClick={onClick}
      aria-label={`有 ${count} 筆新進度，點擊捲動至底部`}
    >
      <span className="badge-arrow">↓</span>
      <span>{count} 筆新進度</span>
    </button>
  );
};
