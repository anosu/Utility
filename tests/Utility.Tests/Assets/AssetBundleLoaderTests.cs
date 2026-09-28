using System;
using System.Collections;
using System.IO;
using Utility.Assets;
using Xunit;

namespace Utility.Tests.Assets
{
    public sealed class AssetBundleLoaderTests
    {
        [Fact]
        public void LoadReportsTerminalErrorsThroughOptionalCallback()
        {
            string missingBundlePath = Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid():N}.bundle"
            );
            var loader = new AssetBundleLoader<UnityEngine.Object>(missingBundlePath);
            Exception? reported = null;
            bool completed = false;

            Exception? escaped = Record.Exception(() =>
                Drain(loader.Load(() => completed = true, exception => reported = exception))
            );

            Assert.Null(escaped);
            Assert.False(completed);
            Assert.IsType<FileNotFoundException>(reported);
            Assert.Same(reported, loader.LastError);
            Assert.False(loader.IsLoading);
        }

        [Fact]
        public void LoadStillThrowsTerminalErrorsWithoutCallback()
        {
            string missingBundlePath = Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid():N}.bundle"
            );
            var loader = new AssetBundleLoader<UnityEngine.Object>(missingBundlePath);

            var exception = Assert.Throws<FileNotFoundException>(() => Drain(loader.Load()));

            Assert.Same(exception, loader.LastError);
        }

        private static void Drain(IEnumerator operation)
        {
            while (operation.MoveNext()) { }
        }
    }
}
