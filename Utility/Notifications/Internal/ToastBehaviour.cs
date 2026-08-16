using System;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace Utility.Notifications.Internal
{
    /// <summary>Bridges Unity lifecycle messages to the managed toast runtime.</summary>
    internal sealed class ToastBehaviour : MonoBehaviour
    {
        private static ToastBehaviour? _instance;

        /// <summary>Initializes a managed wrapper for an existing native component.</summary>
        /// <param name="pointer">The native IL2CPP object pointer.</param>
        public ToastBehaviour(IntPtr pointer)
            : base(pointer) { }

        /// <summary>Gets the active toast behaviour.</summary>
        [HideFromIl2Cpp]
        internal static ToastBehaviour? Instance => _instance;

        private void Awake()
        {
            if (!ReferenceEquals(_instance, null) && !ReferenceEquals(_instance, this))
            {
                Destroy(this);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            ToastRuntime.Shared.AttachRenderer(transform);
        }

        private void Update() => ToastRuntime.Shared.ProcessFrame(Time.unscaledDeltaTime);

        private void OnGUI() => ToastRuntime.Shared.Render();

        private void OnDestroy()
        {
            if (!ReferenceEquals(_instance, this))
                return;

            _instance = null;
            ToastRuntime.Shared.DetachRenderer();
        }
    }
}
