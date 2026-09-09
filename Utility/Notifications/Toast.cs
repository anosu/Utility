using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using Utility.Diagnostics;
using Utility.Notifications.Internal;
using UnityObject = UnityEngine.Object;

namespace Utility.Notifications
{
    /// <summary>Provides the loader-neutral entry point for the toast system.</summary>
    public static class Toast
    {
        private const string DefaultObjectName = "Utility.Notifications";

        /// <summary>Gets a value that indicates whether the Unity behaviour is initialized.</summary>
        public static bool IsInitialized => !ReferenceEquals(ToastBehaviour.Instance, null);

        /// <summary>Gets the last exception that disabled or degraded the toast renderer.</summary>
        public static Exception? LastRenderError => ToastRuntime.Shared.LastRenderError;

        /// <summary>Gets the renderer currently selected by the toast system.</summary>
        /// <remarks>
        /// IMGUI is preferred and falls back to the built-in uGUI renderer when its compatible
        /// drawing paths fail.
        /// </remarks>
        public static ToastRendererKind Renderer => ToastRuntime.Shared.RendererKind;

        /// <summary>Gets the number of active, waiting, and pending notifications.</summary>
        public static int Count => ToastRuntime.Shared.Count;

        /// <summary>Registers and creates the IL2CPP behaviour used by both BepInEx and MelonLoader.</summary>
        /// <param name="gameObjectName">The name assigned to the dedicated persistent GameObject.</param>
        /// <remarks>This method must be called from Unity's main thread.</remarks>
        public static void Initialize(string gameObjectName = DefaultObjectName)
        {
            if (IsInitialized)
                return;

            if (!ClassInjector.IsTypeRegisteredInIl2Cpp<ToastBehaviour>())
                ClassInjector.RegisterTypeInIl2Cpp<ToastBehaviour>();

            var host = new GameObject(
                string.IsNullOrWhiteSpace(gameObjectName) ? DefaultObjectName : gameObjectName
            )
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            UnityObject.DontDestroyOnLoad(host);

            try
            {
                var component = host.AddComponent(Il2CppType.Of<ToastBehaviour>())
                    ?.TryCast<ToastBehaviour>();
                if (ReferenceEquals(component, null))
                    throw new InvalidOperationException(
                        "Unity did not create the ToastBehaviour component."
                    );

                Logging.Write(
                    LogLevel.Information,
                    "Toast",
                    $"Toast initialized with the {Renderer} renderer."
                );
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
            Logging.Write(LogLevel.Information, "Toast", "Toast is shutting down.");
            ToastRuntime.Shared.Reset();

            var instance = ToastBehaviour.Instance;
            if (!ReferenceEquals(instance, null))
            {
                GameObject host = instance.gameObject;
                instance.Detach();
                UnityObject.Destroy(host);
            }
        }

        /// <summary>Queues a notification for display.</summary>
        /// <param name="title">The notification title.</param>
        /// <param name="message">The notification body.</param>
        /// <param name="kind">The semantic notification kind.</param>
        /// <param name="duration">The display duration in seconds.</param>
        /// <returns><see langword="true"/> if the notification was accepted; otherwise, <see langword="false"/>.</returns>
        public static bool Show(
            string title,
            string message,
            ToastKind kind = ToastKind.Info,
            float duration = 3f
        ) =>
            ToastRuntime.Shared.EnqueueShow(
                title ?? string.Empty,
                message ?? string.Empty,
                kind,
                duration
            );

        /// <summary>Queues an informational notification.</summary>
        public static bool Info(string title, string message, float duration = 3f) =>
            Show(title, message, ToastKind.Info, duration);

        /// <summary>Queues a success notification.</summary>
        public static bool Success(string title, string message, float duration = 3f) =>
            Show(title, message, ToastKind.Success, duration);

        /// <summary>Queues a warning notification.</summary>
        public static bool Warning(string title, string message, float duration = 4f) =>
            Show(title, message, ToastKind.Warning, duration);

        /// <summary>Queues an error notification.</summary>
        public static bool Error(string title, string message, float duration = 5f) =>
            Show(title, message, ToastKind.Error, duration);

        /// <summary>Queues a partial style update for the Unity main thread.</summary>
        public static void Configure(
            float? width = null,
            float? minimumHeight = null,
            float? margin = null,
            float? gap = null,
            int? maximumVisible = null,
            int? titleSize = null,
            int? textSize = null,
            ToastAnchor? anchor = null
        ) =>
            ToastRuntime.Shared.EnqueueConfigure(
                new ToastSettingsPatch(
                    width,
                    minimumHeight,
                    margin,
                    gap,
                    maximumVisible,
                    titleSize,
                    textSize,
                    anchor
                )
            );

        /// <summary>Queues removal of all active and waiting notifications.</summary>
        public static void Clear() => ToastRuntime.Shared.EnqueueClear();
    }
}
