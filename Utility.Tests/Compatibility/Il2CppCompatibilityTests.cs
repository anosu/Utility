using System.Reflection;
using Utility.Notifications;
using Utility.Notifications.Internal;
using Xunit;

namespace Utility.Tests.Compatibility
{
    public sealed class Il2CppCompatibilityTests
    {
        [Theory]
        [InlineData("UnityEngine", "GUIStyleState", "set_background")]
        [InlineData("UnityEngine", "GUI", "DrawTextureWithTexCoords")]
        [InlineData("UnityEngine", "AssetBundle", "GetAllAssetNames")]
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
            Assert.False(AssemblyMetadata.HasMemberReferenceNamed("GetEnumerator"));

        [Fact]
        public void SpanPinningIsNotReferenced() =>
            Assert.False(AssemblyMetadata.HasMemberReferenceNamed("GetPinnableReference"));

        [Fact]
        public void ImguiRendererSetsExplicitTextColor() =>
            Assert.True(
                AssemblyMetadata.HasMemberReference("UnityEngine", "GUIStyleState", "set_textColor")
            );

        [Theory]
        [InlineData("UnityEngine.UI")]
        [InlineData("UnityEngine.UIModule")]
        [InlineData("TMPro")]
        [InlineData("Il2CppTMPro")]
        public void CoreAssemblyHasNoHardOptionalDependency(string assemblyName) =>
            Assert.False(AssemblyMetadata.HasAssemblyReference(assemblyName));

        [Fact]
        public void ImguiFailureRetainsOptionalUguiFallback()
        {
            Assert.NotNull(
                typeof(ToastRuntime).GetMethod(
                    "QueueUguiFallback",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
            );
            Assert.NotNull(
                typeof(ToastRuntime).GetMethod(
                    "ActivateUguiFallback",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
            );
            Assert.NotNull(
                typeof(OptionalRendererLoader).GetMethod(
                    "TryCreateUgui",
                    BindingFlags.Static | BindingFlags.NonPublic
                )
            );
        }

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

        [Theory]
        [InlineData("LoadFromFile")]
        [InlineData("LoadFromFileAsync")]
        [InlineData("LoadAsset")]
        [InlineData("LoadAssetAsync")]
        public void AssetLoaderRetainsFileAndNamedAssetFallbacks(string memberName) =>
            Assert.True(
                AssemblyMetadata.HasMemberReference("UnityEngine", "AssetBundle", memberName)
            );
    }
}
