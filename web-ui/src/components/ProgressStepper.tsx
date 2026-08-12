import React from 'react';

interface ProgressStepperProps {
  steps?: string[] | null;
  currentStep?: number | null;
  status?: string;
  message?: string | null;
  hasPayload?: boolean;
}

const getFallbackTitle = (message?: string | null, status?: string): string => {
  if (message && typeof message === 'string' && message.trim() !== '') {
    return message.trim();
  }
  switch (status) {
    case 'working':
      return '執行中';
    case 'completed':
      return '已完成';
    case 'error':
      return '錯誤';
    case 'waiting':
      return '等待中';
    case 'idle':
    default:
      return '待命';
  }
};

export const ProgressStepper: React.FC<ProgressStepperProps> = ({
  steps,
  currentStep,
  status,
  message,
  hasPayload,
}) => {
  const hasSteps = Array.isArray(steps) && steps.length > 0;
  const payloadExists =
    hasPayload !== undefined
      ? hasPayload
      : hasSteps || status !== undefined || (message !== undefined && message !== null);

  if (!payloadExists) {
    return null;
  }

  const effectiveSteps = hasSteps ? steps! : [getFallbackTitle(message, status)];
  const totalSteps = effectiveSteps.length;

  let rawStep: number;
  if (
    currentStep !== null &&
    currentStep !== undefined &&
    typeof currentStep === 'number' &&
    !isNaN(currentStep)
  ) {
    rawStep = currentStep;
  } else if (status === 'completed') {
    rawStep = totalSteps;
  } else {
    rawStep = 1;
  }

  const clampedStep = Math.min(Math.max(1, rawStep), totalSteps);
  const activeIndex = clampedStep - 1;
  const currentStepTitle = effectiveSteps[activeIndex] || '';

  return (
    <div
      className="progress-stepper-card"
      role="region"
      aria-label={`任務步驟進度 (第 ${clampedStep} / ${totalSteps} 步)`}
    >
      <div className="stepper-track" role="list">
        {effectiveSteps.map((stepName, idx) => {
          const stepNum = idx + 1;
          const isCompleted =
            status === 'completed' ? idx <= activeIndex : idx < activeIndex;
          const isCurrent = idx === activeIndex;

          let stepStateClass = 'pending';
          if (isCompleted) {
            stepStateClass = 'completed';
          } else if (isCurrent) {
            stepStateClass = 'current';
          }

          return (
            <React.Fragment key={idx}>
              {idx > 0 && (
                <div
                  className={`stepper-line ${idx <= activeIndex ? 'completed' : ''}`}
                  aria-hidden="true"
                />
              )}
              <div
                className={`stepper-node ${stepStateClass}`}
                role="listitem"
                aria-label={`步驟 ${stepNum}: ${stepName}`}
                aria-current={isCurrent ? 'step' : undefined}
              >
                <span className="node-number">
                  {isCompleted && status === 'completed' && idx === totalSteps - 1 ? '✓' : stepNum}
                </span>
              </div>
            </React.Fragment>
          );
        })}
      </div>

      <div className="stepper-title-row">
        <span className="stepper-current-label">
          步驟 {clampedStep}/{totalSteps}：<span className="stepper-title-text">{currentStepTitle}</span>
        </span>
      </div>
    </div>
  );
};
