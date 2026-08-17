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
        public void PrefixVariantsSatisfyTheSharedContract()
        {
            Type bepinexVariant = typeof(AssetBundleLoader<TMPro.TMP_FontAsset>);
            Type melonVariant = typeof(AssetBundleLoader<Il2CppTMPro.TMP_FontAsset>);

            Assert.True(bepinexVariant.IsGenericType);
            Assert.True(melonVariant.IsGenericType);
            Assert.NotNull(bepinexVariant.GetConstructor(new[] { typeof(string) }));
        }

        [Fact]
        public void CoroutineInterfaceUsesAccurateNames()
        {
            Type loader = typeof(AssetBundleLoader<TMPro.TMP_FontAsset>);

            Assert.NotNull(loader.GetMethod("Load"));
            Assert.Null(loader.GetMethod("LoadAsync"));
            Assert.NotNull(loader.GetProperty("IsLoading"));
            Assert.NotNull(loader.GetProperty("IsLoaded"));
        }

        [Fact]
        public void LoadReportsTerminalErrorsThroughOptionalCallback()
        {
            string missingBundlePath = Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid():N}.bundle"
            );
            var loader = new AssetBundleLoader<TMPro.TMP_FontAsset>(missingBundlePath);
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
            var loader = new AssetBundleLoader<TMPro.TMP_FontAsset>(missingBundlePath);

            var exception = Assert.Throws<FileNotFoundException>(() => Drain(loader.Load()));

            Assert.Same(exception, loader.LastError);
        }

        private static void Drain(IEnumerator operation)
        {
            while (operation.MoveNext()) { }
        }
    }
}
