using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Utility.Toast
{
    /// <summary>Provides the loader-neutral entry point for the toast system.</summary>
    public static class Toast
    {
        private const string DefaultObjectName = "Utility.Toast";

        /// <summary>Gets a value that indicates whether the Unity behaviour is initialized.</summary>
        public static bool IsInitialized => !ReferenceEquals(ToastUI.Instance, null);

        /// <summary>Gets the last exception that disabled or degraded the toast renderer.</summary>
        public static Exception? LastRenderError => ToastRuntime.Shared.LastRenderError;

        /// <summary>Gets the number of active, waiting, and pending notifications.</summary>
        public static int Count => ToastRuntime.Shared.Count;

        /// <summary>Registers and creates the IL2CPP behaviour used by both BepInEx and MelonLoader.</summary>
        /// <param name="gameObjectName">The name assigned to the dedicated persistent GameObject.</param>
        /// <returns>The initialized behaviour.</returns>
        /// <remarks>This method must be called from Unity's main thread.</remarks>
        public static ToastUI Initialize(string gameObjectName = DefaultObjectName)
        {
            if (IsInitialized)
                return ToastUI.Instance!;

            if (!ClassInjector.IsTypeRegisteredInIl2Cpp<ToastUI>())
                ClassInjector.RegisterTypeInIl2Cpp<ToastUI>();

            var host = new GameObject(
                string.IsNullOrWhiteSpace(gameObjectName) ? DefaultObjectName : gameObjectName
            )
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            UnityObject.DontDestroyOnLoad(host);

            try
            {
                var component = host.AddComponent(Il2CppType.Of<ToastUI>())?.TryCast<ToastUI>();
                if (ReferenceEquals(component, null))
                    throw new InvalidOperationException(
                        "Unity did not create the ToastUI component."
                    );

                return component;
            }
            catch
            {
                UnityObject.Destroy(host);
                throw;
            }
        }

        /// <summary>Destroys the toast host and clears all queued notifications.</summary>
        /// <remarks>This method must be called from Unity's main thread.</remarks>
        public static void Shutdown()
        {
            ToastRuntime.Shared.Reset();

            var instance = ToastUI.Instance;
            if (!ReferenceEquals(instance, null))
                UnityObject.Destroy(instance.gameObject);
        }

        /// <summary>Queues a notification for display.</summary>
        /// <param name="title">The notification title.</param>
        /// <param name="message">The notification body.</param>
        /// <param name="type">The numeric notification type.</param>
        /// <param name="duration">The display duration in seconds.</param>
        /// <returns><see langword="true"/> if the notification was accepted; otherwise, <see langword="false"/>.</returns>
        public static bool Show(string title, string message, int type = 0, float duration = 3f) =>
            ToastRuntime.Shared.EnqueueShow(
                title ?? string.Empty,
                message ?? string.Empty,
                type,
                duration
            );

        /// <summary>Queues an informational notification.</summary>
        public static bool Info(string title, string message, float duration = 3f) =>
            Show(title, message, ToastUI.TYPE_INFO, duration);

        /// <summary>Queues a success notification.</summary>
        public static bool Success(string title, string message, float duration = 3f) =>
            Show(title, message, ToastUI.TYPE_SUCCESS, duration);

        /// <summary>Queues a warning notification.</summary>
        public static bool Warn(string title, string message, float duration = 4f) =>
            Show(title, message, ToastUI.TYPE_WARN, duration);

        /// <summary>Queues an error notification.</summary>
        public static bool Error(string title, string message, float duration = 5f) =>
            Show(title, message, ToastUI.TYPE_ERROR, duration);

        /// <summary>Queues a partial style update for the Unity main thread.</summary>
        public static void Configure(
            float? width = null,
            float? maxHeight = null,
            float? margin = null,
            float? gap = null,
            int? max = null,
            int? titleSize = null,
            int? textSize = null,
            int? anchor = null
        ) =>
            ToastRuntime.Shared.EnqueueConfigure(
                new ToastConfiguration(
                    width,
                    maxHeight,
                    margin,
                    gap,
                    max,
                    titleSize,
                    textSize,
                    anchor
                )
            );

        /// <summary>Queues removal of all active and waiting notifications.</summary>
        public static void Clear() => ToastRuntime.Shared.EnqueueClear();
    }
}
