package com.agentdeck.mobile;

import org.junit.Test;
import java.lang.reflect.Method;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.fail;

public final class HapticFeedbackDisabledTest {

    @Test
    public void verifyNoDeadHapticHelperMethodsInAgentDeckView() {
        Method[] methods = AgentDeckView.class.getDeclaredMethods();
        for (Method m : methods) {
            String name = m.getName();
            if (name.equalsIgnoreCase("performKeyHaptic")
                    || name.equalsIgnoreCase("performLongPressHaptic")
                    || name.equalsIgnoreCase("performConfirmHaptic")) {
                fail("Found dead/active haptic helper method in AgentDeckView: " + name);
            }
        }
    }

    @Test
    public void verifyPerformHapticFeedbackOverriddenInAgentDeckView() {
        try {
            Method m1 = AgentDeckView.class.getMethod("performHapticFeedback", int.class);
            assertEquals("performHapticFeedback(int) should be overridden in AgentDeckView",
                    AgentDeckView.class, m1.getDeclaringClass());

            Method m2 = AgentDeckView.class.getMethod("performHapticFeedback", int.class, int.class);
            assertEquals("performHapticFeedback(int, int) should be overridden in AgentDeckView",
                    AgentDeckView.class, m2.getDeclaringClass());
        } catch (NoSuchMethodException e) {
            fail("performHapticFeedback method not found: " + e.getMessage());
        }
    }
}
