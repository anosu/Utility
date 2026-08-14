using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Utility.Fonts;
using Utility.Toast;
using Xunit;

namespace Utility.Tests
{
    public sealed class UtilityTests
    {
        [Fact]
        public void CapacityIsEnforced()
        {
            var buffer = new ToastCommandBuffer();

            for (int i = 0; i < ToastRuntime.Capacity; i++)
                Assert.True(buffer.EnqueueShow("title", i.ToString(), 0, 1f));

            Assert.False(buffer.EnqueueShow("title", "overflow", 0, 1f));
            Assert.Equal(ToastRuntime.Capacity, buffer.Count);

            buffer.RemoveExpiredItem();
            Assert.Equal(ToastRuntime.Capacity - 1, buffer.Count);
        }

        [Fact]
        public void CommandsRetainTheirOrder()
        {
            var buffer = new ToastCommandBuffer();
            var kinds = new List<ToastCommandKind>();

            buffer.EnqueueShow("old", "old", 0, 1f);
            buffer.EnqueueClear();
            buffer.EnqueueShow("new", "new", 0, 1f);
            buffer.RunExclusive(commands =>
            {
                while (commands.Count > 0)
                    kinds.Add(commands.Dequeue().Kind);
            });

            Assert.Equal(1, buffer.Count);
            Assert.Equal(
                new[] { ToastCommandKind.Show, ToastCommandKind.Clear, ToastCommandKind.Show },
                kinds
            );
        }

        [Fact]
        public void ConfigurationRetainsItsPosition()
        {
            var buffer = new ToastCommandBuffer();
            var kinds = new List<ToastCommandKind>();

            buffer.EnqueueShow("first", "first", 0, 1f);
            buffer.EnqueueConfigure(
                new ToastConfiguration(null, null, null, null, 0, null, null, null)
            );
            buffer.EnqueueShow("second", "second", 0, 1f);
            buffer.RunExclusive(commands =>
            {
                while (commands.Count > 0)
                    kinds.Add(commands.Dequeue().Kind);
            });

            Assert.Equal(ToastCommandKind.Configure, kinds[1]);
        }

        [Fact]
        public void DurationIsClamped()
        {
            var data = new ToastData("title", "message", ToastType.Info, -10f);
            Assert.Equal(1f, data.Duration);
        }

        [Fact]
        public void PrefixVariantsSatisfyTheSharedContract()
        {
            Type bepinexVariant = typeof(FontHelper<TMPro.TMP_FontAsset>);
            Type melonVariant = typeof(FontHelper<Il2CppTMPro.TMP_FontAsset>);

            Assert.True(bepinexVariant.IsGenericType);
            Assert.True(melonVariant.IsGenericType);
            Assert.NotNull(bepinexVariant.GetConstructor(new[] { typeof(string) }));
        }

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
        ) => Assert.False(HasMemberReference(ownerNamespace, ownerName, memberName));

        [Fact]
        public void Il2CppArrayEnumeratorIsNotReferenced() =>
            Assert.False(HasMemberReferenceNamed("GetEnumerator"));

        [Fact]
        public void ToastRendererSetsExplicitStyleTextColor() =>
            Assert.True(HasMemberReference("UnityEngine", "GUIStyleState", "set_textColor"));

        [Fact]
        public void FontHelperUsesExplicitTypeEnumerationWithAsyncFallback()
        {
            Assert.True(HasMemberReference("UnityEngine", "AssetBundle", "LoadAllAssets", 1));
            Assert.False(HasMemberReference("UnityEngine", "AssetBundle", "LoadAllAssets", 0));
            Assert.True(HasMemberReference("UnityEngine", "AssetBundle", "LoadAllAssetsAsync", 1));
            Assert.False(HasMemberReference("UnityEngine", "AssetBundle", "LoadAllAssetsAsync", 0));
        }

        [Theory]
        [InlineData("LoadFromFile")]
        [InlineData("LoadFromFileAsync")]
        [InlineData("LoadAsset")]
        [InlineData("LoadAssetAsync")]
        public void FontHelperRetainsFileAndNamedAssetFallbacks(string memberName) =>
            Assert.True(HasMemberReference("UnityEngine", "AssetBundle", memberName));

        private static bool HasMemberReference(
            string ownerNamespace,
            string ownerName,
            string memberName,
            int? parameterCount = null
        )
        {
            using FileStream stream = File.OpenRead(typeof(Utility.Toast.Toast).Assembly.Location);
            using var peReader = new PEReader(stream);
            MetadataReader metadata = peReader.GetMetadataReader();

            foreach (MemberReferenceHandle handle in metadata.MemberReferences)
            {
                MemberReference member = metadata.GetMemberReference(handle);
                if (metadata.GetString(member.Name) != memberName)
                    continue;

                if (member.Parent.Kind != HandleKind.TypeReference)
                    continue;

                TypeReference owner = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
                if (
                    metadata.GetString(owner.Namespace) == ownerNamespace
                    && metadata.GetString(owner.Name) == ownerName
                )
                {
                    if (parameterCount == null)
                        return true;

                    BlobReader signature = metadata.GetBlobReader(member.Signature);
                    SignatureHeader header = signature.ReadSignatureHeader();
                    if (header.Kind != SignatureKind.Method)
                        continue;
                    if (header.IsGeneric)
                        signature.ReadCompressedInteger();

                    if (signature.ReadCompressedInteger() == parameterCount.Value)
                        return true;
                }
            }

            return false;
        }

        private static bool HasMemberReferenceNamed(string memberName)
        {
            using FileStream stream = File.OpenRead(typeof(Utility.Toast.Toast).Assembly.Location);
            using var peReader = new PEReader(stream);
            MetadataReader metadata = peReader.GetMetadataReader();

            foreach (MemberReferenceHandle handle in metadata.MemberReferences)
            {
                MemberReference member = metadata.GetMemberReference(handle);
                if (metadata.GetString(member.Name) == memberName)
                    return true;
            }

            return false;
        }
    }
}
