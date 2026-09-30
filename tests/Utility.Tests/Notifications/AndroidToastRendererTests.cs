using Utility.Notifications.Internal;
using Xunit;

namespace Utility.Tests.Notifications
{
    public sealed class AndroidToastRendererTests
    {
        [Fact]
        public void EmbeddedAndroidBridgeIsDistributedWithUtility()
        {
            var assembly = typeof(AndroidToastRenderer).Assembly;
            Assert.Equal("dex\n", ReadPrefix("Utility.Toast.classes.dex", 4));
            Assert.Equal("\u007fELF", ReadPrefix("Utility.Toast.arm64.so", 4));
            Assert.Equal("\u007fELF", ReadPrefix("Utility.Toast.arm.so", 4));

            string ReadPrefix(string name, int length)
            {
                using var stream = assembly.GetManifestResourceStream(name);
                Assert.NotNull(stream);
                var bytes = new byte[length];
                Assert.Equal(length, stream.Read(bytes, 0, length));
                return System.Text.Encoding.ASCII.GetString(bytes);
            }
        }
    }
}
