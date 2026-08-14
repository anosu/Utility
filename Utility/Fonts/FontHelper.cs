using System;
using System.Collections;
using System.IO;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Utility.Fonts
{
    /// <summary>Loads an IL2CPP Unity asset without binding the utility assembly to its concrete namespace.</summary>
    /// <typeparam name="TAsset">The generated wrapper type expected by the current loader.</typeparam>
    public class FontHelper<TAsset>
        where TAsset : UnityObject
    {
        private readonly string? _assetName;
        private readonly string _bundlePath;

        /// <summary>Initializes a new instance that scans the bundle for the first compatible asset.</summary>
        /// <param name="bundlePath">The absolute or game-relative path to the asset bundle.</param>
        /// <remarks>
        /// This overload preserves automatic discovery using the explicit-type synchronous and
        /// asynchronous AssetBundle APIs. Prefer the asset-name overload on heavily stripped games.
        /// </remarks>
        public FontHelper(string bundlePath)
        {
            if (string.IsNullOrWhiteSpace(bundlePath))
                throw new ArgumentException("Bundle path cannot be empty.", nameof(bundlePath));

            _bundlePath = bundlePath;
        }

        /// <summary>Initializes a new instance that loads an asset by its exact bundle name.</summary>
        /// <param name="bundlePath">The absolute or game-relative path to the asset bundle.</param>
        /// <param name="assetName">The exact asset name stored in the bundle.</param>
        public FontHelper(string bundlePath, string assetName)
            : this(bundlePath)
        {
            if (string.IsNullOrWhiteSpace(assetName))
                throw new ArgumentException("Asset name cannot be empty.", nameof(assetName));

            _assetName = assetName;
        }

        /// <summary>Gets the loaded asset.</summary>
        public TAsset? Asset { get; private set; }

        /// <summary>Gets a value that indicates whether a load operation is active.</summary>
        public bool Loading { get; private set; }

        /// <summary>Gets a value that indicates whether an asset wrapper has been loaded.</summary>
        /// <remarks>This property deliberately avoids invoking Unity's native object operators.</remarks>
        public bool Valid => !ReferenceEquals(Asset, null);

        /// <summary>Gets the last recoverable exception encountered while probing load paths.</summary>
        public Exception? LastError { get; private set; }

        /// <summary>Loads the configured asset and keeps it alive after unloading the bundle.</summary>
        /// <param name="onComplete">An optional callback invoked after a successful load.</param>
        /// <returns>An enumerator suitable for a Unity coroutine.</returns>
        public IEnumerator LoadAsync(Action? onComplete = null)
        {
            if (Valid)
            {
                onComplete?.Invoke();
                yield break;
            }

            if (Loading)
                yield break;

            EnsureBundleExists();
            Loading = true;
            LastError = null;
            AssetBundle? bundle = null;

            try
            {
                bundle = TryLoadBundle();
                if (ReferenceEquals(bundle, null))
                {
                    AssetBundleCreateRequest? request = null;
                    try
                    {
                        request = AssetBundle.LoadFromFileAsync(_bundlePath);
                    }
                    catch (Exception exception)
                    {
                        LastError = exception;
                    }

                    if (ReferenceEquals(request, null))
                    {
                        throw new InvalidOperationException(
                            "AssetBundle.LoadFromFile is unavailable or failed.",
                            LastError
                        );
                    }

                    yield return request;
                    bundle = request.assetBundle;
                }

                if (ReferenceEquals(bundle, null))
                    throw new InvalidOperationException(
                        $"Failed to load asset bundle: {_bundlePath}"
                    );

                Asset = TryLoadAsset(bundle);
                if (!Valid)
                {
                    AssetBundleRequest? request = TryLoadAssetAsync(bundle);
                    if (!ReferenceEquals(request, null))
                    {
                        yield return request;
                        Asset = TryGetRequestedAsset(request);
                    }
                }

                if (!Valid)
                {
                    string message = CreateLoadFailureMessage();
                    throw new InvalidOperationException(message, LastError);
                }

                Asset!.hideFlags = HideFlags.HideAndDontSave;
                UnityObject.DontDestroyOnLoad(Asset);
                LastError = null;
                onComplete?.Invoke();
            }
            finally
            {
                if (!ReferenceEquals(bundle, null))
                    TryUnloadBundle(bundle, unloadLoadedObjects: !Valid);

                Loading = false;
            }
        }

        private TAsset? FindFirstCompatible(Il2CppReferenceArray<UnityObject>? assets)
        {
            if (assets == null)
                return null;

            for (int i = 0; i < assets.Length; i++)
            {
                UnityObject? candidate = assets[i];
                TAsset? asset = TryCast(candidate);
                if (!ReferenceEquals(asset, null))
                    return asset;
            }

            return null;
        }

        private TAsset? TryCast(UnityObject? candidate)
        {
            if (ReferenceEquals(candidate, null))
                return null;

            try
            {
                return candidate.TryCast<TAsset>();
            }
            catch (Exception exception)
            {
                LastError = exception;
                return null;
            }
        }

        private TAsset? TryLoadAsset(AssetBundle bundle)
        {
            if (_assetName != null)
            {
                try
                {
                    return TryCast(bundle.LoadAsset(_assetName));
                }
                catch (Exception exception)
                {
                    LastError = exception;
                    return null;
                }
            }

            try
            {
                return FindFirstCompatible(bundle.LoadAllAssets(Il2CppType.Of<UnityObject>()));
            }
            catch (Exception exception)
            {
                LastError = exception;
                return null;
            }
        }

        private AssetBundleRequest? TryLoadAssetAsync(AssetBundle bundle)
        {
            try
            {
                return _assetName == null
                    ? bundle.LoadAllAssetsAsync(Il2CppType.Of<UnityObject>())
                    : bundle.LoadAssetAsync(_assetName);
            }
            catch (Exception exception)
            {
                RecordError(exception);
                return null;
            }
        }

        private TAsset? TryGetRequestedAsset(AssetBundleRequest request)
        {
            try
            {
                return _assetName == null
                    ? FindFirstCompatible(request.allAssets)
                    : TryCast(request.asset);
            }
            catch (Exception exception)
            {
                RecordError(exception);
                return null;
            }
        }

        private AssetBundle? TryLoadBundle()
        {
            try
            {
                return AssetBundle.LoadFromFile(_bundlePath);
            }
            catch (Exception exception)
            {
                LastError = exception;
                return null;
            }
        }

        private void TryUnloadBundle(AssetBundle bundle, bool unloadLoadedObjects)
        {
            try
            {
                bundle.Unload(unloadLoadedObjects);
            }
            catch (Exception exception)
            {
                LastError ??= exception;
            }
        }

        private void EnsureBundleExists()
        {
            if (!File.Exists(_bundlePath))
                throw new FileNotFoundException("Asset bundle was not found.", _bundlePath);
        }

        private string CreateLoadFailureMessage()
        {
            string message =
                _assetName == null
                    ? $"The bundle contains no asset compatible with {typeof(TAsset).FullName}. Both explicit-type AssetBundle enumeration paths were attempted; use the asset-name constructor when enumeration is stripped."
                    : $"Asset '{_assetName}' is missing or is not compatible with {typeof(TAsset).FullName}.";

            return LastError == null ? message : $"{message}{Environment.NewLine}{LastError}";
        }

        private void RecordError(Exception exception)
        {
            LastError =
                LastError == null
                    ? exception
                    : new AggregateException(
                        "Both synchronous and asynchronous asset-loading paths failed.",
                        LastError,
                        exception
                    );
        }
    }

    /// <summary>Loads an asset as <see cref="UnityObject"/> when a concrete generated wrapper is not required.</summary>
    public sealed class FontHelper : FontHelper<UnityObject>
    {
        /// <summary>Initializes a new instance that scans the bundle for the first asset.</summary>
        /// <param name="bundlePath">The absolute or game-relative path to the asset bundle.</param>
        public FontHelper(string bundlePath)
            : base(bundlePath) { }

        /// <summary>Initializes a new instance that loads an asset by its exact bundle name.</summary>
        /// <param name="bundlePath">The absolute or game-relative path to the asset bundle.</param>
        /// <param name="assetName">The exact asset name stored in the bundle.</param>
        public FontHelper(string bundlePath, string assetName)
            : base(bundlePath, assetName) { }
    }
}
