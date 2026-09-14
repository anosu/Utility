using System;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Utility.Notifications.Internal
{
    internal static class ToastPlatform
    {
        internal static bool IsAndroid { get; } = DetectAndroid();

        private static bool DetectAndroid()
        {
            try
            {
                return ReadAndroidPlatform();
            }
            catch (Exception)
            {
                // Some generated Unity surfaces omit Application.platform.
                return Directory.Exists("/system/fonts");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadAndroidPlatform() =>
            Application.platform == RuntimePlatform.Android;
    }
}
