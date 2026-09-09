using System;
using UnityEngine;
using Utility.Diagnostics;

namespace Utility.Notifications.Internal
{
    internal readonly struct ToastSafeArea
    {
        internal ToastSafeArea(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        internal float X { get; }
        internal float Y { get; }
        internal float Width { get; }
        internal float Height { get; }
        internal float XMax => X + Width;
        internal float YMax => Y + Height;
    }

    internal readonly struct ToastLayout
    {
        internal ToastLayout(
            ToastSafeArea safeArea,
            int screenWidth,
            int screenHeight,
            float width,
            float minimumHeight,
            float margin,
            float gap,
            int maximumVisible,
            int titleSize,
            int textSize,
            float spacingScale = 1f
        )
        {
            SafeArea = safeArea;
            ScreenWidth = screenWidth;
            ScreenHeight = screenHeight;
            Width = width;
            MinimumHeight = minimumHeight;
            Margin = margin;
            Gap = gap;
            MaximumVisible = maximumVisible;
            TitleSize = titleSize;
            TextSize = textSize;
            SpacingScale = spacingScale;
        }

        internal ToastSafeArea SafeArea { get; }
        internal int ScreenWidth { get; }
        internal int ScreenHeight { get; }
        internal float Width { get; }
        internal float MinimumHeight { get; }
        internal float Margin { get; }
        internal float Gap { get; }
        internal int MaximumVisible { get; }
        internal int TitleSize { get; }
        internal int TextSize { get; }
        internal float SpacingScale { get; }
        internal float AccentWidth => ToastMetrics.RoundToPixel(4.8f * SpacingScale);
        internal float CornerRadius => ToastMetrics.RoundToPixel(8f * SpacingScale);
        internal float ContentInset => ToastMetrics.RoundToPixel(12f * SpacingScale);
        internal float VerticalInset => ToastMetrics.CalculateVerticalInset(SpacingScale);
        internal float AvailableHeight => Math.Max(1f, SafeArea.Height - Margin * 2f);

        internal void FitCardHeights(float[] heights, int count)
        {
            if (count < 0 || count > heights.Length)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (count == 0)
                return;

            float availableForCards = Math.Max(1f, AvailableHeight - Gap * Math.Max(0, count - 1));
            float minimum = Math.Min(MinimumHeight, availableForCards / count);
            float total = 0f;
            float maximum = minimum;

            for (int i = 0; i < count; i++)
            {
                float height = float.IsFinite(heights[i]) ? heights[i] : minimum;
                height = Math.Max(minimum, height);
                heights[i] = height;
                total += height;
                maximum = Math.Max(maximum, height);
            }

            if (total <= availableForCards)
                return;

            float lowerBound = minimum;
            float upperBound = maximum;
            for (int iteration = 0; iteration < 24; iteration++)
            {
                float candidate = (lowerBound + upperBound) * 0.5f;
                float candidateTotal = 0f;
                for (int i = 0; i < count; i++)
                    candidateTotal += Math.Min(heights[i], candidate);

                if (candidateTotal > availableForCards)
                    upperBound = candidate;
                else
                    lowerBound = candidate;
            }

            for (int i = 0; i < count; i++)
                heights[i] = Math.Min(heights[i], lowerBound);
        }

        internal float CalculateImguiX(ToastAnchor anchor) =>
            anchor switch
            {
                ToastAnchor.TopLeft or ToastAnchor.BottomLeft => SafeArea.X + Margin,
                ToastAnchor.TopCenter or ToastAnchor.BottomCenter => SafeArea.X
                    + (SafeArea.Width - Width) * 0.5f,
                _ => SafeArea.XMax - Margin - Width,
            };

        internal float CalculateImguiY(ToastAnchor anchor, float totalHeight) =>
            anchor switch
            {
                ToastAnchor.BottomLeft or ToastAnchor.BottomRight or ToastAnchor.BottomCenter =>
                    ScreenHeight - SafeArea.Y - Margin - totalHeight,
                _ => ScreenHeight - SafeArea.YMax + Margin,
            };

        internal float CalculateUguiX(ToastAnchor anchor) =>
            anchor switch
            {
                ToastAnchor.TopLeft or ToastAnchor.BottomLeft => SafeArea.X + Margin,
                ToastAnchor.TopCenter or ToastAnchor.BottomCenter => SafeArea.X
                    + SafeArea.Width * 0.5f
                    - ScreenWidth * 0.5f,
                _ => -(ScreenWidth - SafeArea.XMax + Margin),
            };

        internal float UguiTopInset => ScreenHeight - SafeArea.YMax + Margin;
        internal float UguiBottomInset => SafeArea.Y + Margin;
    }

    internal static class ToastLayoutProvider
    {
        private const float MaximumLandscapeWidthFraction = 0.34f;
        private const float MaximumLayoutScale = 1.6f;
        private const float MaximumTextScale = 1.25f;
        private const float ReferenceHeight = 768f;
        private const float AndroidReferenceShortSide = 480f;
        private const float ReferenceTextDpi = 320f;

        private static bool _dpiUnavailable;
        private static bool _safeAreaUnavailable;

        internal static ToastLayout Calculate(ToastTheme theme)
        {
            int screenWidth = Math.Max(1, Screen.width);
            int screenHeight = Math.Max(1, Screen.height);
            ToastSafeArea safeArea = ReadSafeArea(screenWidth, screenHeight);
            bool isAndroid = ToastPlatform.IsAndroid;
            float textScale = isAndroid ? 1f : ReadTextScale();
            return Calculate(theme, safeArea, screenWidth, screenHeight, textScale, isAndroid);
        }

        internal static ToastLayout Calculate(
            ToastTheme theme,
            ToastSafeArea safeArea,
            int screenWidth,
            int screenHeight,
            float textScale,
            bool isAndroid = false
        )
        {
            screenWidth = Math.Max(1, screenWidth);
            screenHeight = Math.Max(1, screenHeight);
            textScale = float.IsFinite(textScale)
                ? Math.Clamp(textScale, 1f, MaximumTextScale)
                : 1f;
            // Android games can render below the panel resolution. A pixel/DPI floor
            // or a fixed scale ceiling changes physical text size between those games.
            float heightScale = isAndroid
                ? Math.Min(screenWidth, screenHeight) / AndroidReferenceShortSide
                : Math.Clamp(safeArea.Height / ReferenceHeight, 1f, MaximumLayoutScale);
            float widthLimit = CalculateWidthLimit(safeArea);

            float widthScaleLimit = theme.Width > 0f ? widthLimit / theme.Width : 1f;
            float layoutScale = Math.Min(heightScale, Math.Max(1f, widthScaleLimit));
            float spacingScale = isAndroid ? heightScale : 1f;
            float fontScale = isAndroid ? heightScale * 0.85f : Math.Max(layoutScale, textScale);
            int titleSize = ScaleFont(theme.TitleSize, fontScale, isAndroid);
            int textSize = ScaleFont(theme.TextSize, fontScale, isAndroid);
            float shortestSide = Math.Min(safeArea.Width, safeArea.Height);
            float margin = Math.Min(
                theme.Margin * layoutScale,
                Math.Max(0f, (shortestSide - 1f) * 0.5f)
            );
            float availableWidth = Math.Max(1f, safeArea.Width - margin * 2f);
            float width = Math.Max(
                1f,
                ToastMetrics.FloorToPixel(
                    Math.Min(Math.Min(theme.Width * layoutScale, availableWidth), widthLimit)
                )
            );
            float minimumHeight = Math.Max(
                1f,
                ToastMetrics.FloorToPixel(
                    Math.Min(
                        isAndroid
                            ? Math.Max(
                                theme.MinimumHeight * layoutScale,
                                ToastMetrics.CalculateMinimumTextHeight(
                                    titleSize,
                                    textSize,
                                    spacingScale
                                )
                            )
                            : theme.MinimumHeight * layoutScale,
                        Math.Max(1f, safeArea.Height - margin * 2f)
                    )
                )
            );
            float gap = Math.Max(
                0f,
                ToastMetrics.RoundToPixel(
                    Math.Min(theme.Gap * layoutScale, Math.Max(0f, safeArea.Height * 0.1f))
                )
            );
            int maximumVisible = CalculateMaximumVisible(
                theme.MaximumVisible,
                safeArea.Height,
                minimumHeight,
                margin,
                gap
            );

            return new ToastLayout(
                safeArea,
                screenWidth,
                screenHeight,
                width,
                minimumHeight,
                margin,
                gap,
                maximumVisible,
                titleSize,
                textSize,
                spacingScale
            );
        }

        private static int CalculateMaximumVisible(
            int configuredMaximum,
            float safeHeight,
            float minimumHeight,
            float margin,
            float gap
        )
        {
            if (configuredMaximum <= 0)
                return 0;

            float availableHeight = Math.Max(0f, safeHeight - margin * 2f);
            int fittingCount = Math.Max(
                1,
                (int)Math.Floor((availableHeight + gap) / (minimumHeight + gap))
            );
            return Math.Min(configuredMaximum, fittingCount);
        }

        private static float CalculateWidthLimit(ToastSafeArea safeArea)
        {
            if (safeArea.Height <= 0f || safeArea.Width <= safeArea.Height)
                return safeArea.Width;

            const float LandscapeAspect = 4f / 3f;
            float aspectRatio = safeArea.Width / safeArea.Height;
            float progress = Math.Clamp((aspectRatio - 1f) / (LandscapeAspect - 1f), 0f, 1f);
            float smoothProgress = progress * progress * (3f - 2f * progress);
            float widthFraction = 1f + (MaximumLandscapeWidthFraction - 1f) * smoothProgress;
            return safeArea.Width * widthFraction;
        }

        private static ToastSafeArea ReadSafeArea(int screenWidth, int screenHeight)
        {
            var fullScreen = new ToastSafeArea(0f, 0f, screenWidth, screenHeight);
            if (_safeAreaUnavailable)
                return fullScreen;

            try
            {
                Rect safeArea = Screen.safeArea;
                return IsUsable(safeArea, screenWidth, screenHeight)
                    ? new ToastSafeArea(safeArea.x, safeArea.y, safeArea.width, safeArea.height)
                    : fullScreen;
            }
            catch (Exception exception)
            {
                _safeAreaUnavailable = true;
                Logging.WriteRecoverable(
                    LogLevel.Debug,
                    "Toast",
                    "Screen.safeArea is unavailable; using the full screen.",
                    exception
                );
                return fullScreen;
            }
        }

        private static float ReadTextScale()
        {
            if (_dpiUnavailable)
                return 1f;

            try
            {
                float dpi = Screen.dpi;
                return float.IsFinite(dpi) && dpi > ReferenceTextDpi
                    ? Math.Clamp(dpi / ReferenceTextDpi, 1f, MaximumTextScale)
                    : 1f;
            }
            catch (Exception exception)
            {
                _dpiUnavailable = true;
                Logging.WriteRecoverable(
                    LogLevel.Debug,
                    "Toast",
                    "Screen.dpi is unavailable; using the configured text sizes.",
                    exception
                );
                return 1f;
            }
        }

        private static bool IsUsable(Rect area, int screenWidth, int screenHeight) =>
            float.IsFinite(area.x)
            && float.IsFinite(area.y)
            && float.IsFinite(area.width)
            && float.IsFinite(area.height)
            && area.width > 0f
            && area.height > 0f
            && area.xMin >= 0f
            && area.yMin >= 0f
            && area.xMax <= screenWidth
            && area.yMax <= screenHeight;

        private static int ScaleFont(int size, float scale, bool isAndroid) =>
            Math.Clamp(
                (int)Math.Round(size * scale, MidpointRounding.AwayFromZero),
                isAndroid ? 1 : 8,
                isAndroid ? 512 : 96
            );
    }
}
