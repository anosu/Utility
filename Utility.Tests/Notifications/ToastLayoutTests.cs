using System;
using System.Runtime.CompilerServices;
using Utility.Notifications;
using Utility.Notifications.Internal;
using Xunit;

namespace Utility.Tests.Notifications
{
    public sealed class ToastLayoutTests
    {
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

            Assert.Equal(18f, ToastMetrics.ContentLeft);
            Assert.Equal(395f, ToastMetrics.ContentWidth(425f));
            Assert.Equal(3f, ToastMetrics.StretchedContentOffsetX);
            Assert.Equal(-30f, ToastMetrics.StretchedContentWidthDelta);
            Assert.Equal(37f, ToastMetrics.CompatibilityMessageTop);
            Assert.Equal(18f, ToastMetrics.CompatibilityTitleHeight);
            Assert.Equal(6f, ToastMetrics.CalculateTitleBodyGap(mobile));
            Assert.Equal(49f, ToastMetrics.CalculateMessageTop(mobile));
            Assert.Equal(7f, ToastMetrics.CalculateTitleBodyGap(fullHd));
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
