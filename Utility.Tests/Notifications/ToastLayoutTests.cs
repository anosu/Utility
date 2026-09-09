using System;
using System.Runtime.CompilerServices;
using Utility.Notifications;
using Utility.Notifications.Internal;
using Xunit;

namespace Utility.Tests.Notifications
{
    public sealed class ToastLayoutTests
    {
        [Theory]
        [InlineData(240)]
        [InlineData(360)]
        [InlineData(480)]
        [InlineData(720)]
        [InlineData(1080)]
        [InlineData(1440)]
        [InlineData(2160)]
        public void AndroidTextRemainsReadableAcrossGameRenderResolutions(int shortSide)
        {
            ToastLayout layout = ToastLayoutProvider.Calculate(
                CreateTheme(),
                new ToastSafeArea(0f, 0f, shortSide * 2f, shortSide),
                shortSide * 2,
                shortSide,
                1f,
                isAndroid: true
            );

            // Keep the smaller typography and its surrounding space proportional when
            // another game on the same phone renders at half or twice the resolution.
            Assert.InRange(Math.Abs(layout.TextSize - shortSide / 30f * 0.85f), 0f, 0.5f);
            Assert.InRange(Math.Abs(layout.TitleSize - shortSide * 19f / 480f * 0.85f), 0f, 0.5f);
            Assert.InRange(Math.Abs(layout.ContentInset - shortSide / 40f), 0f, 0.5f);
            Assert.InRange(Math.Abs(layout.VerticalInset - shortSide / 60f), 0f, 0.5f);
            Assert.InRange(Math.Abs(layout.AccentWidth - shortSide / 100f), 0f, 0.5f);
            Assert.InRange(Math.Abs(layout.CornerRadius - shortSide / 60f), 0f, 0.5f);
            Assert.Equal(
                layout.Width,
                ToastMetrics.ContentWidth(layout) + layout.ContentInset * 2f
            );
        }

        [Fact]
        public void AndroidRotationAndReportedDpiDoNotChangeTextSize()
        {
            ToastTheme theme = CreateTheme();
            ToastLayout landscape = ToastLayoutProvider.Calculate(
                theme,
                new ToastSafeArea(0f, 0f, 2400f, 1080f),
                2400,
                1080,
                1f,
                true
            );
            ToastLayout portrait = ToastLayoutProvider.Calculate(
                theme,
                new ToastSafeArea(0f, 0f, 1080f, 2400f),
                1080,
                2400,
                1.25f,
                true
            );

            Assert.Equal(landscape.TextSize, portrait.TextSize);
            Assert.Equal(landscape.TitleSize, portrait.TitleSize);
            Assert.Equal(landscape.ContentInset, portrait.ContentInset);
            Assert.Equal(landscape.VerticalInset, portrait.VerticalInset);
        }

        [Theory]
        [InlineData(240)]
        [InlineData(480)]
        [InlineData(1080)]
        public void AndroidStackLeavesRoomForATitleAndOneBodyLine(int shortSide)
        {
            ToastTheme theme = CreateTheme();
            theme.TitleSize = 38;
            theme.TextSize = 32;
            ToastLayout layout = ToastLayoutProvider.Calculate(
                theme,
                new ToastSafeArea(0f, 0f, shortSide * 2f, shortSide),
                shortSide * 2,
                shortSide,
                1f,
                true
            );
            float cardHeight =
                ToastMetrics.CalculateMessageTop(layout)
                + ToastMetrics.CalculateLineHeight(layout.TextSize)
                + layout.VerticalInset;
            float[] heights = new float[layout.MaximumVisible];
            Array.Fill(heights, cardHeight);

            layout.FitCardHeights(heights, heights.Length);

            foreach (float height in heights)
                Assert.True(height >= cardHeight);
        }

        [Fact]
        public void NarrowSafeAreaConstrainsCardWidthAndPosition()
        {
            ToastTheme theme = CreateTheme();
            theme.Anchor = ToastAnchor.TopRight;

            ToastLayout layout = ToastLayoutProvider.Calculate(
                theme,
                new ToastSafeArea(20f, 0f, 320f, 640f),
                360,
                640,
                1f
            );

            Assert.Equal(280f, layout.Width);
            Assert.Equal(40f, layout.CalculateImguiX(theme.Anchor));
        }

        [Fact]
        public void DensityScaleOnlyAppliesToFonts()
        {
            ToastTheme theme = CreateTheme();

            ToastLayout layout = ToastLayoutProvider.Calculate(
                theme,
                new ToastSafeArea(0f, 0f, 1440f, 720f),
                1440,
                720,
                1.5f
            );

            Assert.Equal(theme.Width, layout.Width);
            Assert.Equal(theme.MinimumHeight, layout.MinimumHeight);
            Assert.Equal(24, layout.TitleSize);
            Assert.Equal(20, layout.TextSize);
        }

        [Fact]
        public void FullHdViewportScalesLayoutFromReferenceHeight()
        {
            ToastTheme theme = CreateTheme();

            ToastLayout layout = ToastLayoutProvider.Calculate(
                theme,
                new ToastSafeArea(0f, 0f, 1920f, 1080f),
                1920,
                1080,
                1f
            );

            Assert.Equal(597f, layout.Width);
            Assert.Equal(147f, layout.MinimumHeight);
            Assert.Equal(27, layout.TitleSize);
            Assert.Equal(23, layout.TextSize);
        }

        [Fact]
        public void LandscapeWidthLimitPreventsHalfScreenCards()
        {
            ToastTheme theme = CreateTheme();

            ToastLayout layout = ToastLayoutProvider.Calculate(
                theme,
                new ToastSafeArea(0f, 0f, 1440f, 1080f),
                1440,
                1080,
                1.25f
            );

            Assert.Equal(489f, layout.Width);
            Assert.True(layout.Width < layout.SafeArea.Width * 0.5f);
        }

        [Fact]
        public void ShortViewportReducesVisibleCount()
        {
            ToastTheme theme = CreateTheme();

            ToastLayout layout = ToastLayoutProvider.Calculate(
                theme,
                new ToastSafeArea(0f, 0f, 640f, 300f),
                640,
                300,
                1f
            );

            Assert.Equal(2, layout.MaximumVisible);
        }

        [Fact]
        public void NearSquareOrientationChangeDoesNotCauseWidthJump()
        {
            ToastTheme theme = CreateTheme();
            ToastLayout portrait = ToastLayoutProvider.Calculate(
                theme,
                new ToastSafeArea(0f, 0f, 999f, 1000f),
                999,
                1000,
                1f
            );
            ToastLayout landscape = ToastLayoutProvider.Calculate(
                theme,
                new ToastSafeArea(0f, 0f, 1000f, 999f),
                1000,
                999,
                1f
            );

            Assert.InRange(Math.Abs(portrait.Width - landscape.Width), 0f, 2f);
        }

        [Fact]
        public void MarginIsConstrainedByTheShortestSafeAreaSide()
        {
            ToastTheme theme = CreateTheme();
            theme.Margin = 1000f;

            ToastLayout layout = ToastLayoutProvider.Calculate(
                theme,
                new ToastSafeArea(0f, 0f, 640f, 300f),
                640,
                300,
                1f
            );

            Assert.Equal(149.5f, layout.Margin);
            Assert.InRange(
                layout.CalculateImguiY(ToastAnchor.TopRight, layout.MinimumHeight),
                0f,
                300f
            );
        }

        [Fact]
        public void HeightsRemainNaturalWhenTheStackFits()
        {
            ToastLayout layout = CreateFullHdLayout();
            float[] heights = { 700f, 148f };

            layout.FitCardHeights(heights, heights.Length);

            Assert.Equal(700f, heights[0]);
            Assert.Equal(148f, heights[1]);
        }

        [Fact]
        public void OverflowReductionPreservesShortCards()
        {
            ToastLayout layout = CreateFullHdLayout();
            float[] heights = { 900f, 900f, 148f };

            layout.FitCardHeights(heights, heights.Length);

            Assert.Equal(148f, heights[2]);
            Assert.True(heights[0] < 900f);
            Assert.True(heights[1] < 900f);
            float total = heights[0] + heights[1] + heights[2] + layout.Gap * 2f;
            Assert.True(total <= layout.AvailableHeight + 0.01f);
        }

        [Fact]
        public void SharedMetricsDefineMatchingInsetsAndResponsiveTitleGap()
        {
            ToastTheme theme = CreateTheme();
            ToastLayout mobile = ToastLayoutProvider.Calculate(
                theme,
                new ToastSafeArea(0f, 0f, 1440f, 720f),
                1440,
                720,
                1.25f
            );
            ToastLayout fullHd = CreateFullHdLayout();

            Assert.Equal(12f, mobile.ContentInset);
            Assert.Equal(mobile.Width - 24f, ToastMetrics.ContentWidth(mobile));
            Assert.Equal(8f, mobile.VerticalInset);
            Assert.Equal(3f, ToastMetrics.CalculateTitleBodyGap(mobile));
            Assert.Equal(47f, ToastMetrics.CalculateMessageTop(mobile));
            Assert.Equal(3f, ToastMetrics.CalculateTitleBodyGap(fullHd));
            Assert.True(ToastMetrics.CalculateTitleHeight(fullHd) >= fullHd.TitleSize * 1.5f);
        }

        [Fact]
        public void RoundedBandsMatchTheImguiCardGeometry()
        {
            float[] expectedTops = { 0f, 2f, 4f, 6f, 99f, 101f, 103f };
            float[] expectedHeights = { 2f, 2f, 2f, 93f, 2f, 2f, 2f };
            float[] expectedInsets = { 6f, 3f, 1f, 0f, 1f, 3f, 6f };

            Assert.Equal(7, ToastMetrics.RoundedBandCount);
            for (int i = 0; i < ToastMetrics.RoundedBandCount; i++)
            {
                ToastMetrics.GetRoundedBand(
                    425f,
                    105f,
                    i,
                    out float top,
                    out float height,
                    out float inset
                );
                Assert.Equal(expectedTops[i], top);
                Assert.Equal(expectedHeights[i], height);
                Assert.Equal(expectedInsets[i], inset);
            }
        }

        [Theory]
        [InlineData(0.5f)]
        [InlineData(1f)]
        [InlineData(2.25f)]
        [InlineData(3f)]
        public void ScaledBackgroundBandsCoverTheCardWithoutAlphaOverlaps(float scale)
        {
            float width = 425f * scale;
            float height = 105f * scale;
            float previousEnd = 0f;
            for (int i = 0; i < ToastMetrics.RoundedBandCount; i++)
            {
                ToastMetrics.GetRoundedBand(
                    width,
                    height,
                    i,
                    out float top,
                    out float bandHeight,
                    out float inset,
                    scale
                );
                Assert.Equal(previousEnd, top);
                Assert.InRange(inset, 0f, width * 0.5f);
                Assert.True(bandHeight >= 0f);
                previousEnd = top + bandHeight;
            }
            Assert.Equal(height, previousEnd);
        }

        private static ToastLayout CreateFullHdLayout()
        {
            ToastTheme theme = CreateTheme();
            return ToastLayoutProvider.Calculate(
                theme,
                new ToastSafeArea(0f, 0f, 1920f, 1080f),
                1920,
                1080,
                1f
            );
        }

        private static ToastTheme CreateTheme()
        {
            var theme = (ToastTheme)RuntimeHelpers.GetUninitializedObject(typeof(ToastTheme));
            theme.Width = 425f;
            theme.MinimumHeight = 105f;
            theme.Margin = 20f;
            theme.Gap = 15f;
            theme.MaximumVisible = 4;
            theme.TitleSize = 19;
            theme.TextSize = 16;
            theme.Anchor = ToastAnchor.BottomRight;
            return theme;
        }
    }
}
