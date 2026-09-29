using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using Utility.Diagnostics;

namespace Utility.Notifications.Internal
{
    internal static class ToastCanvasSetup
    {
        internal static void Configure(Canvas canvas)
        {
            try
            {
                SetOverlayMode(canvas);
            }
            catch (Exception exception)
            {
                if (ReadRenderMode(canvas) != RenderMode.ScreenSpaceOverlay)
                    throw;

                Logging.WriteRecoverable(
                    LogLevel.Warning,
                    "Toast",
                    "Canvas.renderMode setter is unavailable; using its existing overlay mode.",
                    exception
                );
            }

            canvas.overrideSorting = true;
            canvas.sortingOrder = 32760;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SetOverlayMode(Canvas canvas) =>
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static RenderMode ReadRenderMode(Canvas canvas) => canvas.renderMode;
    }
}
