import React from 'react';
import { useHostBridge } from './hooks/useHostBridge';
import { Header } from './components/Header';
import { ProjectSelector } from './components/ProjectSelector';
import { ActiveStatusBar } from './components/ActiveStatusBar';
import { ProgressStepper } from './components/ProgressStepper';
import { ConversationView } from './components/ConversationView';
import { ActionDock } from './components/ActionDock';
import './index.css';

export const App: React.FC = () => {
  const { dashboard, connected, sendAction, pendingAction } = useHostBridge();

  const activeEnvelope = dashboard.current;
  const activePayload = activeEnvelope ? activeEnvelope.payload : null;
  const activeEnvelopeId = activeEnvelope ? activeEnvelope.id : null;

  return (
    <div className="app-root">
      <Header connected={connected} activePayload={activePayload} />

      <div className="app-main-layout">
        <aside className="sidebar-section">
          <ProjectSelector
            projects={dashboard.projects}
            activeId={activeEnvelopeId}
            onSelectProject={(targetId) => sendAction('select_project', targetId)}
          />
        </aside>

        <main className="content-section">
          <ActiveStatusBar payload={activePayload} />
          <ProgressStepper
            steps={activePayload?.steps}
            currentStep={activePayload?.current_step}
            status={activePayload?.status}
            message={activePayload?.message}
            hasPayload={activePayload !== null && activePayload !== undefined}
          />
          <ConversationView payload={activePayload} />
          <ActionDock
            payload={activePayload}
            pendingAction={pendingAction}
            onAction={(action, targetId) => sendAction(action, targetId)}
          />
        </main>
      </div>
    </div>
  );
};

export default App;
