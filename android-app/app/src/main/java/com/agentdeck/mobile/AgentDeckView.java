package com.agentdeck.mobile;

import android.annotation.SuppressLint;
import android.content.Context;
import android.content.res.ColorStateList;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.LinearGradient;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.RectF;
import android.graphics.Shader;
import android.graphics.drawable.GradientDrawable;
import android.graphics.drawable.RippleDrawable;
import android.os.Build;
import android.os.SystemClock;
import android.text.Layout;
import android.text.StaticLayout;
import android.text.TextPaint;
import android.text.TextUtils;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;

import java.util.List;
import java.util.Locale;

@SuppressLint("SetTextI18n")
public final class AgentDeckView extends FrameLayout {

    public interface ActionListener {
        void onAction(String action, String targetId);
    }

    // Modern Dark Ops Design Tokens with High WCAG Contrast (>= 4.5:1)
    private static final int BG_DARK = Color.rgb(15, 23, 42);          // #0F172A Slate 900
    private static final int SURFACE = Color.rgb(30, 41, 59);          // #1E293B Slate 800
    private static final int SURFACE_ALT = Color.rgb(24, 34, 50);      // #182232 Dark Slate
    private static final int SURFACE_SELECTED = Color.rgb(30, 58, 107); // Selected Project BG
    private static final int BORDER = Color.rgb(51, 65, 85);           // #334155 Slate 700
    private static final int BORDER_FOCUS = Color.rgb(77, 156, 255);   // Primary Blue Stroke

    // Button Fills (Ensure White text >= 4.5:1)
    private static final int BTN_PRIMARY_BG = Color.rgb(29, 78, 216);  // #1D4ED8 Deep Blue
    private static final int BTN_REJECT_BG = Color.rgb(127, 29, 29);   // #7F1D1D Deep Red

    // Foreground Text & Accent Tokens
    private static final int TEXT_PRIMARY = Color.rgb(248, 250, 252);  // #F8FAFC
    private static final int TEXT_MUTED = Color.rgb(203, 213, 225);    // #CBD5E1
    private static final int TEXT_DIM = Color.rgb(148, 163, 184);      // #94A3B8

    private static final int COLOR_BLUE = Color.rgb(77, 156, 255);     // #4D9CFF
    private static final int COLOR_CYAN = Color.rgb(35, 208, 243);     // #23D0F3
    private static final int COLOR_GREEN = Color.rgb(52, 211, 153);    // #34D399
    private static final int COLOR_AMBER = Color.rgb(251, 191, 36);    // #FBBF24
    private static final int COLOR_RED = Color.rgb(255, 107, 122);     // #FF6B7A
    private static final int COLOR_PURPLE = Color.rgb(192, 132, 252);  // #C084FC

    // State Variables
    private DashboardState dashboard = DashboardState.EMPTY;
    private boolean connected = false;
    private ActionListener actionListener;

    private String pendingAction = null;
    private String feedbackMessage = null;
    private boolean feedbackIsError = false;

    // Gesture detection
    private float touchStartX;
    private float touchStartY;
    private boolean swiping = false;

    // View Components
    private LinearLayout rootContainer;
    private LinearLayout headerView;
    private RobotHeadView robotHeadHeader;
    private LinearLayout usageHeaderRow;
    private TextView tagCodex;
    private TextView tvCodexHeader;
    private TextView tvUsageSeparator;
    private TextView tagAntigravity;
    private TextView tvAntigravityHeader;
    private ConnectionDotView dotConnection;
    private TextView tvConnection;

    private TextView tvFeedbackBanner;

    private LinearLayout emptyContainer;
    private RobotHeadView robotHeadEmpty;
    private TextView tvEmptyTitle;
    private TextView tvEmptySub1;
    private TextView tvEmptySub2;

    private FrameLayout mainContentWrapper;
    private LinearLayout portraitLayout;
    private LinearLayout landscapeLayout;

    // Projects Views
    private LinearLayout projectsCardPortrait;
    private LinearLayout projectsContainerPortrait;
    private LinearLayout projectsCardLandscape;
    private LinearLayout projectsContainerLandscape;

    // Status Workspace Views
    private ScrollView statusScrollViewPortrait;
    private LinearLayout statusCardPortrait;
    private ScrollView statusScrollViewLandscape;
    private LinearLayout statusCardLandscape;

    // Workspace fields (Portrait)
    private TextView tvStatusLabelP;
    private TextView tvStatusTitleP;
    private StatusMarkView statusMarkP;
    private TextView tvSubTitleP;
    private ActivityBarView activityBarP;
    private TextView tvMetric1ValP;
    private TextView tvMetric2ValP;
    private LinearLayout recentEventCardP;
    private LinearLayout eventListContainerP;

    // Workspace fields (Landscape)
    private TextView tvStatusLabelL;
    private TextView tvStatusTitleL;
    private StatusMarkView statusMarkL;
    private TextView tvSubTitleL;
    private ActivityBarView activityBarL;
    private TextView tvMetric1ValL;
    private TextView tvMetric2ValL;
    private LinearLayout recentEventCardL;
    private LinearLayout eventListContainerL;

    // Action Dock Buttons (Portrait & Landscape)
    private LinearLayout actionDockPortrait;
    private Button btnRejectP;
    private Button btnApproveP;

    private LinearLayout actionDockLandscape;
    private Button btnRejectL;
    private Button btnApproveL;

    private final Runnable feedbackHideRunnable = () -> {
        feedbackMessage = null;
        if (tvFeedbackBanner != null) {
            tvFeedbackBanner.setVisibility(GONE);
        }
    };

    public AgentDeckView(Context context) {
        super(context);
        setHapticFeedbackEnabled(false);
        setBackgroundColor(BG_DARK);
        buildUi(context);
    }

    public void setWindowInsets(int left, int top, int right, int bottom) {
        int padLeft = (int) Math.max(dp(12), left + dp(8));
        int padTop = (int) Math.max(dp(12), top + dp(8));
        int padRight = (int) Math.max(dp(12), right + dp(8));
        int padBottom = (int) Math.max(dp(12), bottom + dp(8));
        setPadding(padLeft, padTop, padRight, padBottom);
        requestLayout();
    }

    public void setActionListener(ActionListener listener) {
        this.actionListener = listener;
    }

    public void setDashboard(DashboardState value) {
        this.dashboard = value == null ? DashboardState.EMPTY : value;
        this.pendingAction = null;
        updateUiState();
    }

    public void setConnected(boolean value) {
        this.connected = value;
        if (value && feedbackIsError && feedbackMessage != null && feedbackMessage.contains("斷開")) {
            feedbackMessage = null;
            tvFeedbackBanner.setVisibility(GONE);
        }
        updateUiState();
    }

    public void setPendingAction(String action) {
        this.pendingAction = action;
        updateUiState();
    }

    public void clearPendingAction() {
        this.pendingAction = null;
        updateUiState();
    }

    public void showFeedback(boolean isError, String message) {
        this.feedbackIsError = isError;
        this.feedbackMessage = message;
        this.pendingAction = null;

        removeCallbacks(feedbackHideRunnable);
        if (message != null && !message.isBlank()) {
            tvFeedbackBanner.setVisibility(VISIBLE);
            tvFeedbackBanner.setText(message);
            int bg = isError ? Color.rgb(79, 14, 24) : Color.rgb(12, 59, 36);
            int stroke = isError ? COLOR_RED : COLOR_GREEN;
            int textCol = isError ? Color.rgb(254, 202, 202) : Color.rgb(187, 247, 208);
            tvFeedbackBanner.setBackground(createCardDrawable(bg, stroke, 8));
            tvFeedbackBanner.setTextColor(textCol);
            postDelayed(feedbackHideRunnable, 4000);
        } else {
            tvFeedbackBanner.setVisibility(GONE);
        }
        updateUiState();
    }

    private float fontScale() {
        return getResources().getConfiguration().fontScale;
    }

    private float dp(float val) {
        return val * getResources().getDisplayMetrics().density;
    }

    private GradientDrawable createCardDrawable(int fillColor, int strokeColor, float radiusDp) {
        GradientDrawable gd = new GradientDrawable();
        gd.setShape(GradientDrawable.RECTANGLE);
        gd.setColor(fillColor);
        gd.setCornerRadius(dp(radiusDp));
        if (strokeColor != 0) {
            gd.setStroke((int) Math.max(1f, dp(1.2f)), strokeColor);
        }
        return gd;
    }

    private TextView createNeonTag(Context context, String text) {
        TextView tag = new TextView(context);
        tag.setText(text);
        tag.setTextSize(TypedValue.COMPLEX_UNIT_SP, 10.5f);
        tag.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
        tag.setPadding((int) dp(7), (int) dp(2), (int) dp(7), (int) dp(2));
        int bgBadge = Color.rgb(39, 24, 56);
        int strokeBadge = COLOR_PURPLE;
        tag.setBackground(createCardDrawable(bgBadge, strokeBadge, 8));
        tag.setTextColor(Color.rgb(243, 232, 255));
        return tag;
    }

    private RippleDrawable createRippleButtonDrawable(int normalColor, int pressedColor, int strokeColor, float radiusDp) {
        GradientDrawable content = createCardDrawable(normalColor, strokeColor, radiusDp);
        ColorStateList rippleColor = ColorStateList.valueOf(pressedColor);
        GradientDrawable mask = createCardDrawable(Color.WHITE, 0, radiusDp);
        return new RippleDrawable(rippleColor, content, mask);
    }

    @Override
    public boolean performHapticFeedback(int feedbackConstant) {
        return false;
    }

    @Override
    public boolean performHapticFeedback(int feedbackConstant, int flags) {
        return false;
    }

    private void buildUi(Context context) {
        rootContainer = new LinearLayout(context);
        rootContainer.setOrientation(LinearLayout.VERTICAL);
        addView(rootContainer, new LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.MATCH_PARENT));

        // 1. Header View
        headerView = new LinearLayout(context);
        headerView.setOrientation(LinearLayout.HORIZONTAL);
        headerView.setGravity(Gravity.CENTER_VERTICAL);
        headerView.setPadding(0, 0, 0, (int) dp(8));

        robotHeadHeader = new RobotHeadView(context);
        robotHeadHeader.setColor(COLOR_BLUE);
        LinearLayout.LayoutParams lpRobotHead = new LinearLayout.LayoutParams((int) dp(24), (int) dp(24));
        lpRobotHead.rightMargin = (int) dp(8);
        headerView.addView(robotHeadHeader, lpRobotHead);

        usageHeaderRow = new LinearLayout(context);
        usageHeaderRow.setOrientation(LinearLayout.HORIZONTAL);
        usageHeaderRow.setGravity(Gravity.CENTER_VERTICAL);

        tagCodex = createNeonTag(context, "CODEX");
        LinearLayout.LayoutParams lpTagCodex = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        lpTagCodex.rightMargin = (int) dp(5);
        usageHeaderRow.addView(tagCodex, lpTagCodex);

        tvCodexHeader = new TextView(context);
        tvCodexHeader.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13f);
        tvCodexHeader.setTextColor(TEXT_PRIMARY);
        tvCodexHeader.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
        tvCodexHeader.setMaxLines(1);
        tvCodexHeader.setEllipsize(TextUtils.TruncateAt.END);
        usageHeaderRow.addView(tvCodexHeader, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        tvUsageSeparator = new TextView(context);
        tvUsageSeparator.setText(" · ");
        tvUsageSeparator.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13f);
        tvUsageSeparator.setTextColor(TEXT_DIM);
        tvUsageSeparator.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
        LinearLayout.LayoutParams lpSep = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        lpSep.leftMargin = (int) dp(2);
        lpSep.rightMargin = (int) dp(2);

        tagAntigravity = createNeonTag(context, "ANTIGRAVITY");
        LinearLayout.LayoutParams lpTagAG = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        lpTagAG.rightMargin = (int) dp(5);

        tvAntigravityHeader = new TextView(context);
        tvAntigravityHeader.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13f);
        tvAntigravityHeader.setTextColor(TEXT_MUTED);
        tvAntigravityHeader.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
        tvAntigravityHeader.setMaxLines(2);
        tvAntigravityHeader.setEllipsize(TextUtils.TruncateAt.END);

        usageHeaderRow.addView(tvUsageSeparator, lpSep);
        usageHeaderRow.addView(tagAntigravity, lpTagAG);
        usageHeaderRow.addView(tvAntigravityHeader, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        LinearLayout.LayoutParams lpUsageHead = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f);
        lpUsageHead.rightMargin = (int) dp(8);
        headerView.addView(usageHeaderRow, lpUsageHead);

        dotConnection = new ConnectionDotView(context);
        LinearLayout.LayoutParams lpDot = new LinearLayout.LayoutParams((int) dp(10), (int) dp(10));
        headerView.addView(dotConnection, lpDot);

        tvConnection = new TextView(context);
        tvConnection.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13f);
        tvConnection.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
        LinearLayout.LayoutParams lpConn = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        lpConn.leftMargin = (int) dp(6);
        headerView.addView(tvConnection, lpConn);

        rootContainer.addView(headerView, new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT));

        // Header bottom line
        View divider = new View(context);
        divider.setBackgroundColor(BORDER);
        rootContainer.addView(divider, new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, (int) Math.max(1f, dp(1))));

        // 2. Feedback Banner
        tvFeedbackBanner = new TextView(context);
        tvFeedbackBanner.setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f);
        tvFeedbackBanner.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
        tvFeedbackBanner.setGravity(Gravity.CENTER);
        tvFeedbackBanner.setMaxLines(2);
        tvFeedbackBanner.setEllipsize(TextUtils.TruncateAt.END);
        tvFeedbackBanner.setPadding((int) dp(12), (int) dp(6), (int) dp(12), (int) dp(6));
        tvFeedbackBanner.setVisibility(GONE);
        LinearLayout.LayoutParams lpBanner = new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT);
        lpBanner.topMargin = (int) dp(8);
        rootContainer.addView(tvFeedbackBanner, lpBanner);

        // 3. Main Content Wrapper
        mainContentWrapper = new FrameLayout(context);
        mainContentWrapper.setClipChildren(true);
        mainContentWrapper.setClipToPadding(true);
        LinearLayout.LayoutParams lpWrapper = new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, 0, 1f);
        lpWrapper.topMargin = (int) dp(8);
        rootContainer.addView(mainContentWrapper, lpWrapper);

        buildEmptyStateUi(context);
        buildWorkspaceUi(context);
    }

    private void buildEmptyStateUi(Context context) {
        emptyContainer = new LinearLayout(context);
        emptyContainer.setOrientation(LinearLayout.VERTICAL);
        emptyContainer.setGravity(Gravity.CENTER);
        emptyContainer.setPadding((int) dp(24), (int) dp(32), (int) dp(24), (int) dp(32));
        emptyContainer.setBackground(createCardDrawable(SURFACE, BORDER, 16));

        robotHeadEmpty = new RobotHeadView(context);
        robotHeadEmpty.setColor(COLOR_BLUE);
        LinearLayout.LayoutParams lpH = new LinearLayout.LayoutParams((int) dp(64), (int) dp(64));
        emptyContainer.addView(robotHeadEmpty, lpH);

        tvEmptyTitle = new TextView(context);
        tvEmptyTitle.setText("等待本機 Bridge 連線");
        tvEmptyTitle.setTextSize(TypedValue.COMPLEX_UNIT_SP, 22f);
        tvEmptyTitle.setTextColor(TEXT_PRIMARY);
        tvEmptyTitle.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
        tvEmptyTitle.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams lpT = new LinearLayout.LayoutParams(LayoutParams.WRAP_CONTENT, LayoutParams.WRAP_CONTENT);
        lpT.topMargin = (int) dp(16);
        emptyContainer.addView(tvEmptyTitle, lpT);

        tvEmptySub1 = new TextView(context);
        tvEmptySub1.setText("請於電腦上執行 Start-AgentDeckAndroid.ps1");
        tvEmptySub1.setTextSize(TypedValue.COMPLEX_UNIT_SP, 14f);
        tvEmptySub1.setTextColor(TEXT_MUTED);
        tvEmptySub1.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams lpS1 = new LinearLayout.LayoutParams(LayoutParams.WRAP_CONTENT, LayoutParams.WRAP_CONTENT);
        lpS1.topMargin = (int) dp(12);
        emptyContainer.addView(tvEmptySub1, lpS1);

        tvEmptySub2 = new TextView(context);
        tvEmptySub2.setText("手機將透過 USB 自動同步 AI Agent 儀表板");
        tvEmptySub2.setTextSize(TypedValue.COMPLEX_UNIT_SP, 14f);
        tvEmptySub2.setTextColor(TEXT_MUTED);
        tvEmptySub2.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams lpS2 = new LinearLayout.LayoutParams(LayoutParams.WRAP_CONTENT, LayoutParams.WRAP_CONTENT);
        lpS2.topMargin = (int) dp(6);
        emptyContainer.addView(tvEmptySub2, lpS2);

        FrameLayout.LayoutParams lpEmpty = new FrameLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT);
        lpEmpty.gravity = Gravity.CENTER;
        mainContentWrapper.addView(emptyContainer, lpEmpty);
    }

    private void buildWorkspaceUi(Context context) {
        // --- PORTRAIT LAYOUT ---
        portraitLayout = new LinearLayout(context);
        portraitLayout.setOrientation(LinearLayout.VERTICAL);
        portraitLayout.setClipChildren(true);
        portraitLayout.setClipToPadding(true);

        // Projects Section (Portrait)
        projectsCardPortrait = new LinearLayout(context);
        projectsCardPortrait.setOrientation(LinearLayout.VERTICAL);
        projectsCardPortrait.setPadding((int) dp(12), (int) dp(10), (int) dp(12), (int) dp(10));
        projectsCardPortrait.setBackground(createCardDrawable(SURFACE_ALT, BORDER, 12));
        projectsCardPortrait.setClipChildren(true);
        projectsCardPortrait.setClipToPadding(true);

        TextView tvProjHeadP = new TextView(context);
        tvProjHeadP.setText("專案列表");
        tvProjHeadP.setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f);
        tvProjHeadP.setTextColor(TEXT_DIM);
        tvProjHeadP.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
        projectsCardPortrait.addView(tvProjHeadP);

        projectsContainerPortrait = new LinearLayout(context);
        projectsContainerPortrait.setOrientation(LinearLayout.VERTICAL);
        LinearLayout.LayoutParams lpProjContP = new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT);
        lpProjContP.topMargin = (int) dp(6);
        projectsCardPortrait.addView(projectsContainerPortrait, lpProjContP);

        portraitLayout.addView(projectsCardPortrait, new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT));

        // Status ScrollView (Portrait)
        statusScrollViewPortrait = new ScrollView(context);
        statusScrollViewPortrait.setFillViewport(true);
        statusScrollViewPortrait.setClipToPadding(true);
        statusScrollViewPortrait.setClipChildren(true);
        statusScrollViewPortrait.setVerticalScrollBarEnabled(false);

        statusCardPortrait = new LinearLayout(context);
        statusCardPortrait.setOrientation(LinearLayout.VERTICAL);
        statusCardPortrait.setPadding((int) dp(16), (int) dp(16), (int) dp(16), (int) dp(16));
        statusCardPortrait.setBackground(createCardDrawable(SURFACE, BORDER, 16));
        statusCardPortrait.setClipChildren(true);
        statusCardPortrait.setClipToPadding(true);

        // Workspace fields (Portrait)
        tvStatusLabelP = new TextView(context);
        tvStatusLabelP.setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f);
        tvStatusLabelP.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);

        tvStatusTitleP = new TextView(context);
        tvStatusTitleP.setTextSize(TypedValue.COMPLEX_UNIT_SP, 20f);
        tvStatusTitleP.setTextColor(TEXT_PRIMARY);
        tvStatusTitleP.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
        tvStatusTitleP.setMaxLines(1);
        tvStatusTitleP.setEllipsize(TextUtils.TruncateAt.END);

        statusMarkP = new StatusMarkView(context);

        LinearLayout topHeaderColP = new LinearLayout(context);
        topHeaderColP.setOrientation(LinearLayout.VERTICAL);
        topHeaderColP.addView(tvStatusLabelP);

        LinearLayout statusMainRowP = new LinearLayout(context);
        statusMainRowP.setOrientation(LinearLayout.HORIZONTAL);
        statusMainRowP.setGravity(Gravity.CENTER_VERTICAL);

        LinearLayout.LayoutParams lpTitleP = new LinearLayout.LayoutParams(LayoutParams.WRAP_CONTENT, LayoutParams.WRAP_CONTENT);
        lpTitleP.rightMargin = (int) dp(4);
        statusMainRowP.addView(tvStatusTitleP, lpTitleP);

        tvMetric1ValP = createMetricCard(context, statusMainRowP, "經過時間", COLOR_CYAN);
        tvMetric2ValP = createMetricCard(context, statusMainRowP, "對話 Token", COLOR_BLUE);

        LinearLayout.LayoutParams lpMarkP = new LinearLayout.LayoutParams((int) dp(36), (int) dp(36));
        lpMarkP.leftMargin = (int) dp(4);
        statusMainRowP.addView(statusMarkP, lpMarkP);

        LinearLayout.LayoutParams lpMainRowP = new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT);
        lpMainRowP.topMargin = (int) dp(2);
        topHeaderColP.addView(statusMainRowP, lpMainRowP);

        statusCardPortrait.addView(topHeaderColP);

        tvSubTitleP = new TextView(context);
        tvSubTitleP.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13f);
        tvSubTitleP.setTextColor(TEXT_MUTED);
        tvSubTitleP.setMaxLines(2);
        tvSubTitleP.setEllipsize(TextUtils.TruncateAt.END);
        LinearLayout.LayoutParams lpSubP = new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT);
        lpSubP.topMargin = (int) dp(6);
        statusCardPortrait.addView(tvSubTitleP, lpSubP);

        activityBarP = new ActivityBarView(context);
        LinearLayout.LayoutParams lpBarP = new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT);
        lpBarP.topMargin = (int) dp(10);
        statusCardPortrait.addView(activityBarP, lpBarP);

        // Recent Event (Portrait) - Placed directly under step progress bar with vertical scroll
        recentEventCardP = new LinearLayout(context);
        recentEventCardP.setOrientation(LinearLayout.VERTICAL);
        recentEventCardP.setPadding((int) dp(14), (int) dp(12), (int) dp(14), (int) dp(12));
        recentEventCardP.setBackground(createCardDrawable(SURFACE_ALT, BORDER, 10));

        LinearLayout evHeaderRowP = new LinearLayout(context);
        evHeaderRowP.setOrientation(LinearLayout.HORIZONTAL);
        evHeaderRowP.setGravity(Gravity.CENTER_VERTICAL);

        TextView tvEvTitleP = new TextView(context);
        tvEvTitleP.setText("最新事件");
        tvEvTitleP.setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f);
        tvEvTitleP.setTextColor(TEXT_DIM);
        tvEvTitleP.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
        evHeaderRowP.addView(tvEvTitleP, new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT));

        recentEventCardP.addView(evHeaderRowP, new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT));

        ScrollView eventScrollViewP = new ScrollView(context);
        eventScrollViewP.setVerticalScrollBarEnabled(true);

        eventListContainerP = new LinearLayout(context);
        eventListContainerP.setOrientation(LinearLayout.VERTICAL);

        eventScrollViewP.addView(eventListContainerP, new FrameLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT));

        LinearLayout.LayoutParams lpEvScrollP = new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, (int) dp(120));
        lpEvScrollP.topMargin = (int) dp(6);
        recentEventCardP.addView(eventScrollViewP, lpEvScrollP);

        LinearLayout.LayoutParams lpEvCardP = new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT);
        lpEvCardP.topMargin = (int) dp(14);
        statusCardPortrait.addView(recentEventCardP, lpEvCardP);

        statusScrollViewPortrait.addView(statusCardPortrait, new FrameLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT));

        LinearLayout.LayoutParams lpScrollP = new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, 0, 1f);
        lpScrollP.topMargin = (int) dp(10);
        portraitLayout.addView(statusScrollViewPortrait, lpScrollP);

        // Fixed Action Dock (Portrait)
        actionDockPortrait = new LinearLayout(context);
        actionDockPortrait.setOrientation(LinearLayout.HORIZONTAL);
        btnRejectP = createActionButton(context, actionDockPortrait, BTN_REJECT_BG, Color.WHITE);
        btnApproveP = createActionButton(context, actionDockPortrait, BTN_PRIMARY_BG, Color.WHITE);
        LinearLayout.LayoutParams lpDockP = new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT);
        lpDockP.topMargin = (int) dp(8);
        portraitLayout.addView(actionDockPortrait, lpDockP);

        mainContentWrapper.addView(portraitLayout, new FrameLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.MATCH_PARENT));

        // --- LANDSCAPE LAYOUT ---
        landscapeLayout = new LinearLayout(context);
        landscapeLayout.setOrientation(LinearLayout.HORIZONTAL);
        landscapeLayout.setClipChildren(true);
        landscapeLayout.setClipToPadding(true);

        // Left Rail Projects (Landscape)
        ScrollView leftRailScrollView = new ScrollView(context);
        leftRailScrollView.setVerticalScrollBarEnabled(false);
        leftRailScrollView.setClipChildren(true);
        leftRailScrollView.setClipToPadding(true);

        projectsCardLandscape = new LinearLayout(context);
        projectsCardLandscape.setOrientation(LinearLayout.VERTICAL);
        projectsCardLandscape.setPadding((int) dp(12), (int) dp(10), (int) dp(12), (int) dp(10));
        projectsCardLandscape.setBackground(createCardDrawable(SURFACE_ALT, BORDER, 12));
        projectsCardLandscape.setClipChildren(true);
        projectsCardLandscape.setClipToPadding(true);

        TextView tvProjHeadL = new TextView(context);
        tvProjHeadL.setText("專案列表");
        tvProjHeadL.setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f);
        tvProjHeadL.setTextColor(TEXT_DIM);
        tvProjHeadL.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
        projectsCardLandscape.addView(tvProjHeadL);

        projectsContainerLandscape = new LinearLayout(context);
        projectsContainerLandscape.setOrientation(LinearLayout.VERTICAL);
        LinearLayout.LayoutParams lpProjContL = new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT);
        lpProjContL.topMargin = (int) dp(6);
        projectsCardLandscape.addView(projectsContainerLandscape, lpProjContL);

        leftRailScrollView.addView(projectsCardLandscape, new FrameLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT));
        LinearLayout.LayoutParams lpLeftRail = new LinearLayout.LayoutParams(0, LayoutParams.MATCH_PARENT, 0.32f);
        lpLeftRail.rightMargin = (int) dp(12);
        landscapeLayout.addView(leftRailScrollView, lpLeftRail);

        // Right Workspace (Landscape)
        LinearLayout rightWorkspace = new LinearLayout(context);
        rightWorkspace.setOrientation(LinearLayout.VERTICAL);
        rightWorkspace.setClipChildren(true);
        rightWorkspace.setClipToPadding(true);

        statusScrollViewLandscape = new ScrollView(context);
        statusScrollViewLandscape.setFillViewport(true);
        statusScrollViewLandscape.setClipToPadding(true);
        statusScrollViewLandscape.setClipChildren(true);
        statusScrollViewLandscape.setVerticalScrollBarEnabled(false);

        statusCardLandscape = new LinearLayout(context);
        statusCardLandscape.setOrientation(LinearLayout.VERTICAL);
        statusCardLandscape.setPadding((int) dp(16), (int) dp(16), (int) dp(16), (int) dp(16));
        statusCardLandscape.setBackground(createCardDrawable(SURFACE, BORDER, 16));
        statusCardLandscape.setClipChildren(true);
        statusCardLandscape.setClipToPadding(true);

        // Workspace fields (Landscape)
        tvStatusLabelL = new TextView(context);
        tvStatusLabelL.setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f);
        tvStatusLabelL.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);

        tvStatusTitleL = new TextView(context);
        tvStatusTitleL.setTextSize(TypedValue.COMPLEX_UNIT_SP, 20f);
        tvStatusTitleL.setTextColor(TEXT_PRIMARY);
        tvStatusTitleL.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
        tvStatusTitleL.setMaxLines(1);
        tvStatusTitleL.setEllipsize(TextUtils.TruncateAt.END);

        statusMarkL = new StatusMarkView(context);

        LinearLayout topHeaderColL = new LinearLayout(context);
        topHeaderColL.setOrientation(LinearLayout.VERTICAL);
        topHeaderColL.addView(tvStatusLabelL);

        LinearLayout statusMainRowL = new LinearLayout(context);
        statusMainRowL.setOrientation(LinearLayout.HORIZONTAL);
        statusMainRowL.setGravity(Gravity.CENTER_VERTICAL);

        LinearLayout.LayoutParams lpTitleL = new LinearLayout.LayoutParams(LayoutParams.WRAP_CONTENT, LayoutParams.WRAP_CONTENT);
        lpTitleL.rightMargin = (int) dp(4);
        statusMainRowL.addView(tvStatusTitleL, lpTitleL);

        tvMetric1ValL = createMetricCard(context, statusMainRowL, "經過時間", COLOR_CYAN);
        tvMetric2ValL = createMetricCard(context, statusMainRowL, "對話 Token", COLOR_BLUE);

        LinearLayout.LayoutParams lpMarkL = new LinearLayout.LayoutParams((int) dp(36), (int) dp(36));
        lpMarkL.leftMargin = (int) dp(4);
        statusMainRowL.addView(statusMarkL, lpMarkL);

        LinearLayout.LayoutParams lpMainRowL = new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT);
        lpMainRowL.topMargin = (int) dp(2);
        topHeaderColL.addView(statusMainRowL, lpMainRowL);

        statusCardLandscape.addView(topHeaderColL);

        tvSubTitleL = new TextView(context);
        tvSubTitleL.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13f);
        tvSubTitleL.setTextColor(TEXT_MUTED);
        tvSubTitleL.setMaxLines(2);
        tvSubTitleL.setEllipsize(TextUtils.TruncateAt.END);
        LinearLayout.LayoutParams lpSubL = new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT);
        lpSubL.topMargin = (int) dp(6);
        statusCardLandscape.addView(tvSubTitleL, lpSubL);

        activityBarL = new ActivityBarView(context);
        LinearLayout.LayoutParams lpBarL = new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT);
        lpBarL.topMargin = (int) dp(10);
        statusCardLandscape.addView(activityBarL, lpBarL);

        // Recent Event (Landscape)
        recentEventCardL = new LinearLayout(context);
        recentEventCardL.setOrientation(LinearLayout.VERTICAL);
        recentEventCardL.setPadding((int) dp(14), (int) dp(12), (int) dp(14), (int) dp(12));
        recentEventCardL.setBackground(createCardDrawable(SURFACE_ALT, BORDER, 10));

        LinearLayout evHeaderRowL = new LinearLayout(context);
        evHeaderRowL.setOrientation(LinearLayout.HORIZONTAL);
        evHeaderRowL.setGravity(Gravity.CENTER_VERTICAL);

        TextView tvEvTitleL = new TextView(context);
        tvEvTitleL.setText("最新事件");
        tvEvTitleL.setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f);
        tvEvTitleL.setTextColor(TEXT_DIM);
        tvEvTitleL.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
        evHeaderRowL.addView(tvEvTitleL, new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT));

        recentEventCardL.addView(evHeaderRowL, new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT));

        ScrollView eventScrollViewL = new ScrollView(context);
        eventScrollViewL.setVerticalScrollBarEnabled(true);

        eventListContainerL = new LinearLayout(context);
        eventListContainerL.setOrientation(LinearLayout.VERTICAL);

        eventScrollViewL.addView(eventListContainerL, new FrameLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT));

        LinearLayout.LayoutParams lpEvScrollL = new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, (int) dp(120));
        lpEvScrollL.topMargin = (int) dp(6);
        recentEventCardL.addView(eventScrollViewL, lpEvScrollL);

        LinearLayout.LayoutParams lpEvCardL = new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT);
        lpEvCardL.topMargin = (int) dp(14);
        statusCardLandscape.addView(recentEventCardL, lpEvCardL);

        statusScrollViewLandscape.addView(statusCardLandscape, new FrameLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT));
        rightWorkspace.addView(statusScrollViewLandscape, new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, 0, 1f));

        // Fixed Action Dock (Landscape)
        actionDockLandscape = new LinearLayout(context);
        actionDockLandscape.setOrientation(LinearLayout.HORIZONTAL);
        btnRejectL = createActionButton(context, actionDockLandscape, BTN_REJECT_BG, Color.WHITE);
        btnApproveL = createActionButton(context, actionDockLandscape, BTN_PRIMARY_BG, Color.WHITE);
        LinearLayout.LayoutParams lpDockL = new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT);
        lpDockL.topMargin = (int) dp(8);
        rightWorkspace.addView(actionDockLandscape, lpDockL);

        landscapeLayout.addView(rightWorkspace, new LinearLayout.LayoutParams(0, LayoutParams.MATCH_PARENT, 0.68f));

        mainContentWrapper.addView(landscapeLayout, new FrameLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.MATCH_PARENT));

        // Setup Button OnClick Listeners
        View.OnClickListener onReject = v -> {
            setPendingAction("reject");
            if (actionListener != null) {
                actionListener.onAction("reject", dashboard.current != null ? dashboard.current.targetId : null);
            }
        };
        btnRejectP.setOnClickListener(onReject);
        btnRejectL.setOnClickListener(onReject);

        View.OnClickListener onApprove = v -> {
            setPendingAction("approve");
            if (actionListener != null) {
                actionListener.onAction("approve", dashboard.current != null ? dashboard.current.targetId : null);
            }
        };
        btnApproveP.setOnClickListener(onApprove);
        btnApproveL.setOnClickListener(onApprove);
    }

    private TextView createMetricCard(Context context, LinearLayout row, String labelText, int accentColor) {
        LinearLayout card = new LinearLayout(context);
        card.setOrientation(LinearLayout.VERTICAL);
        card.setGravity(Gravity.CENTER);
        card.setPadding((int) dp(6), (int) dp(5), (int) dp(6), (int) dp(5));
        card.setBackground(createCardDrawable(SURFACE_ALT, BORDER, 8));

        TextView tvL = new TextView(context);
        tvL.setText(labelText);
        tvL.setTextSize(TypedValue.COMPLEX_UNIT_SP, 10f);
        tvL.setTextColor(TEXT_MUTED);
        tvL.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
        tvL.setGravity(Gravity.CENTER);
        card.addView(tvL);

        TextView tvV = new TextView(context);
        tvV.setTextSize(TypedValue.COMPLEX_UNIT_SP, 14f);
        tvV.setTextColor(accentColor);
        tvV.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
        tvV.setGravity(Gravity.CENTER);
        tvV.setMaxLines(1);
        tvV.setEllipsize(TextUtils.TruncateAt.END);
        LinearLayout.LayoutParams lpV = new LinearLayout.LayoutParams(LayoutParams.WRAP_CONTENT, LayoutParams.WRAP_CONTENT);
        lpV.topMargin = (int) dp(2);
        card.addView(tvV, lpV);

        LinearLayout.LayoutParams lpCard = new LinearLayout.LayoutParams(0, LayoutParams.WRAP_CONTENT, 1f);
        lpCard.rightMargin = (int) dp(3);
        lpCard.leftMargin = (int) dp(3);
        row.addView(card, lpCard);
        return tvV;
    }

    private Button createActionButton(Context context, LinearLayout dock, int normalBg, int textColor) {
        Button btn = new Button(context);
        btn.setHapticFeedbackEnabled(false);
        btn.setTextSize(TypedValue.COMPLEX_UNIT_SP, 15f);
        btn.setTextColor(textColor);
        btn.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
        btn.setMinimumHeight((int) dp(48));
        btn.setBackground(createRippleButtonDrawable(normalBg, Color.argb(60, 255, 255, 255), normalBg, 12));

        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(0, (int) dp(48), 1f);
        lp.leftMargin = (int) dp(4);
        lp.rightMargin = (int) dp(4);
        dock.addView(btn, lp);
        return btn;
    }

    private void updateUiState() {
        // Header
        tvConnection.setText(connected ? "本機在線" : "等待 Bridge");
        tvConnection.setTextColor(connected ? COLOR_GREEN : TEXT_MUTED);
        dotConnection.setConnected(connected);

        boolean isLandscape = getWidth() > getHeight() || getResources().getConfiguration().orientation == android.content.res.Configuration.ORIENTATION_LANDSCAPE;
        boolean compactUsage = !isLandscape;

        if (dashboard.current != null && dashboard.current.codexUsage != null) {
            tvCodexHeader.setText(LayoutPolicyHelper.formatCodexHeader(dashboard.current.codexUsage, compactUsage));
        } else {
            tvCodexHeader.setText(LayoutPolicyHelper.formatCodexHeader(null, compactUsage));
        }

        if (dashboard.current != null && dashboard.current.antigravityUsage != null) {
            tvAntigravityHeader.setText(LayoutPolicyHelper.formatAntigravityHeader(dashboard.current.antigravityUsage, compactUsage));
        } else {
            tvAntigravityHeader.setText(LayoutPolicyHelper.formatAntigravityHeader(null, compactUsage));
        }

        if (dashboard.current == null) {
            emptyContainer.setVisibility(VISIBLE);
            portraitLayout.setVisibility(GONE);
            landscapeLayout.setVisibility(GONE);
            return;
        }

        emptyContainer.setVisibility(GONE);
        portraitLayout.setVisibility(isLandscape ? GONE : VISIBLE);
        landscapeLayout.setVisibility(isLandscape ? VISIBLE : GONE);

        DashboardState.AgentState state = dashboard.current;

        // Update Projects Cards
        updateProjectsContainer(projectsContainerPortrait);
        updateProjectsContainer(projectsContainerLandscape);

        // Update Workspace details
        updateWorkspace(state, tvStatusLabelP, tvStatusTitleP, statusMarkP, tvSubTitleP, activityBarP,
                tvMetric1ValP, tvMetric2ValP, recentEventCardP, eventListContainerP);
        updateWorkspace(state, tvStatusLabelL, tvStatusTitleL, statusMarkL, tvSubTitleL, activityBarL,
                tvMetric1ValL, tvMetric2ValL, recentEventCardL, eventListContainerL);

        // Update Action Dock Buttons
        updateActionDock(state, actionDockPortrait, btnRejectP, btnApproveP);
        updateActionDock(state, actionDockLandscape, btnRejectL, btnApproveL);
    }

    private void updateProjectsContainer(LinearLayout container) {
        container.removeAllViews();
        List<DashboardState.AgentState> projects = dashboard.projects;
        int count = Math.min(projects.size(), 4);
        for (int i = 0; i < count; i++) {
            DashboardState.AgentState p = projects.get(i);
            boolean selected = dashboard.current != null && dashboard.current.eventId.equals(p.eventId);

            LinearLayout card = new LinearLayout(getContext());
            card.setOrientation(LinearLayout.HORIZONTAL);
            card.setGravity(Gravity.CENTER_VERTICAL);
            card.setPadding((int) dp(10), (int) dp(8), (int) dp(10), (int) dp(8));
            card.setMinimumHeight((int) dp(48));

            card.setHapticFeedbackEnabled(false);
            card.setFocusable(true);
            card.setClickable(true);
            card.setSelected(selected);

            int bg = selected ? SURFACE_SELECTED : SURFACE;
            int stroke = selected ? BORDER_FOCUS : BORDER;
            card.setBackground(createRippleButtonDrawable(bg, Color.argb(40, 255, 255, 255), stroke, 10));

            String conversation = p.conversation == null ? "" : p.conversation.trim();
            boolean hasConversation = !conversation.isEmpty();
            card.setContentDescription("專案：" + p.project
                    + (hasConversation ? "，對話：" + conversation : "")
                    + "，狀態：" + p.statusLabel()
                    + (selected ? "，已選取" : "，未選取"));

            card.setOnClickListener(v -> {
                setPendingAction("select_project");
                if (actionListener != null) {
                    actionListener.onAction("select_project", p.eventId);
                }
            });

            View dot = new View(getContext());
            dot.setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_NO);
            int accent = statusColor(p);
            GradientDrawable gdDot = new GradientDrawable();
            gdDot.setShape(GradientDrawable.OVAL);
            gdDot.setColor(accent);
            dot.setBackground(gdDot);
            card.addView(dot, new LinearLayout.LayoutParams((int) dp(9), (int) dp(9)));

            LinearLayout col = new LinearLayout(getContext());
            col.setOrientation(LinearLayout.VERTICAL);
            LinearLayout.LayoutParams lpCol = new LinearLayout.LayoutParams(0, LayoutParams.WRAP_CONTENT, 1f);
            lpCol.leftMargin = (int) dp(10);

            TextView tvName = new TextView(getContext());
            tvName.setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_NO);
            tvName.setText(p.project);
            tvName.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13f);
            tvName.setTextColor(selected ? COLOR_CYAN : TEXT_PRIMARY);
            tvName.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
            tvName.setMaxLines(1);
            tvName.setEllipsize(TextUtils.TruncateAt.END);
            col.addView(tvName);

            TextView tvStat = new TextView(getContext());
            tvStat.setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_NO);
            tvStat.setText(conversation);
            tvStat.setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f);
            tvStat.setTextColor(TEXT_MUTED);
            tvStat.setMaxLines(1);
            tvStat.setEllipsize(TextUtils.TruncateAt.END);
            tvStat.setVisibility(hasConversation ? VISIBLE : GONE);
            col.addView(tvStat);

            card.addView(col, lpCol);

            LinearLayout.LayoutParams lpCard = new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT);
            lpCard.topMargin = (int) dp(4);
            container.addView(card, lpCard);
        }
    }

    private void updateWorkspace(DashboardState.AgentState state,
                                 TextView tvLabel, TextView tvTitle, StatusMarkView statusMark,
                                 TextView tvSub, ActivityBarView activityBar,
                                 TextView tvM1, TextView tvM2,
                                 LinearLayout recentCard, LinearLayout eventListContainer) {
        int accent = statusColor(state);
        tvLabel.setText(LayoutPolicyHelper.formatAgentStatusLabel(state.models));
        tvLabel.setMaxLines(2);
        tvLabel.setEllipsize(android.text.TextUtils.TruncateAt.END);
        tvLabel.setTextColor(accent);

        tvTitle.setText(state.statusLabel());
        statusMark.setState(state);

        String sub = (state.conversation != null && !state.conversation.isBlank()) ? state.conversation : (state.project != null ? state.project : "");
        tvSub.setText(sub);

        tvM1.setText(state.elapsedLabel());
        tvM2.setText(TokenFormatter.formatCompact(state.conversationTokens));
        if (tvM2.getParent() instanceof View) {
            ((View) tvM2.getParent()).setContentDescription("對話 Token：" + TokenFormatter.formatFull(state.conversationTokens));
        }

        boolean isCompleted = "completed".equals(state.status);

        activityBar.setVisibility(isCompleted ? GONE : VISIBLE);
        recentCard.setVisibility(VISIBLE);

        if (!isCompleted) {
            activityBar.setState(state);
        }
        updateRecentEvents(state, eventListContainer);
    }

    private void updateRecentEvents(DashboardState.AgentState state, LinearLayout container) {
        container.removeAllViews();
        List<DashboardState.RecentEvent> events = state.recentEvents;
        if (events == null || events.isEmpty()) {
            if (state.message != null && !state.message.isBlank()) {
                events = java.util.Collections.singletonList(new DashboardState.RecentEvent("status", state.message, null));
            } else {
                events = java.util.Collections.emptyList();
            }
        }

        Context context = container.getContext();
        for (int i = 0; i < events.size(); i++) {
            DashboardState.RecentEvent ev = events.get(i);

            LinearLayout itemLayout = new LinearLayout(context);
            itemLayout.setOrientation(LinearLayout.VERTICAL);
            if (i > 0) {
                LinearLayout.LayoutParams lpItem = new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT);
                lpItem.topMargin = (int) dp(10);
                itemLayout.setLayoutParams(lpItem);
            }

            LinearLayout headerRow = new LinearLayout(context);
            headerRow.setOrientation(LinearLayout.HORIZONTAL);
            headerRow.setGravity(Gravity.CENTER_VERTICAL);

            EventGlyphView glyphView = new EventGlyphView(context);
            glyphView.setKind(ev.kind);
            LinearLayout.LayoutParams lpIcon = new LinearLayout.LayoutParams((int) dp(15), (int) dp(15));
            headerRow.addView(glyphView, lpIcon);

            TextView tvLabel = new TextView(context);
            tvLabel.setText(ev.label);
            tvLabel.setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f);
            tvLabel.setTextColor(TEXT_DIM);
            tvLabel.setSingleLine(true);
            tvLabel.setEllipsize(TextUtils.TruncateAt.END);
            LinearLayout.LayoutParams lpLabel = new LinearLayout.LayoutParams(0, LayoutParams.WRAP_CONTENT, 1f);
            lpLabel.leftMargin = (int) dp(8);
            headerRow.addView(tvLabel, lpLabel);

            itemLayout.addView(headerRow, new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT));

            if (ev.content != null && !ev.content.isBlank()) {
                TextView tvContent = new TextView(context);
                tvContent.setText(ev.content);
                tvContent.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13f);
                tvContent.setTextColor(TEXT_PRIMARY);
                tvContent.setLineSpacing(dp(2), 1.1f);
                LinearLayout.LayoutParams lpContent = new LinearLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT);
                lpContent.topMargin = (int) dp(4);
                lpContent.leftMargin = (int) dp(24);
                itemLayout.addView(tvContent, lpContent);
            }

            container.addView(itemLayout);
        }
    }

    private void updateActionDock(DashboardState.AgentState state, LinearLayout dockView, Button btnReject, Button btnApprove) {
        boolean isPending = pendingAction != null;
        float alpha = isPending ? 0.6f : 1.0f;

        if (state.requiresAction) {
            dockView.setVisibility(VISIBLE);
            btnReject.setVisibility(VISIBLE);
            btnApprove.setVisibility(VISIBLE);

            boolean isRejectPending = "reject".equals(pendingAction);
            boolean isApprovePending = "approve".equals(pendingAction);

            btnReject.setText(LayoutPolicyHelper.formatButtonText("reject", fontScale(), isRejectPending));
            btnApprove.setText(LayoutPolicyHelper.formatButtonText("approve", fontScale(), isApprovePending));

            btnReject.setEnabled(!isPending);
            btnApprove.setEnabled(!isPending);
            btnReject.setAlpha(alpha);
            btnApprove.setAlpha(alpha);
        } else {
            dockView.setVisibility(GONE);
            btnReject.setVisibility(GONE);
            btnApprove.setVisibility(GONE);
        }
    }

    private int statusColor(DashboardState.AgentState state) {
        if (state == null) return COLOR_BLUE;
        if (state.requiresAction) return COLOR_AMBER;
        switch (state.status) {
            case "completed": return COLOR_GREEN;
            case "error": return COLOR_RED;
            case "waiting": return COLOR_AMBER;
            case "working": return COLOR_CYAN;
            default: return COLOR_BLUE;
        }
    }

    @Override
    protected void onLayout(boolean changed, int left, int top, int right, int bottom) {
        super.onLayout(changed, left, top, right, bottom);
        if (changed) {
            updateUiState();
        }
    }

    @Override
    public boolean performClick() {
        super.performClick();
        return true;
    }

    // Touch & Gesture Events
    @Override
    public boolean onInterceptTouchEvent(MotionEvent ev) {
        int action = ev.getActionMasked();
        if (action == MotionEvent.ACTION_DOWN) {
            touchStartX = ev.getX();
            touchStartY = ev.getY();
            swiping = false;
        } else if (action == MotionEvent.ACTION_MOVE) {
            SwipeGestureHelper.Direction dir = SwipeGestureHelper.detectSwipe(
                    touchStartX, touchStartY, ev.getX(), ev.getY(), dp(48), 1.5f
            );
            if (dir != null) {
                swiping = true;
                return true; // Intercept horizontal swipe!
            }
        }
        return super.onInterceptTouchEvent(ev);
    }

    @Override
    public boolean onTouchEvent(MotionEvent ev) {
        int action = ev.getActionMasked();
        if (action == MotionEvent.ACTION_MOVE) {
            if (!swiping) {
                SwipeGestureHelper.Direction dir = SwipeGestureHelper.detectSwipe(
                        touchStartX, touchStartY, ev.getX(), ev.getY(), dp(48), 1.5f
                );
                if (dir != null) {
                    swiping = true;
                }
            }
        }
        if (action == MotionEvent.ACTION_UP) {
            if (swiping && pendingAction == null && actionListener != null) {
                SwipeGestureHelper.Direction dir = SwipeGestureHelper.detectSwipe(
                        touchStartX, touchStartY, ev.getX(), ev.getY(), dp(48), 1.5f
                );
                if (dir != null) {
                    setPendingAction(dir.actionName);
                    actionListener.onAction(dir.actionName, null);
                }
            }
            swiping = false;
            performClick();
            return true;
        } else if (action == MotionEvent.ACTION_CANCEL) {
            swiping = false;
            return true;
        }
        return super.onTouchEvent(ev);
    }

    // Pure Decorative Views (importantForAccessibility = NO)
    private static class EventGlyphView extends View {
        private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Path path = new Path();
        private final RectF rect = new RectF();
        private String kind = "status";

        public EventGlyphView(Context context) {
            super(context);
            setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_NO);
        }

        public void setKind(String kind) {
            this.kind = kind != null ? kind : "status";
            invalidate();
        }

        @Override
        protected void onDraw(Canvas canvas) {
            super.onDraw(canvas);
            float w = getWidth();
            float h = getHeight();
            if (w <= 0 || h <= 0) return;
            float pad = Math.min(w, h) * 0.1f;
            float cx = w / 2f;
            float cy = h / 2f;

            paint.setColor(TEXT_DIM);
            paint.setStyle(Paint.Style.STROKE);
            paint.setStrokeWidth(TypedValue.applyDimension(TypedValue.COMPLEX_UNIT_DIP, 1.2f, getResources().getDisplayMetrics()));
            paint.setStrokeCap(Paint.Cap.ROUND);
            paint.setStrokeJoin(Paint.Join.ROUND);

            if ("command".equals(kind)) {
                rect.set(pad, pad, w - pad, h - pad);
                canvas.drawRoundRect(rect, pad * 1.5f, pad * 1.5f, paint);
                path.reset();
                path.moveTo(pad + w * 0.22f, cy - h * 0.18f);
                path.lineTo(pad + w * 0.42f, cy);
                path.lineTo(pad + w * 0.22f, cy + h * 0.18f);
                canvas.drawPath(path, paint);
                canvas.drawLine(pad + w * 0.48f, cy + h * 0.18f, w - pad - w * 0.2f, cy + h * 0.18f, paint);
            } else if ("reply".equals(kind)) {
                rect.set(pad, pad, w - pad, h - pad * 2.2f);
                canvas.drawRoundRect(rect, pad * 1.5f, pad * 1.5f, paint);
                path.reset();
                path.moveTo(pad + w * 0.2f, h - pad * 2.2f);
                path.lineTo(pad + w * 0.15f, h - pad);
                path.lineTo(pad + w * 0.4f, h - pad * 2.2f);
                canvas.drawPath(path, paint);
                canvas.drawLine(pad + w * 0.2f, cy - h * 0.12f, w - pad - w * 0.2f, cy - h * 0.12f, paint);
            } else if ("tool".equals(kind)) {
                rect.set(pad + w * 0.1f, pad, w - pad - w * 0.1f, h - pad);
                canvas.drawRoundRect(rect, pad, pad, paint);
                canvas.drawLine(pad + w * 0.28f, cy - h * 0.15f, w - pad - w * 0.28f, cy - h * 0.15f, paint);
                canvas.drawLine(pad + w * 0.28f, cy + h * 0.15f, w - pad - w * 0.28f, cy + h * 0.15f, paint);
            } else {
                float r = Math.min(w, h) / 2f - pad;
                canvas.drawCircle(cx, cy, r, paint);
                paint.setStyle(Paint.Style.FILL);
                canvas.drawCircle(cx, cy - r * 0.35f, r * 0.15f, paint);
                paint.setStyle(Paint.Style.STROKE);
                canvas.drawLine(cx, cy - r * 0.05f, cx, cy + r * 0.5f, paint);
            }
        }
    }

    private static class RobotHeadView extends View {
        private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final RectF headBounds = new RectF();
        private int color = COLOR_BLUE;

        public RobotHeadView(Context context) {
            super(context);
            setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_NO);
        }

        public void setColor(int color) {
            this.color = color;
            invalidate();
        }

        @Override
        protected void onDraw(Canvas canvas) {
            super.onDraw(canvas);
            float w = getWidth();
            float h = getHeight();
            float cx = w / 2f;
            float cy = h / 2f;
            float radius = Math.min(w, h) / 2f * 0.8f;

            paint.setColor(color);
            paint.setStyle(Paint.Style.FILL);
            headBounds.set(cx - radius, cy - radius * 0.65f, cx + radius, cy + radius * 0.65f);
            canvas.drawRoundRect(headBounds, radius * 0.35f, radius * 0.35f, paint);

            paint.setColor(BG_DARK);
            canvas.drawCircle(cx - radius * 0.38f, cy - radius * 0.08f, radius * 0.12f, paint);
            canvas.drawCircle(cx + radius * 0.38f, cy - radius * 0.08f, radius * 0.12f, paint);

            paint.setColor(color);
            canvas.drawRect(cx - 2, cy - radius, cx + 2, cy - radius * 0.62f, paint);
            canvas.drawCircle(cx, cy - radius, radius * 0.12f, paint);
        }
    }

    private static class ConnectionDotView extends View {
        private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
        private boolean connected = false;

        public ConnectionDotView(Context context) {
            super(context);
            setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_NO);
        }

        public void setConnected(boolean connected) {
            this.connected = connected;
            invalidate();
        }

        @Override
        protected void onDraw(Canvas canvas) {
            super.onDraw(canvas);
            float cx = getWidth() / 2f;
            float cy = getHeight() / 2f;
            float r = Math.min(cx, cy) * 0.7f;
            paint.setColor(connected ? COLOR_GREEN : COLOR_RED);
            canvas.drawCircle(cx, cy, r, paint);
        }
    }

    private static class StatusMarkView extends View {
        private final Paint bgPaint = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint borderPaint = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint glyphPaint = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Path path = new Path();
        private final RectF bounds = new RectF();
        private DashboardState.AgentState state;

        public StatusMarkView(Context context) {
            super(context);
            setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_NO);
            bgPaint.setStyle(Paint.Style.FILL);
            borderPaint.setStyle(Paint.Style.STROKE);
            borderPaint.setStrokeWidth(dp(1.5f));
            glyphPaint.setStyle(Paint.Style.STROKE);
            glyphPaint.setStrokeWidth(dp(2.5f));
            glyphPaint.setStrokeCap(Paint.Cap.ROUND);
            glyphPaint.setStrokeJoin(Paint.Join.ROUND);
        }

        private float dp(float val) {
            return val * getResources().getDisplayMetrics().density;
        }

        public void setState(DashboardState.AgentState state) {
            this.state = state;
            invalidate();
        }

        private float getAnimatorDurationScale() {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.JELLY_BEAN_MR1) {
                try {
                    return android.provider.Settings.Global.getFloat(
                            getContext().getContentResolver(),
                            android.provider.Settings.Global.ANIMATOR_DURATION_SCALE, 1.0f);
                } catch (Exception ignored) {}
            }
            return 1.0f;
        }

        @Override
        protected void onDraw(Canvas canvas) {
            super.onDraw(canvas);
            if (state == null) return;
            float w = getWidth();
            float h = getHeight();
            if (w <= 0 || h <= 0) return;

            bgPaint.setAlpha(255);
            borderPaint.setAlpha(255);
            glyphPaint.setAlpha(255);

            int color = COLOR_BLUE;
            if (state.requiresAction) color = COLOR_AMBER;
            else if ("completed".equals(state.status)) color = COLOR_GREEN;
            else if ("error".equals(state.status)) color = COLOR_RED;
            else if ("waiting".equals(state.status)) color = COLOR_AMBER;
            else if ("working".equals(state.status)) color = COLOR_CYAN;

            // Background: Dark tinted background
            int bgAlpha = 45;
            int bgTint = Color.argb(bgAlpha, Color.red(color), Color.green(color), Color.blue(color));
            bgPaint.setColor(bgTint);
            float cornerRadius = dp(12f);
            bounds.set(0, 0, w, h);
            canvas.drawRoundRect(bounds, cornerRadius, cornerRadius, bgPaint);

            // Border
            borderPaint.setColor(color);
            float inset = borderPaint.getStrokeWidth() / 2f;
            bounds.set(inset, inset, w - inset, h - inset);
            canvas.drawRoundRect(bounds, cornerRadius, cornerRadius, borderPaint);

            // Glyph
            glyphPaint.setColor(color);
            float cx = w / 2f;
            float cy = h / 2f;
            float r = Math.min(w, h) * 0.28f;

            path.reset();
            boolean isWorking = "working".equals(state.status) && !state.requiresAction;

            if (isWorking) {
                float durationScale = getAnimatorDurationScale();
                if (durationScale > 0f) {
                    float phase = (float) Math.sin((SystemClock.uptimeMillis() % 1200L) / 1200f * 2.0 * Math.PI);
                    int pulseAlpha = 200 + (int) (27.5f * (phase + 1.0f));
                    glyphPaint.setAlpha(pulseAlpha);
                    postInvalidateOnAnimation();
                }

                path.moveTo(cx - r * 1.1f, cy);
                path.lineTo(cx - r * 0.5f, cy);
                path.lineTo(cx - r * 0.25f, cy - r * 0.6f);
                path.lineTo(cx + r * 0.1f, cy + r * 0.7f);
                path.lineTo(cx + r * 0.4f, cy - r * 0.3f);
                path.lineTo(cx + r * 0.6f, cy);
                path.lineTo(cx + r * 1.1f, cy);
                glyphPaint.setStyle(Paint.Style.STROKE);
                canvas.drawPath(path, glyphPaint);
            } else if ("completed".equals(state.status)) {
                path.moveTo(cx - r * 0.6f, cy);
                path.lineTo(cx - r * 0.15f, cy + r * 0.45f);
                path.lineTo(cx + r * 0.65f, cy - r * 0.35f);
                glyphPaint.setStyle(Paint.Style.STROKE);
                canvas.drawPath(path, glyphPaint);
            } else if ("error".equals(state.status)) {
                path.moveTo(cx - r * 0.5f, cy - r * 0.5f);
                path.lineTo(cx + r * 0.5f, cy + r * 0.5f);
                path.moveTo(cx + r * 0.5f, cy - r * 0.5f);
                path.lineTo(cx - r * 0.5f, cy + r * 0.5f);
                glyphPaint.setStyle(Paint.Style.STROKE);
                canvas.drawPath(path, glyphPaint);
            } else if ("waiting".equals(state.status) || state.requiresAction) {
                glyphPaint.setStyle(Paint.Style.STROKE);
                canvas.drawCircle(cx, cy, r * 0.75f, glyphPaint);
                path.moveTo(cx, cy - r * 0.45f);
                path.lineTo(cx, cy);
                path.lineTo(cx + r * 0.3f, cy);
                canvas.drawPath(path, glyphPaint);
            } else {
                glyphPaint.setStyle(Paint.Style.STROKE);
                canvas.drawLine(cx - r * 0.3f, cy - r * 0.45f, cx - r * 0.3f, cy + r * 0.45f, glyphPaint);
                canvas.drawLine(cx + r * 0.3f, cy - r * 0.45f, cx + r * 0.3f, cy + r * 0.45f, glyphPaint);
            }
        }
    }

    private static class ActivityBarView extends View {
        private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final TextPaint labelPaint = new TextPaint(Paint.ANTI_ALIAS_FLAG);
        private DashboardState.AgentState state;

        public ActivityBarView(Context context) {
            super(context);
            setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_NO);
        }

        private float dp(float value) {
            return value * getResources().getDisplayMetrics().density;
        }

        public void setState(DashboardState.AgentState state) {
            this.state = state;
            int total = totalSteps(state);
            int stage = Math.max(1, Math.min(currentStage(state), total));
            setContentDescription(state == null ? "工作階段" :
                    "步驟 " + stage + " / " + total + "，" + ActivityStageHelper.progressTitle(state));
            invalidate();
            requestLayout();
        }

        private StaticLayout createStaticLayout(String title, int availableWidth) {
            if (availableWidth <= 0) availableWidth = 100;
            labelPaint.setTextAlign(Paint.Align.LEFT);
            labelPaint.setTextSize(dp(11));
            labelPaint.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
                return StaticLayout.Builder.obtain(title, 0, title.length(), labelPaint, availableWidth)
                        .setAlignment(Layout.Alignment.ALIGN_CENTER)
                        .setLineSpacing(dp(2), 1.1f)
                        .setMaxLines(3)
                        .setEllipsize(TextUtils.TruncateAt.END)
                        .build();
            } else {
                return new StaticLayout(title, labelPaint, availableWidth, Layout.Alignment.ALIGN_CENTER, 1.1f, dp(2), false);
            }
        }

        @Override
        protected void onMeasure(int widthMeasureSpec, int heightMeasureSpec) {
            int width = MeasureSpec.getSize(widthMeasureSpec);
            int availableWidth = width > 0 ? (int) Math.max(dp(40), width - dp(32)) : (int) dp(280);
            String title = (state != null) ? ActivityStageHelper.progressTitle(state) : "";
            if (title == null) title = "";
            StaticLayout layout = createStaticLayout(title, availableWidth);
            int contentHeight = (int) Math.ceil(dp(38) + layout.getHeight() + dp(10));
            int desiredHeight = Math.max((int) dp(54), contentHeight);
            setMeasuredDimension(resolveSize(width, widthMeasureSpec), resolveSize(desiredHeight, heightMeasureSpec));
        }

        @Override
        @SuppressLint("DrawAllocation")
        protected void onDraw(Canvas canvas) {
            super.onDraw(canvas);
            float w = getWidth();
            float h = getHeight();
            if (w <= 0 || h <= 0) return;
            if (state == null) return;

            int totalSteps = totalSteps(state);
            int activeStage = Math.max(1, Math.min(currentStage(state), totalSteps));
            int accent = stageColor(state);
            float centerY = dp(20);
            float left = dp(24);
            float right = w - dp(24);
            float gap = totalSteps > 1 ? (right - left) / (totalSteps - 1f) : 0f;

            paint.setStrokeWidth(dp(4));
            paint.setStrokeCap(Paint.Cap.ROUND);
            for (int i = 0; i < totalSteps - 1; i++) {
                paint.setColor(i + 1 < activeStage ? COLOR_BLUE : BORDER);
                canvas.drawLine(left + gap * i, centerY, left + gap * (i + 1), centerY, paint);
            }

            labelPaint.setTextAlign(Paint.Align.CENTER);
            labelPaint.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
            for (int i = 0; i < totalSteps; i++) {
                float x = totalSteps > 1 ? left + gap * i : (left + right) / 2f;
                boolean active = i + 1 == activeStage;
                boolean complete = i + 1 < activeStage;
                int color = active ? accent : (complete ? COLOR_BLUE : TEXT_DIM);
                paint.setColor(color);
                paint.setStyle(Paint.Style.FILL);
                canvas.drawCircle(x, centerY, dp(active ? 10 : 8), paint);
                labelPaint.setTextSize(dp(9));
                labelPaint.setColor(SURFACE);
                canvas.drawText(String.valueOf(i + 1), x, centerY + dp(3), labelPaint);
            }

            labelPaint.setTextSize(dp(11));
            labelPaint.setColor(TEXT_PRIMARY);
            String title = ActivityStageHelper.progressTitle(state);
            float availableWidth = Math.max(0, w - dp(32));
            StaticLayout layout = createStaticLayout(title, (int) availableWidth);

            canvas.save();
            canvas.translate(w / 2f - layout.getWidth() / 2f, dp(38));
            layout.draw(canvas);
            canvas.restore();

            if ("working".equals(state.status) || "waiting".equals(state.status)) {
                postInvalidateOnAnimation();
            }
        }

        private int totalSteps(DashboardState.AgentState value) {
            return Math.max(1, ActivityStageHelper.totalSteps(value));
        }

        private int currentStage(DashboardState.AgentState value) {
            return ActivityStageHelper.currentStage(value);
        }

        private int stageColor(DashboardState.AgentState value) {
            if (value.requiresAction || "waiting".equals(value.status)) return COLOR_AMBER;
            if ("completed".equals(value.status)) return COLOR_GREEN;
            if ("error".equals(value.status)) return COLOR_RED;
            if ("working".equals(value.status)) return COLOR_CYAN;
            return COLOR_BLUE;
        }
    }
}
