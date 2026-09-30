package dev.jitsu.utility.toast;

import android.app.Activity;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Rect;
import android.graphics.Typeface;
import android.os.Handler;
import android.os.Looper;
import android.text.Layout;
import android.text.StaticLayout;
import android.text.TextPaint;
import android.text.TextUtils;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowInsets;
import android.widget.FrameLayout;
import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;
import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

/** Draws toast cards in the Activity's pixel-resolution view hierarchy. */
public final class ToastOverlayBridge {
    private static final Handler UI = new Handler(Looper.getMainLooper());
    private static volatile int[] viewport = new int[6];
    private static Update pending;
    private static Activity maintenanceActivity;
    private static Activity activity;
    private static Overlay overlay;
    private static View host;
    private static boolean scheduled;
    private static boolean maintenanceScheduled;
    private static Snapshot latestSnapshot;
    private static volatile RuntimeException failure;
    private static volatile int generation;
    private static final Runnable APPLY = new Runnable() {
        @Override public void run() {
            Update update;
            synchronized (ToastOverlayBridge.class) {
                update = pending;
                pending = null;
                scheduled = false;
            }
            if (update == null || update.generation != generation) return;
            try {
                Snapshot snapshot = Snapshot.parse(
                        new JSONObject(new String(update.json, StandardCharsets.UTF_8)));
                latestSnapshot = snapshot;
                if (update.activity.isFinishing()) return;
                attach(update.activity);
                if (overlay != null) overlay.setSnapshot(snapshot);
            } catch (JSONException exception) {
                if (update.generation == generation)
                    failure = new IllegalArgumentException("Invalid toast snapshot", exception);
            } catch (RuntimeException exception) {
                if (update.generation == generation) failure = exception;
            }
        }
    };
    private static final Runnable MAINTAIN = new Runnable() {
        @Override public void run() {
            Activity current;
            int requestedGeneration;
            synchronized (ToastOverlayBridge.class) {
                current = maintenanceActivity;
                requestedGeneration = generation;
                maintenanceActivity = null;
                maintenanceScheduled = false;
            }
            if (current == null || requestedGeneration != generation || current.isFinishing()) return;
            try {
                Overlay previous = overlay;
                attach(current);
                if (overlay != previous && overlay != null && latestSnapshot != null)
                    overlay.setSnapshot(latestSnapshot);
            } catch (RuntimeException exception) {
                if (requestedGeneration == generation) failure = exception;
            }
        }
    };

    private ToastOverlayBridge() {}

    public static void start(final Activity current) throws InterruptedException {
        synchronized (ToastOverlayBridge.class) {
            generation++;
            pending = null;
            maintenanceActivity = null;
            scheduled = false;
            maintenanceScheduled = false;
        }
        UI.removeCallbacks(APPLY);
        UI.removeCallbacks(MAINTAIN);
        if (Looper.myLooper() == Looper.getMainLooper()) {
            latestSnapshot = null;
            detach();
            attach(current);
            failure = null;
            return;
        }
        final CountDownLatch ready = new CountDownLatch(1);
        final AtomicReference<RuntimeException> error = new AtomicReference<>();
        if (!UI.post(new Runnable() {
            @Override public void run() {
                try {
                    latestSnapshot = null;
                    detach();
                    attach(current);
                    failure = null;
                }
                catch (RuntimeException exception) { error.set(exception); }
                finally { ready.countDown(); }
            }
        })) throw new IllegalStateException("Android UI thread rejected toast startup");
        if (!ready.await(3, TimeUnit.SECONDS))
            throw new IllegalStateException("Timed out attaching the Android toast view");
        if (error.get() != null) throw error.get();
        if (overlay == null || overlay.getParent() == null)
            throw new IllegalStateException("Android toast view was not attached");
    }

    public static void present(final Activity current, byte[] json) {
        if (failure != null) throw failure;
        synchronized (ToastOverlayBridge.class) {
            pending = new Update(current, json, generation);
            if (scheduled) return;
            scheduled = true;
            if (!UI.post(APPLY)) {
                pending = null;
                scheduled = false;
                throw new IllegalStateException("Android UI thread rejected toast frame");
            }
        }
    }

    public static void maintain(Activity current) {
        if (failure != null) throw failure;
        synchronized (ToastOverlayBridge.class) {
            maintenanceActivity = current;
            if (maintenanceScheduled) return;
            maintenanceScheduled = true;
            if (!UI.post(MAINTAIN)) {
                maintenanceActivity = null;
                maintenanceScheduled = false;
                throw new IllegalStateException("Android UI thread rejected toast maintenance");
            }
        }
    }

    public static int[] viewport() { return viewport; }

    public static void stop() {
        final int stoppedGeneration;
        synchronized (ToastOverlayBridge.class) {
            stoppedGeneration = ++generation;
            pending = null;
            maintenanceActivity = null;
            scheduled = false;
            maintenanceScheduled = false;
        }
        UI.removeCallbacks(APPLY);
        UI.removeCallbacks(MAINTAIN);
        UI.post(new Runnable() {
            @Override public void run() {
                if (stoppedGeneration != generation) return;
                latestSnapshot = null;
                failure = null;
                detach();
            }
        });
    }

    private static void attach(Activity current) {
        if (current == null || current.isFinishing())
            throw new IllegalStateException("Android Activity is unavailable");
        View unityHost = findUnityHost(current);
        if (activity == current && overlay != null && overlay.getParent() != null) {
            if (unityHost == host || unityHost == null
                    || !(unityHost.getParent() instanceof FrameLayout)) {
                syncHostLayout();
                ViewGroup parent = (ViewGroup) overlay.getParent();
                if (parent.indexOfChild(overlay) != parent.getChildCount() - 1)
                    overlay.bringToFront();
                return;
            }
        }
        detach();
        activity = current;
        ViewGroup parent = unityHost != null && unityHost.getParent() instanceof FrameLayout
                ? (ViewGroup) unityHost.getParent()
                : (ViewGroup) current.getWindow().getDecorView();
        host = unityHost != null && parent == unityHost.getParent() ? unityHost : null;
        overlay = new Overlay(current);
        overlay.setClickable(false);
        overlay.setFocusable(false);
        parent.addView(overlay, copyLayout(host));
        syncHostLayout();
    }

    private static void detach() {
        if (overlay != null && overlay.getParent() instanceof ViewGroup)
            ((ViewGroup) overlay.getParent()).removeView(overlay);
        overlay = null;
        host = null;
        activity = null;
        viewport = new int[6];
    }

    private static void syncHostLayout() {
        if (overlay == null || host == null) return;
        ViewGroup.LayoutParams source = host.getLayoutParams();
        ViewGroup.LayoutParams target = overlay.getLayoutParams();
        if (source.width != target.width || source.height != target.height
                || source instanceof FrameLayout.LayoutParams
                && target instanceof FrameLayout.LayoutParams
                && (((FrameLayout.LayoutParams) source).gravity != ((FrameLayout.LayoutParams) target).gravity
                || ((FrameLayout.LayoutParams) source).leftMargin != ((FrameLayout.LayoutParams) target).leftMargin
                || ((FrameLayout.LayoutParams) source).topMargin != ((FrameLayout.LayoutParams) target).topMargin
                || ((FrameLayout.LayoutParams) source).rightMargin != ((FrameLayout.LayoutParams) target).rightMargin
                || ((FrameLayout.LayoutParams) source).bottomMargin != ((FrameLayout.LayoutParams) target).bottomMargin))
            overlay.setLayoutParams(copyLayout(host));
    }

    private static FrameLayout.LayoutParams copyLayout(View source) {
        if (source != null && source.getLayoutParams() instanceof FrameLayout.LayoutParams) {
            FrameLayout.LayoutParams original = (FrameLayout.LayoutParams) source.getLayoutParams();
            FrameLayout.LayoutParams copy = new FrameLayout.LayoutParams(
                    original.width, original.height, original.gravity);
            copy.setMargins(original.leftMargin, original.topMargin,
                    original.rightMargin, original.bottomMargin);
            return copy;
        }
        return new FrameLayout.LayoutParams(-1, -1, Gravity.FILL);
    }

    private static View findUnityHost(Activity current) {
        for (Class<?> type = current.getClass(); type != null; type = type.getSuperclass()) {
            try {
                Field field = type.getDeclaredField("mUnityPlayer");
                field.setAccessible(true);
                Object player = field.get(current);
                if (player instanceof View) return (View) player;
                if (player == null) return null;
                for (String name : new String[] { "getFrameLayout", "getView" }) {
                    try {
                        Method method = player.getClass().getMethod(name);
                        Object view = method.invoke(player);
                        if (view instanceof View) return (View) view;
                    } catch (NoSuchMethodException ignored) {
                        // Different Unity versions expose different host accessors.
                    }
                }
                return null;
            } catch (NoSuchFieldException ignored) {
                // Unity Activity versions store the player at different inheritance levels.
            } catch (Exception ignored) {
                return null;
            }
        }
        return null;
    }

    private static final class Update {
        final Activity activity;
        final byte[] json;
        final int generation;
        Update(Activity activity, byte[] json, int generation) {
            this.activity = activity;
            this.json = json;
            this.generation = generation;
        }
    }

    private static final class Card {
        final String title;
        final String message;
        final int accent;
        final float alpha;
        Card(JSONObject json) throws JSONException {
            title = json.getString("title");
            message = json.getString("message");
            accent = json.getInt("accent");
            alpha = (float) json.getDouble("alpha");
        }
    }

    private static final class Snapshot {
        final int anchor, background, titleColor, textColor;
        final float width, minimumHeight, margin, gap;
        final float contentInset, verticalInset, cornerRadius, accentWidth, messageTop;
        final int titleSize, textSize;
        final Card[] cards;
        Snapshot(JSONObject json) throws JSONException {
            anchor = json.getInt("anchor");
            background = json.getInt("background");
            titleColor = json.getInt("titleColor");
            textColor = json.getInt("textColor");
            width = (float) json.getDouble("width");
            minimumHeight = (float) json.getDouble("minimumHeight");
            margin = (float) json.getDouble("margin");
            gap = (float) json.getDouble("gap");
            contentInset = (float) json.getDouble("contentInset");
            verticalInset = (float) json.getDouble("verticalInset");
            cornerRadius = (float) json.getDouble("cornerRadius");
            accentWidth = (float) json.getDouble("accentWidth");
            messageTop = (float) json.getDouble("messageTop");
            titleSize = json.getInt("titleSize");
            textSize = json.getInt("textSize");
            JSONArray array = json.getJSONArray("cards");
            cards = new Card[array.length()];
            for (int i = 0; i < cards.length; i++) cards[i] = new Card(array.getJSONObject(i));
        }
        static Snapshot parse(JSONObject json) throws JSONException { return new Snapshot(json); }
    }

    private static final class Overlay extends View {
        private final Paint surface = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final TextPaint title = new TextPaint(Paint.ANTI_ALIAS_FLAG);
        private final TextPaint body = new TextPaint(Paint.ANTI_ALIAS_FLAG);
        private final Rect insets = new Rect();
        private Snapshot snapshot;
        private String[] measuredMessages = new String[0];
        private StaticLayout[] measuredLayouts = new StaticLayout[0];
        private String[] titleInputs = new String[0];
        private String[] fittedTitles = new String[0];
        private float[] heights = new float[0];
        private int measuredTextSize;
        private int measuredContentWidth;
        private int fittedTitleSize;
        private float fittedTitleWidth;

        Overlay(Activity activity) {
            super(activity);
            title.setTypeface(Typeface.create("sans-serif", Typeface.BOLD));
            body.setTypeface(Typeface.create("sans-serif", Typeface.NORMAL));
            setWillNotDraw(false);
        }

        @Override public boolean onTouchEvent(android.view.MotionEvent event) { return false; }

        @Override protected void onSizeChanged(int width, int height, int oldWidth, int oldHeight) {
            super.onSizeChanged(width, height, oldWidth, oldHeight);
            updateViewport();
        }

        @Override protected void onLayout(boolean changed, int left, int top, int right, int bottom) {
            super.onLayout(changed, left, top, right, bottom);
            if (changed) updateViewport();
        }

        @Override public WindowInsets onApplyWindowInsets(WindowInsets windowInsets) {
            WindowInsets result = super.onApplyWindowInsets(windowInsets);
            updateViewport();
            return result;
        }

        void setSnapshot(Snapshot value) {
            snapshot = value;
            if (value.cards.length == 0) {
                measuredMessages = new String[0];
                measuredLayouts = new StaticLayout[0];
                titleInputs = new String[0];
                fittedTitles = new String[0];
                heights = new float[0];
            }
            invalidate();
        }

        private void updateViewport() {
            WindowInsets windowInsets = getRootWindowInsets();
            View root = getRootView();
            int[] rootPosition = new int[2];
            int[] viewPosition = new int[2];
            root.getLocationInWindow(rootPosition);
            getLocationInWindow(viewPosition);
            int safeLeft = rootPosition[0];
            int safeTop = rootPosition[1];
            int safeRight = safeLeft + root.getWidth();
            int safeBottom = safeTop + root.getHeight();
            if (windowInsets != null) {
                safeLeft += windowInsets.getSystemWindowInsetLeft();
                safeTop += windowInsets.getSystemWindowInsetTop();
                safeRight -= windowInsets.getSystemWindowInsetRight();
                safeBottom -= windowInsets.getSystemWindowInsetBottom();
            }
            int width = getWidth(), height = getHeight();
            int left = Math.max(0, Math.min(width, safeLeft - viewPosition[0]));
            int top = Math.max(0, Math.min(height, safeTop - viewPosition[1]));
            int right = Math.max(0, Math.min(width - left,
                    viewPosition[0] + width - safeRight));
            int bottom = Math.max(0, Math.min(height - top,
                    viewPosition[1] + height - safeBottom));
            insets.set(left, top, right, bottom);
            viewport = new int[] { getWidth(), getHeight(), insets.left, insets.top,
                    insets.right, insets.bottom };
            invalidate();
        }

        @Override protected void onDraw(Canvas canvas) {
            Snapshot data = snapshot;
            if (data == null || data.cards.length == 0 || getWidth() <= 0 || getHeight() <= 0) return;
            float safeLeft = insets.left;
            float safeTop = insets.top;
            float safeWidth = Math.max(1, getWidth() - insets.left - insets.right);
            float safeHeight = Math.max(1, getHeight() - insets.top - insets.bottom);
            float margin = data.margin;
            float cardWidth = data.width;
            float gap = data.gap;
            float inset = data.contentInset;
            float verticalInset = data.verticalInset;
            float radius = data.cornerRadius;
            title.setTextSize(data.titleSize);
            body.setTextSize(data.textSize);
            float messageTop = data.messageTop;
            float contentWidth = Math.max(1, cardWidth - inset * 2);
            float minimum = data.minimumHeight;

            int measuredWidth = Math.max(1, (int) contentWidth);
            if (measuredLayouts.length != data.cards.length
                    || measuredTextSize != data.textSize
                    || measuredContentWidth != measuredWidth) {
                measuredMessages = new String[data.cards.length];
                measuredLayouts = new StaticLayout[data.cards.length];
                measuredTextSize = data.textSize;
                measuredContentWidth = measuredWidth;
            }
            if (fittedTitles.length != data.cards.length
                    || fittedTitleSize != data.titleSize
                    || fittedTitleWidth != contentWidth) {
                titleInputs = new String[data.cards.length];
                fittedTitles = new String[data.cards.length];
                fittedTitleSize = data.titleSize;
                fittedTitleWidth = contentWidth;
            }
            if (heights.length != data.cards.length) heights = new float[data.cards.length];
            for (int i = 0; i < data.cards.length; i++) {
                String message = data.cards[i].message;
                if (measuredLayouts[i] == null || !message.equals(measuredMessages[i])) {
                    measuredMessages[i] = message;
                    measuredLayouts[i] = StaticLayout.Builder.obtain(message, 0,
                            message.length(), body, measuredWidth)
                            .setAlignment(Layout.Alignment.ALIGN_NORMAL).setIncludePad(false).build();
                }
                heights[i] = Math.max(minimum,
                        messageTop + measuredLayouts[i].getHeight() + verticalInset);
                String titleText = data.cards[i].title;
                if (fittedTitles[i] == null || !titleText.equals(titleInputs[i])) {
                    titleInputs[i] = titleText;
                    fittedTitles[i] = TextUtils.ellipsize(titleText, title, contentWidth,
                            TextUtils.TruncateAt.END).toString();
                }
            }
            fitHeights(heights, Math.max(1, safeHeight - margin * 2 - gap * (heights.length - 1)));
            float total = gap * (heights.length - 1);
            for (float height : heights) total += height;
            int column = data.anchor % 3;
            boolean bottom = data.anchor >= 3;
            float x = column == 0 ? safeLeft + margin
                    : column == 1 ? safeLeft + (safeWidth - cardWidth) * .5f
                    : safeLeft + safeWidth - margin - cardWidth;
            float y = bottom ? safeTop + safeHeight - margin - total : safeTop + margin;
            x = Math.round(x);
            y = Math.round(y);

            for (int i = 0; i < data.cards.length; i++) {
                Card card = data.cards[i];
                float height = Math.round(heights[i]);
                int opacity = Math.max(0, Math.min(255, Math.round(card.alpha * 255)));
                if (opacity == 0) { y += height + gap; continue; }
                int layer = opacity == 255 ? -1
                        : canvas.saveLayerAlpha(x, y, x + cardWidth, y + height, opacity);
                surface.setColor(data.background);
                canvas.drawRoundRect(x, y, x + cardWidth, y + height, radius, radius, surface);
                surface.setColor(card.accent);
                canvas.drawRect(x, y + radius, x + data.accentWidth,
                        y + height - radius, surface);
                title.setColor(data.titleColor);
                canvas.drawText(fittedTitles[i], x + inset,
                        y + verticalInset - title.getFontMetrics().ascent, title);
                body.setColor(data.textColor);
                int clip = canvas.save();
                canvas.clipRect(x + inset, y + messageTop, x + cardWidth - inset,
                        y + height - verticalInset);
                canvas.translate(x + inset, y + messageTop);
                measuredLayouts[i].draw(canvas);
                canvas.restoreToCount(clip);
                if (layer != -1) canvas.restoreToCount(layer);
                y += height + gap;
            }
        }

        private static void fitHeights(float[] heights, float available) {
            float total = 0, maximum = 0;
            for (float height : heights) { total += height; maximum = Math.max(maximum, height); }
            if (total <= available) return;
            float low = Math.min(maximum, available / heights.length), high = maximum;
            for (int i = 0; i < 24; i++) {
                float middle = (low + high) * .5f;
                float used = 0;
                for (float height : heights) used += Math.min(height, middle);
                if (used > available) high = middle; else low = middle;
            }
            for (int i = 0; i < heights.length; i++) heights[i] = Math.min(heights[i], low);
        }
    }
}
