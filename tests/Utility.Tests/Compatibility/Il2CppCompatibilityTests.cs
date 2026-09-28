using Xunit;

namespace Utility.Tests.Compatibility
{
    public sealed class Il2CppCompatibilityTests
    {
        [Theory]
        [InlineData("UnityEngine", "GUI", "DrawTextureWithTexCoords")]
        [InlineData("UnityEngine", "AssetBundle", "GetAllAssetNames")]
        [InlineData("UnityEngine", "Texture2D", "SetPixel")]
        [InlineData("UnityEngine", "Texture2D", "Apply")]
        [InlineData("UnityEngine", "Object", "op_Implicit")]
        [InlineData("UnityEngine", "Object", "op_Equality")]
        [InlineData("UnityEngine", "Object", "op_Inequality")]
        public void DangerousUnityMemberIsNotReferenced(
            string ownerNamespace,
            string ownerName,
            string memberName
        ) =>
            Assert.False(
                AssemblyMetadata.HasMemberReference(ownerNamespace, ownerName, memberName)
            );

        [Fact]
        public void Il2CppArrayEnumeratorIsNotReferenced() =>
            Assert.False(AssemblyMetadata.HasIl2CppMemberReferenceNamed("GetEnumerator"));

        [Fact]
        public void SpanPinningIsNotReferenced() =>
            Assert.False(AssemblyMetadata.HasMemberReferenceNamed("GetPinnableReference"));

        [Fact]
        public void TypographyFailureDoesNotFallBackToGameSkinLabels() =>
            Assert.False(AssemblyMetadata.HasMemberReference("UnityEngine", "GUI", "Label", 2));

        [Theory]
        [InlineData("TMPro")]
        [InlineData("Il2CppTMPro")]
        public void CoreAssemblyHasNoTextMeshProDependency(string assemblyName) =>
            Assert.False(AssemblyMetadata.HasAssemblyReference(assemblyName));

        [Fact]
        public void AssetLoaderUsesTypedEnumerationWithAsyncFallback()
        {
            Assert.True(
                AssemblyMetadata.HasMemberReference(
                    "UnityEngine",
                    "AssetBundle",
                    "LoadAllAssets",
                    1
                )
            );
            Assert.False(
                AssemblyMetadata.HasMemberReference(
                    "UnityEngine",
                    "AssetBundle",
                    "LoadAllAssets",
                    0
                )
            );
            Assert.True(
                AssemblyMetadata.HasMemberReference(
                    "UnityEngine",
                    "AssetBundle",
                    "LoadAllAssetsAsync",
                    1
                )
            );
            Assert.False(
                AssemblyMetadata.HasMemberReference(
                    "UnityEngine",
                    "AssetBundle",
                    "LoadAllAssetsAsync",
                    0
                )
            );
        }
    }
}
