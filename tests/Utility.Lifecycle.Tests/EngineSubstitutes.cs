// Controlled substitutes for native calls. Tests execute the linked production
// coroutines and lifecycle callbacks; these substitutes do not validate rendering.
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Il2CppInterop.Runtime
{
    internal static class Il2CppType
    {
        internal static Type Of<T>() => typeof(T);
    }
}

namespace Il2CppInterop.Runtime.Injection
{
    internal static class ClassInjector
    {
        internal static bool IsTypeRegisteredInIl2Cpp<T>() => true;

        internal static void RegisterTypeInIl2Cpp<T>() { }
    }
}

namespace Il2CppInterop.Runtime.Attributes
{
    internal sealed class HideFromIl2CppAttribute : Attribute { }
}

namespace Il2CppInterop.Runtime.InteropTypes { }

namespace Il2CppInterop.Runtime.InteropTypes.Arrays
{
    public sealed class Il2CppReferenceArray<T>
    {
        private readonly T[] _items;

        public Il2CppReferenceArray(params T[] items) => _items = items;

        public int Length => _items.Length;
        public T this[int index] => _items[index];
    }
}

namespace UnityEngine
{
    using Il2CppInterop.Runtime.InteropTypes.Arrays;

    public enum HideFlags
    {
        HideAndDontSave,
    }

    public class Object
    {
        private static readonly Queue<Object> PendingDestroy = new();
        public bool FailPersistence { get; set; }
        public HideFlags hideFlags { get; set; }

        public T? TryCast<T>()
            where T : class => this as T;

        public static void DontDestroyOnLoad(Object target)
        {
            if (target.FailPersistence)
                throw new InvalidOperationException("Persistence failed.");
        }

        public static void Destroy(Object target) => PendingDestroy.Enqueue(target);

        internal static void FlushDestroy()
        {
            while (PendingDestroy.TryDequeue(out Object? target))
            {
                if (target is GameObject host && host.Component != null)
                    host.Component.InvokeMessage("OnDestroy");
                else if (target is MonoBehaviour component)
                    component.InvokeMessage("OnDestroy");
            }
        }
    }

    public sealed class Transform : Object { }

    public sealed class GameObject : Object
    {
        public GameObject(string name) { }

        public Transform transform { get; } = new();
        internal MonoBehaviour? Component { get; private set; }

        public Object AddComponent(Type type)
        {
            Component = (MonoBehaviour)Activator.CreateInstance(type, IntPtr.Zero)!;
            Component.gameObject = this;
            Component.InvokeMessage("Awake");
            return Component;
        }
    }

    public class MonoBehaviour : Object
    {
        public MonoBehaviour(IntPtr pointer) { }

        public GameObject gameObject { get; internal set; } = null!;
        public Transform transform => gameObject.transform;

        internal void InvokeMessage(string name) =>
            GetType()
                .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(this, null);
    }

    public static class Time
    {
        public static float unscaledDeltaTime => 0.016f;
    }

    public sealed class AssetBundleCreateRequest
    {
        public AssetBundle Result { get; init; } = null!;
        public int ResultReads { get; private set; }

        // Unity completes the request synchronously if its result is read early.
        public AssetBundle assetBundle
        {
            get
            {
                ResultReads++;
                return Result;
            }
        }
    }

    public sealed class AssetBundleRequest
    {
        public Object? asset { get; init; }
        public Il2CppReferenceArray<Object>? allAssets { get; init; }
    }

    public sealed class AssetBundle : Object
    {
        public static AssetBundle? SyncResult { get; set; }
        public static AssetBundleCreateRequest? AsyncResult { get; set; }
        public Object? Candidate { get; set; }
        public AssetBundleRequest? AssetRequest { get; set; }
        public List<bool> Unloads { get; } = new();

        public static AssetBundle? LoadFromFile(string path) => SyncResult;

        public static AssetBundleCreateRequest? LoadFromFileAsync(string path) => AsyncResult;

        public Object? LoadAsset(string name, Type type) => Candidate;

        public Il2CppReferenceArray<Object> LoadAllAssets(Type type) =>
            Candidate == null ? new() : new(Candidate);

        public AssetBundleRequest? LoadAssetAsync(string name, Type type) => AssetRequest;

        public AssetBundleRequest? LoadAllAssetsAsync(Type type) => AssetRequest;

        public void Unload(bool unloadLoadedObjects) => Unloads.Add(unloadLoadedObjects);
    }
}

namespace Utility.Notifications.Internal
{
    internal sealed class ToastRuntime
    {
        internal static ToastRuntime Shared { get; } = new();
        internal int Count => 0;
        internal ToastRendererKind RendererKind { get; private set; }
        internal Exception? LastRenderError => null;
        internal int DetachCount { get; private set; }
        internal int FrameCount { get; private set; }
        internal int RenderCount { get; private set; }

        internal void AttachRenderer(UnityEngine.Transform host) =>
            RendererKind = ToastRendererKind.Imgui;

        internal void DetachRenderer()
        {
            DetachCount++;
            RendererKind = ToastRendererKind.None;
        }

        internal void ProcessFrame(float delta) => FrameCount++;

        internal void Render() => RenderCount++;

        internal void Reset() { }

        internal bool EnqueueShow(string title, string message, ToastKind kind, float duration) =>
            true;

        internal void EnqueueConfigure(ToastSettingsPatch settings) { }

        internal void EnqueueClear() { }
    }
}
