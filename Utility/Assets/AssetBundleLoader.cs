using System;
using System.Collections;
using System.IO;
using System.Runtime.ExceptionServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using Utility.Diagnostics;
using UnityObject = UnityEngine.Object;

namespace Utility.Assets
{
    /// <summary>Loads an IL2CPP Unity asset without binding the utility assembly to its concrete namespace.</summary>
    /// <typeparam name="TAsset">The generated wrapper type expected by the current loader.</typeparam>
    public sealed class AssetBundleLoader<TAsset>
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
        public AssetBundleLoader(string bundlePath)
        {
            if (string.IsNullOrWhiteSpace(bundlePath))
                throw new ArgumentException("Bundle path cannot be empty.", nameof(bundlePath));

            _bundlePath = bundlePath;
        }

        /// <summary>Initializes a new instance that loads an asset by its exact bundle name.</summary>
        /// <param name="bundlePath">The absolute or game-relative path to the asset bundle.</param>
        /// <param name="assetName">The exact asset name stored in the bundle.</param>
        public AssetBundleLoader(string bundlePath, string assetName)
            : this(bundlePath)
        {
            if (string.IsNullOrWhiteSpace(assetName))
                throw new ArgumentException("Asset name cannot be empty.", nameof(assetName));

            _assetName = assetName;
        }

        /// <summary>Gets the loaded asset.</summary>
        public TAsset? Asset { get; private set; }

        /// <summary>Gets a value that indicates whether a load operation is active.</summary>
        public bool IsLoading { get; private set; }

        /// <summary>Gets a value that indicates whether an asset wrapper has been loaded.</summary>
        /// <remarks>This property deliberately avoids invoking Unity's native object operators.</remarks>
        public bool IsLoaded => !ReferenceEquals(Asset, null);

        /// <summary>Gets the last exception encountered during a load operation.</summary>
        public Exception? LastError { get; private set; }

        /// <summary>Loads the configured asset and keeps it alive after unloading the bundle.</summary>
        /// <param name="onComplete">An optional callback invoked after a successful load.</param>
        /// <param name="onError">
        /// An optional callback invoked with a terminal load error. When supplied, the error is
        /// reported through the callback instead of escaping the coroutine.
        /// </param>
        /// <returns>An enumerator suitable for a Unity coroutine.</returns>
        public IEnumerator Load(Action? onComplete = null, Action<Exception>? onError = null)
        {
            if (IsLoaded)
            {
                onComplete?.Invoke();
                yield break;
            }

            if (IsLoading)
                yield break;

            IEnumerator operation = LoadCore();
            try
            {
                while (true)
                {
                    bool hasNext;
                    object? current = null;
                    Exception? loadError = null;

                    try
                    {
                        hasNext = operation.MoveNext();
                        if (hasNext)
                            current = operation.Current;
                    }
                    catch (Exception exception)
                    {
                        hasNext = false;
                        loadError = exception;
                    }

                    if (loadError != null)
                    {
                        LastError = loadError;
                        Logging.Write(LogLevel.Error, "Assets", "Asset loading failed.", loadError);

                        if (onError == null)
                            ExceptionDispatchInfo.Capture(loadError).Throw();

                        onError(loadError);
                        yield break;
                    }

                    if (!hasNext)
                        break;

                    yield return current;
                }
            }
            finally
            {
                (operation as IDisposable)?.Dispose();
            }

            onComplete?.Invoke();
        }

        private IEnumerator LoadCore()
        {
            EnsureBundleExists();
            IsLoading = true;
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
                        Logging.WriteRecoverable(
                            LogLevel.Debug,
                            "Assets",
                            "AssetBundle.LoadFromFileAsync failed.",
                            exception
                        );
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
                if (!IsLoaded)
                {
                    AssetBundleRequest? request = TryLoadAssetAsync(bundle);
                    if (!ReferenceEquals(request, null))
                    {
                        yield return request;
                        Asset = TryGetRequestedAsset(request);
                    }
                }

                if (!IsLoaded)
                {
                    string message = CreateLoadFailureMessage();
                    throw new InvalidOperationException(message, LastError);
                }

                Asset!.hideFlags = HideFlags.HideAndDontSave;
                UnityObject.DontDestroyOnLoad(Asset);
                LastError = null;
            }
            finally
            {
                if (!ReferenceEquals(bundle, null))
                    TryUnloadBundle(bundle, unloadLoadedObjects: !IsLoaded);

                IsLoading = false;
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
                    return TryCast(bundle.LoadAsset(_assetName, Il2CppType.Of<UnityObject>()));
                }
                catch (Exception exception)
                {
                    LastError = exception;
                    Logging.WriteRecoverable(
                        LogLevel.Debug,
                        "Assets",
                        "Synchronous named-asset loading failed; trying its asynchronous fallback.",
                        exception
                    );
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
                Logging.WriteRecoverable(
                    LogLevel.Debug,
                    "Assets",
                    "Synchronous typed AssetBundle enumeration failed; trying its asynchronous fallback.",
                    exception
                );
                return null;
            }
        }

        private AssetBundleRequest? TryLoadAssetAsync(AssetBundle bundle)
        {
            try
            {
                return _assetName == null
                    ? bundle.LoadAllAssetsAsync(Il2CppType.Of<UnityObject>())
                    : bundle.LoadAssetAsync(_assetName, Il2CppType.Of<UnityObject>());
            }
            catch (Exception exception)
            {
                RecordError(exception);
                Logging.WriteRecoverable(
                    LogLevel.Debug,
                    "Assets",
                    _assetName == null
                        ? "Asynchronous typed AssetBundle enumeration is unavailable."
                        : "Asynchronous named-asset loading is unavailable.",
                    exception
                );
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
                Logging.WriteRecoverable(
                    LogLevel.Debug,
                    "Assets",
                    "Reading the asynchronous AssetBundle result failed.",
                    exception
                );
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
                Logging.WriteRecoverable(
                    LogLevel.Debug,
                    "Assets",
                    "AssetBundle.LoadFromFile failed; trying LoadFromFileAsync.",
                    exception
                );
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
                Logging.WriteRecoverable(
                    LogLevel.Warning,
                    "Assets",
                    "AssetBundle unloading failed.",
                    exception
                );
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

            return message;
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
}
