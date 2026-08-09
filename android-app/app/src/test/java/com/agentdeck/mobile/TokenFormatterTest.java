package com.agentdeck.mobile;

import org.junit.Test;

import static org.junit.Assert.assertEquals;

public final class TokenFormatterTest {

    @Test
    public void formatsNullOrNegativeTokens() {
        assertEquals("--", TokenFormatter.formatCompact(null));
        assertEquals("--", TokenFormatter.formatCompact(-100));
        assertEquals("--", TokenFormatter.formatFull(null));
        assertEquals("--", TokenFormatter.formatFull(-100));
    }

    @Test
    public void formatsCompactValues() {
        assertEquals("0", TokenFormatter.formatCompact(0));
        assertEquals("999", TokenFormatter.formatCompact(999));
        assertEquals("1K", TokenFormatter.formatCompact(1000));
        assertEquals("12.4K", TokenFormatter.formatCompact(12400));
        assertEquals("100K", TokenFormatter.formatCompact(100000));
        assertEquals("12.2M", TokenFormatter.formatCompact(12200000));
    }

    @Test
    public void formatsFullThousandSeparatedValues() {
        assertEquals("0", TokenFormatter.formatFull(0));
        assertEquals("999", TokenFormatter.formatFull(999));
        assertEquals("1,000", TokenFormatter.formatFull(1000));
        assertEquals("12,400", TokenFormatter.formatFull(12400));
        assertEquals("12,200,000", TokenFormatter.formatFull(12200000));
    }
}
