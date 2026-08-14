using System.Collections;
using System.IO;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;

namespace Utility.Fonts
{
    public sealed class FontHelper
    {
        public TMP_FontAsset Asset { get; private set; }
        public bool Loading { get; private set; }
        public bool Valid => Asset != null && Asset.atlasTexture != null && Asset.material != null;

        private readonly string _bundlePath;

        public FontHelper(string bundlePath)
        {
            _bundlePath = bundlePath;
        }

        public IEnumerator LoadAsync(System.Action onComplete = null)
        {
            if (Valid || Loading)
                yield break;

            EnsureExists();

            Loading = true;

            AssetBundle bundle = null;

            try
            {
                bundle = AssetBundle.LoadFromFile(_bundlePath);
            }
            catch { }

            if (bundle == null)
            {
                AssetBundleCreateRequest abcr = null;

                try
                {
                    abcr = AssetBundle.LoadFromFileAsync(_bundlePath);
                }
                catch { }

                if (abcr == null)
                {
                    Loading = false;
                    throw new("Failed to load AssetBundle with LoadFromFile and LoadFromFileAsync");
                }

                yield return abcr;

                bundle = abcr.assetBundle;
            }

            if (bundle == null)
            {
                Loading = false;
                throw new("Failed to load AssetBundle with LoadFromFile and LoadFromFileAsync");
            }

            try
            {
                ProcessBundle(bundle, bundle.LoadAllAssets());
            }
            catch { }

            if (Valid)
            {
                Loading = false;
                yield break;
            }

            AssetBundleRequest abr = null;

            try
            {
                abr = bundle.LoadAllAssetsAsync(Il2CppType.Of<Object>());
            }
            catch { }

            if (abr == null)
            {
                Loading = false;
                throw new("Failed to load Assets with LoadAllAssets and LoadAllAssetsAsync");
            }

            yield return abr;

            try
            {
                ProcessBundle(bundle, abr.allAssets);
            }
            catch { }

            Loading = false;

            EnsureValid();

            onComplete?.Invoke();
        }

        private void ProcessBundle(AssetBundle bundle, Il2CppReferenceArray<Object> assets)
        {
            if (assets != null)
            {
                foreach (var asset in assets)
                {
                    Asset = asset?.TryCast<TMP_FontAsset>();
                    if (Asset != null)
                        break;
                }
            }

            if (Asset != null)
            {
                Asset.hideFlags = HideFlags.HideAndDontSave;
                Object.DontDestroyOnLoad(Asset);
            }

            bundle.Unload(Asset == null);
        }

        private void EnsureExists()
        {
            if (!File.Exists(_bundlePath))
            {
                throw new FileNotFoundException($"找不到文件: {_bundlePath}");
            }
        }

        private void EnsureValid()
        {
            if (!Valid)
            {
                throw new System.Exception("TMP字体加载失败");
            }
        }
    }
}
