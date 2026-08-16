using Utility.Notifications.Internal;
using Xunit;

namespace Utility.Tests.Notifications
{
    public sealed class ToastTextMetricsTests
    {
        [Fact]
        public void WideCharactersRequireMoreLinesThanAscii()
        {
            int asciiLines = ToastTextMetrics.EstimateLineCount(
                "abcdefghijklmnopqrst",
                100f,
                20
            );
            int wideLines = ToastTextMetrics.EstimateLineCount(
                "这是用于测试中文字符换行高度估算的消息内容",
                100f,
                20
            );

            Assert.True(wideLines > asciiLines);
        }

        [Fact]
        public void CrLfCountsAsOneLineBreak() =>
            Assert.Equal(2, ToastTextMetrics.EstimateLineCount("first\r\nsecond", 500f, 20));

        [Fact]
        public void SurrogatePairIsMeasuredAsOneGlyph() =>
            Assert.Equal(1, ToastTextMetrics.EstimateLineCount("\ud83d\ude00", 20f, 20));
    }
}
