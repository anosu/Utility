using System;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using Utility.Diagnostics;
using UnityObject = UnityEngine.Object;

namespace Utility.Notifications.Internal
{
    // Both renderers use this font; no font is inherited from the game's GUISkin.
    internal static class ToastFont
    {
        private static Font? _font;
        private static bool _ownsFont;

        internal static Font Get()
        {
            if (!ReferenceEquals(_font, null))
                return _font;

            if (ToastPlatform.IsAndroid)
            {
                try
                {
                    Font font = CreateAndroidFont();
                    _font = font;
                    _ownsFont = true;
                    Logging.Write(
                        LogLevel.Information,
                        "Toast",
                        "Using the Android system sans-serif font."
                    );
                    return font;
                }
                catch (Exception exception)
                {
                    Logging.WriteRecoverable(
                        LogLevel.Debug,
                        "Toast",
                        "Android dynamic fonts are unavailable; trying Unity's built-in fonts.",
                        exception
                    );
                }
            }

            Exception? lastError = null;
            string[] candidates = { "LegacyRuntime.ttf", "Arial.ttf" };
            for (int i = 0; i < candidates.Length; i++)
            {
                try
                {
                    _font = LoadBuiltinFont(candidates[i]);
                    Logging.Write(
                        LogLevel.Information,
                        "Toast",
                        $"Using the built-in font '{candidates[i]}'."
                    );
                    return _font;
                }
                catch (Exception exception)
                {
                    lastError = exception;
                }
            }

            throw new InvalidOperationException(
                "No compatible toast font is available.",
                lastError
            );
        }

        internal static void Release()
        {
            Font? font = _font;
            bool ownsFont = _ownsFont;
            _font = null;
            _ownsFont = false;
            if (ownsFont && !ReferenceEquals(font, null))
                UnityObject.Destroy(font);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Font CreateAndroidFont()
        {
            // Fixed family order gives the same device the same choice across games.
            // Include CJK families for the Chinese and Japanese notification text.
            Font font = Font.CreateDynamicFontFromOSFont(
                new[]
                {
                    "Noto Sans CJK SC",
                    "Noto Sans CJK JP",
                    "Noto Sans SC",
                    "Noto Sans",
                    "Roboto",
                    "sans-serif",
                },
                16
            );
            if (ReferenceEquals(font, null))
                throw new InvalidOperationException("Unity did not create the Android toast font.");
            try
            {
                font.hideFlags = HideFlags.HideAndDontSave;
                return font;
            }
            catch
            {
                UnityObject.Destroy(font);
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Font LoadBuiltinFont(string name)
        {
            UnityObject? asset = Resources.GetBuiltinResource(Il2CppType.Of<Font>(), name);
            return asset?.TryCast<Font>()
                ?? throw new InvalidOperationException($"Built-in font '{name}' is unavailable.");
        }
    }
}
