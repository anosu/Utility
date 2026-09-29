using System;

namespace Utility.Notifications.Internal
{
    internal static class ToastMetrics
    {
        internal const int RoundedBandCount = 7;

        private const float MaximumTitleBodyGap = 4f;
        private const float MinimumTitleBodyGap = 2f;

        internal static float ContentWidth(ToastLayout layout) =>
            Math.Max(1f, layout.Width - layout.ContentInset * 2f);

        internal static float CalculateVerticalInset(float spacingScale) =>
            RoundToPixel(8f * spacingScale);

        internal static float CalculateTitleHeight(ToastLayout layout) =>
            CalculateLineHeight(layout.TitleSize);

        // CJK system fonts can have taller ascenders/descenders than Unity's Latin font.
        internal static float CalculateLineHeight(int fontSize) =>
            (float)Math.Ceiling(fontSize * 1.5f);

        internal static float CalculateTitleBodyGap(ToastLayout layout) =>
            CalculateTitleBodyGap(layout.TitleSize, layout.SpacingScale);

        private static float CalculateTitleBodyGap(int titleSize, float spacingScale) =>
            RoundToPixel(
                Math.Clamp(
                    titleSize * 0.125f,
                    MinimumTitleBodyGap * spacingScale,
                    MaximumTitleBodyGap * spacingScale
                )
            );

        internal static float CalculateMinimumTextHeight(
            int titleSize,
            int textSize,
            float spacingScale
        ) =>
            CalculateVerticalInset(spacingScale) * 2f
            + CalculateLineHeight(titleSize)
            + CalculateTitleBodyGap(titleSize, spacingScale)
            + CalculateLineHeight(textSize);

        internal static float CalculateMessageTop(ToastLayout layout) =>
            layout.VerticalInset + CalculateTitleHeight(layout) + CalculateTitleBodyGap(layout);

        internal static float CalculateMessageHeight(
            float cardHeight,
            float messageTop,
            ToastLayout layout
        ) => Math.Max(1f, cardHeight - messageTop - layout.VerticalInset);

        internal static float RoundToPixel(float value) =>
            (float)Math.Round(value, MidpointRounding.AwayFromZero);

        internal static float FloorToPixel(float value) => (float)Math.Floor(value);

        internal static void GetRoundedBand(
            float width,
            float height,
            int index,
            out float top,
            out float bandHeight,
            out float inset,
            float spacingScale = 1f
        )
        {
            if (index < 0 || index >= RoundedBandCount)
                throw new ArgumentOutOfRangeException(nameof(index));

            float snappedHeight = RoundToPixel(height);
            float radius = Math.Min(
                RoundToPixel(8f * spacingScale),
                Math.Min(width, snappedHeight) * 0.5f
            );
            float edgeBandHeight = radius * 0.25f;
            float firstEdge = RoundToPixel(edgeBandHeight);
            float secondEdge = RoundToPixel(edgeBandHeight * 2f);
            float thirdEdge = RoundToPixel(edgeBandHeight * 3f);
            switch (index)
            {
                case 0:
                    top = 0f;
                    bandHeight = firstEdge;
                    inset = RoundToPixel(radius * 0.75f);
                    break;
                case 1:
                    top = firstEdge;
                    bandHeight = secondEdge - firstEdge;
                    inset = RoundToPixel(radius * 0.375f);
                    break;
                case 2:
                    top = secondEdge;
                    bandHeight = thirdEdge - secondEdge;
                    inset = RoundToPixel(radius * 0.125f);
                    break;
                case 3:
                    top = thirdEdge;
                    bandHeight = Math.Max(0f, snappedHeight - thirdEdge * 2f);
                    inset = 0f;
                    break;
                case 4:
                    top = snappedHeight - thirdEdge;
                    bandHeight = thirdEdge - secondEdge;
                    inset = RoundToPixel(radius * 0.125f);
                    break;
                case 5:
                    top = snappedHeight - secondEdge;
                    bandHeight = secondEdge - firstEdge;
                    inset = RoundToPixel(radius * 0.375f);
                    break;
                default:
                    top = snappedHeight - firstEdge;
                    bandHeight = firstEdge;
                    inset = RoundToPixel(radius * 0.75f);
                    break;
            }
        }
    }
}
