using System;
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
    }
}
