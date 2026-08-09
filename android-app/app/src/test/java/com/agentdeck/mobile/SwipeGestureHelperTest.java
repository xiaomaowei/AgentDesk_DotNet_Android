package com.agentdeck.mobile;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;

public final class SwipeGestureHelperTest {

    @Test
    public void detectsLeftSwipeAsNextProject() {
        SwipeGestureHelper.Direction dir = SwipeGestureHelper.detectSwipe(
                300f, 200f, 100f, 210f, 50f, 1.5f
        );
        assertEquals(SwipeGestureHelper.Direction.NEXT_PROJECT, dir);
    }

    @Test
    public void detectsRightSwipeAsPreviousProject() {
        SwipeGestureHelper.Direction dir = SwipeGestureHelper.detectSwipe(
                100f, 200f, 300f, 210f, 50f, 1.5f
        );
        assertEquals(SwipeGestureHelper.Direction.PREVIOUS_PROJECT, dir);
    }

    @Test
    public void ignoresSwipeBelowDistanceThreshold() {
        SwipeGestureHelper.Direction dir = SwipeGestureHelper.detectSwipe(
                100f, 200f, 120f, 200f, 50f, 1.5f
        );
        assertNull(dir);
    }

    @Test
    public void ignoresVerticalSwipeToPreventScrollViewConflict() {
        // Vertical movement is larger than horizontal / maxOffAxisRatio limit
        SwipeGestureHelper.Direction dir = SwipeGestureHelper.detectSwipe(
                100f, 100f, 180f, 300f, 50f, 1.5f
        );
        assertNull(dir);
    }

    @Test
    public void testTouchActionRoutingOnUpAndCancel() {
        // ACTION_UP (1) with valid swipe should trigger action
        assertTrue(SwipeGestureHelper.shouldTriggerSwipeAction(
                1, true, false, SwipeGestureHelper.Direction.NEXT_PROJECT
        ));
        assertTrue(SwipeGestureHelper.shouldPerformClickOnTouchEnd(1));

        // ACTION_CANCEL (3) should never trigger action or performClick
        assertFalse(SwipeGestureHelper.shouldTriggerSwipeAction(
                3, true, false, SwipeGestureHelper.Direction.NEXT_PROJECT
        ));
        assertFalse(SwipeGestureHelper.shouldPerformClickOnTouchEnd(3));

        // ACTION_UP with pending action should not trigger swipe action
        assertFalse(SwipeGestureHelper.shouldTriggerSwipeAction(
                1, true, true, SwipeGestureHelper.Direction.NEXT_PROJECT
        ));
    }
}
