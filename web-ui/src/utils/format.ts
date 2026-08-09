/**
 * Format percentage with max 2 decimal places and trimmed trailing zeros.
 * Preserves decimals without rounding them away (e.g. 63.56 -> "63.56", 78.19 -> "78.19", 63.5 -> "63.5", 60 -> "60").
 */
export function formatDecimalPercent(val: number): string {
  if (val === null || val === undefined || isNaN(val)) {
    return '';
  }
  const rounded = Math.round((val + Number.EPSILON) * 100) / 100;
  return String(rounded);
}

/**
 * Compact refresh text:
 * - Replaces prefixes "Refreshes in ", "Refreshes ", "Re:" with "Re:" without modifying the remaining string.
 * - For "Quota available", returns "Quota avail".
 */
export function normalizeRefreshText(raw: string | null | undefined): string {
  if (!raw) return '';
  const trimmed = raw.trim();
  if (!trimmed) return '';

  if (trimmed.toLowerCase() === 'quota available') {
    return 'Quota avail';
  }

  let rest = trimmed;
  if (/^refreshes\s+in\s+/i.test(rest)) {
    rest = rest.replace(/^refreshes\s+in\s+/i, '');
  } else if (/^refreshes\s+/i.test(rest)) {
    rest = rest.replace(/^refreshes\s+/i, '');
  } else if (/^re:\s*/i.test(rest)) {
    rest = rest.replace(/^re:\s*/i, '');
  }
  rest = rest.trim();
  return rest ? `Re:${rest}` : '';
}

/**
 * Convert a usage percentage (0–100) to an HSL color string for continuous
 * color feedback:
 *   0%   → red   (hue   0)
 *   50%  → yellow (hue  60)
 *   100% → green  (hue 120)
 *
 * Formula: hue = clamp(percent, 0, 100) * 1.2
 * Saturation and lightness are fixed for readable, accessible colours.
 */
export function usageColor(percent: number): string {
  const clamped = Math.max(0, Math.min(100, isNaN(percent) ? 0 : percent));
  const hue = clamped * 1.2; // 0→0, 50→60, 100→120
  return `hsl(${hue.toFixed(1)}, 85%, 55%)`;
}
