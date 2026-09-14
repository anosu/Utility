using System.Globalization;
using Utility.Notifications;
using Utility.Notifications.Internal;
using Xunit;

namespace Utility.Tests.Notifications
{
    public sealed class ToastTitleTests
    {
        [Theory]
        [InlineData("abcdef", 4f, "abc…")]
        [InlineData("abc", 3f, "abc")]
        [InlineData("a\r\nb", 3f, "a b")]
        [InlineData("e\u0301🙂xyz", 3f, "e\u0301🙂…")]
        [InlineData("👩‍👩‍👧‍👦AB", 2f, "👩‍👩‍👧‍👦…")]
        [InlineData("标题", 0.5f, "")]
        public void TitlesStayOnOneLineWithoutSplittingTextElements(
            string text,
            float width,
            string expected
        )
        {
            string result = ToastTextMetrics.FitTitle(text, width, Measure);
            Assert.Equal(expected, result);
            Assert.True(Measure(result) <= width);
        }

        [Fact]
        public void CachedTitleIsRemeasuredAfterWidthSizeOrRendererChanges()
        {
            var item = new ToastItem("abcdef", "message", ToastKind.Info, 3f);
            var owner = new object();
            int measurements = 0;
            float CountedMeasure(string value)
            {
                measurements++;
                return Measure(value);
            }

            Assert.Equal("ab…", item.GetDisplayTitle(3f, 16, owner, CountedMeasure));
            int cachedCount = measurements;
            Assert.Equal("ab…", item.GetDisplayTitle(3f, 16, owner, CountedMeasure));
            Assert.Equal(cachedCount, measurements);
            Assert.Equal("abcdef", item.GetDisplayTitle(6f, 16, owner, CountedMeasure));
            Assert.Equal(cachedCount + 1, measurements);
            item.GetDisplayTitle(6f, 18, owner, CountedMeasure);
            Assert.Equal(cachedCount + 2, measurements);
            item.GetDisplayTitle(6f, 18, new object(), CountedMeasure);
            Assert.Equal(cachedCount + 3, measurements);
        }

        private static float Measure(string text) =>
            StringInfo.ParseCombiningCharacters(text).Length;
    }
}
