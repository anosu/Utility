using System;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace Utility.Toast
{
    /// <summary>Bridges Unity lifecycle messages to the managed toast runtime.</summary>
    public sealed class ToastUI : MonoBehaviour
    {
        /// <summary>Identifies an informational notification.</summary>
        public const int TYPE_INFO = 0;

        /// <summary>Identifies a success notification.</summary>
        public const int TYPE_SUCCESS = 1;

        /// <summary>Identifies a warning notification.</summary>
        public const int TYPE_WARN = 2;

        /// <summary>Identifies an error notification.</summary>
        public const int TYPE_ERROR = 3;

        /// <summary>Identifies the top-left anchor.</summary>
        public const int ANCHOR_TL = 0;

        /// <summary>Identifies the top-center anchor.</summary>
        public const int ANCHOR_TC = 1;

        /// <summary>Identifies the top-right anchor.</summary>
        public const int ANCHOR_TR = 2;

        /// <summary>Identifies the bottom-left anchor.</summary>
        public const int ANCHOR_BL = 3;

        /// <summary>Identifies the bottom-center anchor.</summary>
        public const int ANCHOR_BC = 4;

        /// <summary>Identifies the bottom-right anchor.</summary>
        public const int ANCHOR_BR = 5;

        private static ToastUI? _instance;

        /// <summary>Initializes a managed wrapper for an existing native component.</summary>
        /// <param name="pointer">The native IL2CPP object pointer.</param>
        public ToastUI(IntPtr pointer)
            : base(pointer) { }

        /// <summary>Gets the active toast behaviour.</summary>
        [HideFromIl2Cpp]
        public static ToastUI? Instance => _instance;

        /// <summary>Queues a notification through the loader-neutral facade.</summary>
        [HideFromIl2Cpp]
        public bool Show(string title, string message, int type = TYPE_INFO, float duration = 3f) =>
            Toast.Show(title, message, type, duration);

        /// <summary>Queues an informational notification.</summary>
        [HideFromIl2Cpp]
        public bool Info(string title, string message, float duration = 3f) =>
            Toast.Info(title, message, duration);

        /// <summary>Queues a success notification.</summary>
        [HideFromIl2Cpp]
        public bool Success(string title, string message, float duration = 3f) =>
            Toast.Success(title, message, duration);

        /// <summary>Queues a warning notification.</summary>
        [HideFromIl2Cpp]
        public bool Warn(string title, string message, float duration = 4f) =>
            Toast.Warn(title, message, duration);

        /// <summary>Queues an error notification.</summary>
        [HideFromIl2Cpp]
        public bool Error(string title, string message, float duration = 5f) =>
            Toast.Error(title, message, duration);

        /// <summary>Queues a partial style update.</summary>
        [HideFromIl2Cpp]
        public void Config(
            float? width = null,
            float? maxHeight = null,
            float? margin = null,
            float? gap = null,
            int? max = null,
            int? titleSize = null,
            int? textSize = null,
            int? anchor = null
        ) => Toast.Configure(width, maxHeight, margin, gap, max, titleSize, textSize, anchor);

        /// <summary>Queues removal of all notifications.</summary>
        [HideFromIl2Cpp]
        public void Clear() => Toast.Clear();

        /// <summary>Gets the number of active, waiting, and pending notifications.</summary>
        [HideFromIl2Cpp]
        public int Count => Toast.Count;

        private void Awake()
        {
            if (!ReferenceEquals(_instance, null) && !ReferenceEquals(_instance, this))
            {
                Destroy(this);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            ToastRuntime.Shared.AttachRenderer();
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
