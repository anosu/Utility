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
        internal const int RoundedBandCount = 7;
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
            RoundToPixel(
                Math.Clamp(layout.TitleSize * 0.25f, MinimumTitleBodyGap, MaximumTitleBodyGap)
            );

        internal static float CalculateMessageTop(ToastLayout layout) =>
            TopPadding + CalculateTitleHeight(layout) + CalculateTitleBodyGap(layout);

        internal static float CalculateMessageHeight(float cardHeight, float messageTop) =>
            Math.Max(1f, cardHeight - messageTop - BottomPadding);

        internal static float RoundToPixel(float value) =>
            (float)Math.Round(value, MidpointRounding.AwayFromZero);

        internal static float FloorToPixel(float value) => (float)Math.Floor(value);

        internal static void GetRoundedBand(
            float width,
            float height,
            int index,
            out float top,
            out float bandHeight,
            out float inset
        )
        {
            if (index < 0 || index >= RoundedBandCount)
                throw new ArgumentOutOfRangeException(nameof(index));

            float radius = Math.Min(CornerRadius, Math.Min(width, height) * 0.5f);
            float edgeBandHeight = radius * 0.25f;
            bandHeight = edgeBandHeight;
            switch (index)
            {
                case 0:
                    top = 0f;
                    inset = radius * 0.75f;
                    break;
                case 1:
                    top = edgeBandHeight;
                    inset = radius * 0.375f;
                    break;
                case 2:
                    top = edgeBandHeight * 2f;
                    inset = radius * 0.125f;
                    break;
                case 3:
                    top = edgeBandHeight * 3f;
                    bandHeight = Math.Max(0f, height - edgeBandHeight * 6f);
                    inset = 0f;
                    break;
                case 4:
                    top = height - edgeBandHeight * 3f;
                    inset = radius * 0.125f;
                    break;
                case 5:
                    top = height - edgeBandHeight * 2f;
                    inset = radius * 0.375f;
                    break;
                default:
                    top = height - edgeBandHeight;
                    inset = radius * 0.75f;
                    break;
            }
        }
    }
}
