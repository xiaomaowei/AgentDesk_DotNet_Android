import React from 'react';

interface RobotIconProps {
  className?: string;
  size?: number;
}

export const RobotIcon: React.FC<RobotIconProps> = ({ className = 'robot-mark-icon', size = 24 }) => (
  <svg
    width={size}
    height={size}
    viewBox="0 0 24 24"
    fill="none"
    xmlns="http://www.w3.org/2000/svg"
    className={className}
    data-testid="robot-mark-svg"
    aria-hidden="true"
  >
    {/* Red side ears */}
    <rect x="2" y="10" width="2" height="4" rx="1" fill="#FF6B7A" />
    <rect x="20" y="10" width="2" height="4" rx="1" fill="#FF6B7A" />

    {/* Top Antenna */}
    <line x1="12" y1="2" x2="12" y2="5" stroke="#CBD5E1" strokeWidth="2" strokeLinecap="round" />
    <circle cx="12" cy="2" r="1.5" fill="#4D9CFF" />

    {/* Light Gray Outer Shell */}
    <rect x="4" y="5" width="16" height="14" rx="4" fill="#CBD5E1" stroke="#94A3B8" strokeWidth="1" />

    {/* Dark Faceplate */}
    <rect x="6" y="8" width="12" height="8" rx="2" fill="#0F172A" />

    {/* Blue Eyes */}
    <circle cx="9" cy="12" r="1.5" fill="#4D9CFF" />
    <circle cx="15" cy="12" r="1.5" fill="#4D9CFF" />
  </svg>
);
