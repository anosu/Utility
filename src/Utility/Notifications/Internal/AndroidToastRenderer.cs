using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using UnityEngine;

namespace Utility.Notifications.Internal
{
    internal sealed class AndroidToastRenderer : IFrameToastRenderer
    {
        private const string DexResource = "Utility.Toast.classes.dex";
        private readonly IntPtr _library;
        private readonly Present _present;
        private readonly Viewport _viewport;
        private readonly Stop _stop;

        internal AndroidToastRenderer()
        {
            string resource = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "Utility.Toast.arm64.so",
                Architecture.Arm => "Utility.Toast.arm.so",
                _ => throw new PlatformNotSupportedException(
                    "Android toast bridge requires an ARM runtime."
                ),
            };
            _library = NativeLibrary.Load(ExtractLibrary(resource));
            Stop? cleanup = null;
            try
            {
                var start = Load<Start>("toast_start");
                _present = Load<Present>("toast_present");
                _viewport = Load<Viewport>("toast_viewport");
                _stop = Load<Stop>("toast_stop");
                cleanup = _stop;
                byte[] dex = ReadResource(DexResource);
                GCHandle handle = GCHandle.Alloc(dex, GCHandleType.Pinned);
                try
                {
                    if (
                        start(IntPtr.Zero, handle.AddrOfPinnedObject(), dex.Length) == 0
                        && start(ReadUnityJavaVm(), handle.AddrOfPinnedObject(), dex.Length) == 0
                    )
                        throw new InvalidOperationException(
                            "Android toast bridge could not start."
                        );
                }
                finally
                {
                    handle.Free();
                }
            }
            catch
            {
                cleanup?.Invoke();
                NativeLibrary.Free(_library);
                throw;
            }
        }

        internal bool TryReadViewport(out ToastSafeArea safeArea, out int width, out int height)
        {
            var values = new int[6];
            GCHandle handle = GCHandle.Alloc(values, GCHandleType.Pinned);
            try
            {
                if (_viewport(handle.AddrOfPinnedObject()) == 0 || values[0] <= 0 || values[1] <= 0)
                {
                    safeArea = default;
                    width = height = 0;
                    return false;
                }
            }
            finally
            {
                handle.Free();
            }

            width = values[0];
            height = values[1];
            int left = Math.Clamp(values[2], 0, width - 1);
            int top = Math.Clamp(values[3], 0, height - 1);
            int right = Math.Clamp(values[4], 0, width - left - 1);
            int bottom = Math.Clamp(values[5], 0, height - top - 1);
            safeArea = new ToastSafeArea(left, bottom, width - left - right, height - top - bottom);
            return true;
        }

        public void RenderFrame(
            IReadOnlyList<ToastItem> active,
            ToastTheme style,
            ToastLayout layout
        )
        {
            byte[] bytes = CreateSnapshot(active, style, layout);
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                if (_present(handle.AddrOfPinnedObject(), bytes.Length) == 0)
                    throw new InvalidOperationException("Android toast view rejected a frame.");
            }
            finally
            {
                handle.Free();
            }
        }

        private static byte[] CreateSnapshot(
            IReadOnlyList<ToastItem> active,
            ToastTheme style,
            ToastLayout layout
        )
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteNumber("anchor", (int)style.Anchor);
                writer.WriteNumber("background", Argb(style.BackgroundColor));
                writer.WriteNumber("titleColor", Argb(style.TitleColor));
                writer.WriteNumber("textColor", Argb(style.TextColor));
                writer.WriteNumber("width", layout.Width);
                writer.WriteNumber("minimumHeight", layout.MinimumHeight);
                writer.WriteNumber("margin", layout.Margin);
                writer.WriteNumber("gap", layout.Gap);
                writer.WriteNumber("titleSize", layout.TitleSize);
                writer.WriteNumber("textSize", layout.TextSize);
                writer.WriteNumber("contentInset", layout.ContentInset);
                writer.WriteNumber("verticalInset", layout.VerticalInset);
                writer.WriteNumber("cornerRadius", layout.CornerRadius);
                writer.WriteNumber("accentWidth", layout.AccentWidth);
                writer.WriteNumber("messageTop", ToastMetrics.CalculateMessageTop(layout));
                writer.WriteStartArray("cards");
                for (int i = 0; i < active.Count; i++)
                {
                    ToastItem item = active[i];
                    writer.WriteStartObject();
                    writer.WriteString("title", item.Title);
                    writer.WriteString("message", item.Message);
                    writer.WriteNumber("accent", Argb(style.Accent(item.Kind)));
                    writer.WriteNumber("alpha", item.Alpha);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            return stream.ToArray();
        }

        public void Dispose()
        {
            _stop();
            NativeLibrary.Free(_library);
        }

        private T Load<T>(string name)
            where T : Delegate =>
            Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_library, name));

        private static IntPtr ReadUnityJavaVm()
        {
            try
            {
                Type? type = typeof(GameObject).Assembly.GetType("UnityEngine.AndroidJNI");
                MethodInfo? method = type?.GetMethod(
                    "GetJavaVM",
                    BindingFlags.Public | BindingFlags.Static
                );
                return method?.Invoke(null, null) is IntPtr pointer ? pointer : IntPtr.Zero;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        private static string ExtractLibrary(string resource)
        {
            byte[] bytes = ReadResource(resource);
            string hash = Convert.ToHexString(SHA256.HashData(bytes)).Substring(0, 16);
            string directory = Path.Combine(Path.GetTempPath(), "utility-toast", hash);
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "libutilitytoast.so");
            if (File.Exists(path))
                return path;
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllBytes(temporary, bytes);
            try
            {
                File.Move(temporary, path);
            }
            catch (IOException) when (File.Exists(path))
            {
                File.Delete(temporary);
            }
            return path;
        }

        private static byte[] ReadResource(string name)
        {
            using Stream stream =
                typeof(AndroidToastRenderer).Assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Missing Android toast resource '{name}'.");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }

        private static int Argb(Color color) =>
            (Channel(color.a) << 24)
            | (Channel(color.r) << 16)
            | (Channel(color.g) << 8)
            | Channel(color.b);

        private static int Channel(float value) =>
            (int)Math.Round(Math.Clamp(value, 0f, 1f) * 255f);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int Start(IntPtr vm, IntPtr dex, int length);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int Present(IntPtr bytes, int length);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int Viewport(IntPtr values);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void Stop();
    }
}
