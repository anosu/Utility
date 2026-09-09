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

            float radius = Math.Min(
                RoundToPixel(8f * spacingScale),
                Math.Min(width, height) * 0.5f
            );
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
