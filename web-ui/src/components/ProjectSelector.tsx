import React from 'react';
import { StateEnvelope } from '../types/dashboard';

interface ProjectSelectorProps {
  projects: StateEnvelope[];
  activeId: string | null;
  onSelectProject: (targetId: string) => void;
}

export const ProjectSelector: React.FC<ProjectSelectorProps> = ({
  projects,
  activeId,
  onSelectProject,
}) => {
  if (!projects || projects.length === 0) {
    return null;
  }

  return (
    <nav className="projects-card" aria-label="專案列表">
      <div className="projects-header">專案列表</div>
      <div className="projects-list" role="list">
        {projects.map((p) => {
          const isSelected = p.id === activeId;
          const statusText = p.payload.requires_action
            ? '需要確認'
            : p.payload.status === 'working'
            ? '執行中'
            : p.payload.status === 'completed'
            ? '已完成'
            : p.payload.status === 'error'
            ? '錯誤'
            : p.payload.status === 'waiting'
            ? '等待中'
            : '待命';

          const convName = p.payload.conversation_name?.trim();
          const convAria = convName ? ` (${convName})` : '';
          const ariaLabel = `切換至專案 ${p.payload.project}${convAria}，狀態：${statusText}`;

          return (
            <button
              key={p.id}
              type="button"
              className={`project-item ${isSelected ? 'selected' : ''}`}
              onClick={() => onSelectProject(p.id)}
              aria-label={ariaLabel}
              aria-current={isSelected ? 'true' : undefined}
            >
              <div className="project-item-main">
                <span className="project-name">{p.payload.project}</span>
                {convName ? <span className="project-conv-name">{convName}</span> : null}
              </div>
            </button>
          );
        })}
      </div>
    </nav>
  );
};
