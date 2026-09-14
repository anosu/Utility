using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Utility.Assets;
using Utility.Notifications;
using Utility.Notifications.Internal;
using Xunit;
using UnityObject = UnityEngine.Object;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Utility.Lifecycle.Tests
{
    public sealed class LifecycleTests : IDisposable
    {
        private readonly string _bundlePath = Path.GetTempFileName();

        public LifecycleTests()
        {
            AssetBundle.SyncResult = null;
            AssetBundle.AsyncResult = null;
        }

        [Fact]
        public void CancelDuringBundleRequestUnloadsItsResultWithoutPublishingSuccess()
        {
            var bundle = new AssetBundle { Candidate = new UnityObject() };
            var request = new AssetBundleCreateRequest { Result = bundle };
            AssetBundle.AsyncResult = request;
            var loader = new AssetBundleLoader<UnityObject>(_bundlePath);
            bool completed = false;
            IEnumerator operation = loader.Load(() => completed = true);

            Assert.True(operation.MoveNext());
            Assert.Same(request, operation.Current);
            ((IDisposable)operation).Dispose();

            Assert.Equal(new[] { true }, bundle.Unloads);
            Assert.Equal(1, request.ResultReads);
            Assert.False(loader.IsLoaded);
            Assert.False(loader.IsLoading);
            Assert.False(completed);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PersistenceFailureDoesNotPublishAnAssetAndCanBeRetried(bool asynchronous)
        {
            var asset = new UnityObject { FailPersistence = true };
            var bundle = new AssetBundle
            {
                Candidate = asynchronous ? null : asset,
                AssetRequest = new AssetBundleRequest
                {
                    allAssets =
                        new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<UnityObject>(
                            asset
                        ),
                },
            };
            AssetBundle.SyncResult = bundle;
            var loader = new AssetBundleLoader<UnityObject>(_bundlePath);
            Exception? reported = null;
            bool completed = false;

            Drain(loader.Load(() => completed = true, error => reported = error));

            Assert.IsType<InvalidOperationException>(reported);
            Assert.False(loader.IsLoaded);
            Assert.False(loader.IsLoading);
            Assert.False(completed);
            Assert.Equal(new[] { true }, bundle.Unloads);

            var replacement = new UnityObject();
            var retryBundle = new AssetBundle { Candidate = replacement };
            AssetBundle.SyncResult = retryBundle;
            Drain(loader.Load(() => completed = true));

            Assert.True(completed);
            Assert.Same(replacement, loader.Asset);
            Assert.Null(loader.LastError);
            Assert.Equal(new[] { false }, retryBundle.Unloads);
        }

        [Fact]
        public void CancelDuringAssetRequestUnloadsTheOwnedBundle()
        {
            var request = new AssetBundleRequest();
            var bundle = new AssetBundle { AssetRequest = request };
            AssetBundle.SyncResult = bundle;
            var loader = new AssetBundleLoader<UnityObject>(_bundlePath);
            IEnumerator operation = loader.Load();

            Assert.True(operation.MoveNext());
            Assert.Same(request, operation.Current);
            ((IDisposable)operation).Dispose();

            Assert.Equal(new[] { true }, bundle.Unloads);
            Assert.False(loader.IsLoaded);
            Assert.False(loader.IsLoading);
        }

        [Fact]
        public void CompletedBundleRequestKeepsThePublishedAssetAndUnloadsOnce()
        {
            var asset = new UnityObject();
            var bundle = new AssetBundle { Candidate = asset };
            var request = new AssetBundleCreateRequest { Result = bundle };
            AssetBundle.AsyncResult = request;
            var loader = new AssetBundleLoader<UnityObject>(_bundlePath);
            int completions = 0;
            IEnumerator operation = loader.Load(() => completions++);

            Assert.True(operation.MoveNext());
            Assert.False(operation.MoveNext());
            ((IDisposable)operation).Dispose();

            Assert.Same(asset, loader.Asset);
            Assert.Equal(new[] { false }, bundle.Unloads);
            Assert.Equal(1, request.ResultReads);
            Assert.Equal(1, completions);
            Assert.False(loader.IsLoading);
        }

        [Fact]
        public void ImmediateReinitializationIgnoresOldCallbacksAndDelayedDestruction()
        {
            Toast.Initialize();
            ToastBehaviour old = ToastBehaviour.Instance!;
            Toast.Shutdown();
            Assert.False(Toast.IsInitialized);
            Toast.Initialize();
            ToastBehaviour current = ToastBehaviour.Instance!;
            Assert.NotSame(old, current);
            int detached = ToastRuntime.Shared.DetachCount;
            int frames = ToastRuntime.Shared.FrameCount;
            int renders = ToastRuntime.Shared.RenderCount;

            old.InvokeMessage("Update");
            old.InvokeMessage("OnGUI");
            UnityObject.FlushDestroy();

            Assert.Same(current, ToastBehaviour.Instance);
            Assert.Equal(detached, ToastRuntime.Shared.DetachCount);
            Assert.Equal(frames, ToastRuntime.Shared.FrameCount);
            Assert.Equal(renders, ToastRuntime.Shared.RenderCount);
            current.InvokeMessage("Update");
            current.InvokeMessage("OnGUI");
            Assert.Equal(frames + 1, ToastRuntime.Shared.FrameCount);
            Assert.Equal(renders + 1, ToastRuntime.Shared.RenderCount);
        }

        private static void Drain(IEnumerator operation)
        {
            while (operation.MoveNext()) { }
        }

        public void Dispose()
        {
            Toast.Shutdown();
            UnityObject.FlushDestroy();
            File.Delete(_bundlePath);
        }
    }
}
