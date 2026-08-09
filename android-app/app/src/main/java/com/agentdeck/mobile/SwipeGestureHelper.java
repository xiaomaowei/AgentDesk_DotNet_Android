package com.agentdeck.mobile;

public final class SwipeGestureHelper {
    public enum Direction {
        NEXT_PROJECT("next_project"),
        PREVIOUS_PROJECT("previous_project");

        public final String actionName;

        Direction(String actionName) {
            this.actionName = actionName;
        }
    }

    private SwipeGestureHelper() {}

    public static Direction detectSwipe(
            float startX, float startY,
            float endX, float endY,
            float minDistancePx,
            float maxOffAxisRatio
    ) {
        float dx = endX - startX;
        float dy = endY - startY;

        float absDx = Math.abs(dx);
        float absDy = Math.abs(dy);

        if (absDx < minDistancePx) {
            return null;
        }

        if (absDx < absDy * maxOffAxisRatio) {
            return null;
        }

        if (dx < 0) {
            return Direction.NEXT_PROJECT; // Left swipe
        } else {
            return Direction.PREVIOUS_PROJECT; // Right swipe
        }
    }

    public static boolean shouldTriggerSwipeAction(int actionMasked, boolean swiping, boolean hasPendingAction, Direction dir) {
        if (actionMasked != 1) { // 1 == MotionEvent.ACTION_UP
            return false;
        }
        return swiping && !hasPendingAction && dir != null;
    }

    public static boolean shouldPerformClickOnTouchEnd(int actionMasked) {
        return actionMasked == 1; // 1 == MotionEvent.ACTION_UP
    }
}
