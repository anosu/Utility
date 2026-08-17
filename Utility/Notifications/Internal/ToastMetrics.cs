using System;

namespace Utility.Notifications.Internal
{
    internal static class ToastMetrics
    {
        internal const float AccentWidth = 6f;
        internal const float BottomPadding = 15f;
        internal const float CompatibilityMessageTop = 37f;
        internal const float CompatibilityTitleHeight = 18f;
        internal const float CornerRadius = 8f;
        internal const float HorizontalPadding = 12f;
        internal const float TitleHeight = 28f;
        internal const float TopPadding = 15f;

        private const float MaximumTitleBodyGap = 8f;
        private const float MinimumTitleBodyGap = 5f;

        internal static float ContentLeft => AccentWidth + HorizontalPadding;
        internal static float StretchedContentOffsetX => AccentWidth * 0.5f;
        internal static float StretchedContentWidthDelta => -AccentWidth - HorizontalPadding * 2f;

        internal static float ContentWidth(float cardWidth) =>
            Math.Max(1f, cardWidth - AccentWidth - HorizontalPadding * 2f);

        internal static float CalculateTitleHeight(ToastLayout layout) =>
            Math.Max(TitleHeight, layout.TitleSize + 4f);

        internal static float CalculateTitleBodyGap(ToastLayout layout) =>
            Math.Clamp(layout.TitleSize * 0.25f, MinimumTitleBodyGap, MaximumTitleBodyGap);

        internal static float CalculateMessageTop(ToastLayout layout) =>
            TopPadding + CalculateTitleHeight(layout) + CalculateTitleBodyGap(layout);

        internal static float CalculateMessageHeight(float cardHeight, float messageTop) =>
            Math.Max(1f, cardHeight - messageTop - BottomPadding);
    }
}
