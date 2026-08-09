import React, { useRef, useEffect, useState, useCallback, useMemo } from 'react';
import { StatePayload, RecentEvent } from '../types/dashboard';
import { TurnItemView } from './TurnItemView';
import { SafeMarkdown } from './SafeMarkdown';
import { NewProgressBadge } from './NewProgressBadge';
import { RobotIcon } from './RobotIcon';
import { SquareTerminal } from 'lucide-react';

interface ConversationViewProps {
  payload: StatePayload | null;
}

export const ConversationView: React.FC<ConversationViewProps> = ({ payload }) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const [isNearBottom, setIsNearBottom] = useState<boolean>(true);
  const [unreadCount, setUnreadCount] = useState<number>(0);

  const turn = payload?.current_turn;
  const recentEvents = payload?.recent_events;

  // Calculate total items count
  const currentItemsCount = turn
    ? turn.items.length + 1 // +1 for prompt
    : recentEvents
    ? recentEvents.length
    : 0;

  // Compute stable revision signature of current turn / events
  const currentRevisionSignature = useMemo(() => {
    if (turn) {
      const itemsSig = turn.items
        .map((i) => `${i.id}:${i.phase}:${i.timestamp}:${i.label}:${i.content || ''}`)
        .join('|');
      return `turn:${turn.id}:${turn.started_at}:${turn.prompt}:${itemsSig}`;
    }
    if (recentEvents && recentEvents.length > 0) {
      const eventsSig = recentEvents
        .map((e) => `${e.kind}:${e.label}:${e.content || ''}`)
        .join('|');
      return `legacy:${eventsSig}`;
    }
    return 'empty';
  }, [turn, recentEvents]);

  const prevRevisionRef = useRef<string>(currentRevisionSignature);
  const prevItemsCountRef = useRef<number>(currentItemsCount);

  const handleScroll = useCallback(() => {
    if (!containerRef.current) return;
    const { scrollTop, scrollHeight, clientHeight } = containerRef.current;
    const distanceToBottom = scrollHeight - (scrollTop + clientHeight);
    const nearBottom = distanceToBottom <= 60;
    setIsNearBottom(nearBottom);
    if (nearBottom) {
      setUnreadCount(0);
    }
  }, []);

  const scrollToBottom = useCallback(() => {
    if (containerRef.current) {
      containerRef.current.scrollTop = containerRef.current.scrollHeight;
      setIsNearBottom(true);
      setUnreadCount(0);
    }
  }, []);

  useEffect(() => {
    const revisionChanged = currentRevisionSignature !== prevRevisionRef.current;
    if (revisionChanged) {
      const countDiff = currentItemsCount - prevItemsCountRef.current;
      if (isNearBottom) {
        scrollToBottom();
      } else {
        const increment = countDiff > 0 ? countDiff : 1;
        setUnreadCount((prev) => prev + increment);
      }
      prevRevisionRef.current = currentRevisionSignature;
      prevItemsCountRef.current = currentItemsCount;
    }
  }, [currentRevisionSignature, currentItemsCount, isNearBottom, scrollToBottom]);

  if (!payload) {
    return (
      <div className="conversation-container empty-conversation">
        <div className="empty-state-card">
          <span className="empty-icon"><RobotIcon size={48} /></span>
          <h3>等待本機 Bridge 連線</h3>
          <p>請於電腦上執行 Start-AgentDeckAndroid.ps1</p>
          <p className="empty-sub">手機將透過 USB 自動同步 AI Agent 儀表板</p>
        </div>
      </div>
    );
  }

  return (
    <div className="conversation-viewport">
      <div
        ref={containerRef}
        className="conversation-container"
        onScroll={handleScroll}
        role="region"
        aria-label="對話與事件進度"
      >
        {turn ? (
          <div className="current-turn-view">
            {/* User Prompt */}
            <div className="user-prompt-card">
              <div className="prompt-header">
                <span className="prompt-icon">👤</span>
                <span className="prompt-title">提問</span>
                <span className="prompt-timestamp">{turn.started_at}</span>
              </div>
              <div className="prompt-body">
                <SafeMarkdown content={turn.prompt} />
              </div>
            </div>

            {/* Turn Items */}
            <div className="turn-items-list">
              {turn.items.map((item) => (
                <TurnItemView key={item.id} item={item} />
              ))}
            </div>
          </div>
        ) : recentEvents && recentEvents.length > 0 ? (
          /* Legacy Fallback: Recent Events */
          <div className="legacy-events-view">
            <div className="legacy-events-header">
              <span className="legacy-title">最新事件</span>
            </div>
            <div className="legacy-events-list">
              {recentEvents.map((ev: RecentEvent, idx: number) => {
                const isCommandOrTool = ev.kind === 'command' || ev.kind === 'tool';
                const fullText = ev.content ? `${ev.label}: ${ev.content}` : ev.label;

                if (isCommandOrTool) {
                  return (
                    <div
                      key={idx}
                      className={`legacy-event-item compact-event-row kind-${ev.kind}`}
                      title={fullText}
                      aria-label={`事件: ${ev.label}`}
                    >
                      <SquareTerminal className="event-terminal-icon" size={14} aria-hidden="true" />
                      <span className="event-compact-text">{fullText}</span>
                    </div>
                  );
                }

                return (
                  <div key={idx} className={`legacy-event-item legacy-reply-item kind-${ev.kind}`}>
                    <div className="event-meta">
                      <span className="event-kind">{ev.kind}</span>
                      <span className="event-label">{ev.label}</span>
                    </div>
                    {ev.content && (
                      <div className="event-content">
                        <SafeMarkdown content={ev.content} />
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          </div>
        ) : (
          <div className="empty-turn">
            <p>目前尚無對話進度與事件紀錄</p>
          </div>
        )}
      </div>

      <NewProgressBadge count={unreadCount} onClick={scrollToBottom} />
    </div>
  );
};
