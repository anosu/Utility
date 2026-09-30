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
        private const long HealthIntervalMilliseconds = 500;
        private readonly IntPtr _library;
        private readonly Present _present;
        private readonly Maintain _maintain;
        private readonly Viewport _viewport;
        private readonly Stop _stop;
        private readonly IntPtr _viewportBuffer;
        private readonly ToastItem?[] _lastItems = new ToastItem?[ToastRuntime.Capacity];
        private readonly float[] _lastAlphas = new float[ToastRuntime.Capacity];
        private bool _hasSnapshot;
        private ToastTheme? _lastTheme;
        private ToastLayout _lastLayout;
        private int _lastThemeVersion;
        private int _lastCount;
        private long _nextHealthCheck;

        internal AndroidToastRenderer()
        {
            string resource = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "Utility.Toast.arm64.so",
                _ => throw new PlatformNotSupportedException(
                    "Android toast bridge requires an ARM64 runtime."
                ),
            };
            _library = NativeLibrary.Load(ExtractLibrary(resource));
            Stop? cleanup = null;
            try
            {
                var start = Load<Start>("toast_start");
                _present = Load<Present>("toast_present");
                _maintain = Load<Maintain>("toast_maintain");
                _viewport = Load<Viewport>("toast_viewport");
                _stop = Load<Stop>("toast_stop");
                cleanup = _stop;
                byte[] dex = ReadResource(DexResource);
                GCHandle handle = GCHandle.Alloc(dex, GCHandleType.Pinned);
                try
                {
                    int result = start(IntPtr.Zero, handle.AddrOfPinnedObject(), dex.Length);
                    if (result == -1)
                    {
                        IntPtr javaVm = ReadUnityJavaVm();
                        if (javaVm != IntPtr.Zero)
                            result = start(javaVm, handle.AddrOfPinnedObject(), dex.Length);
                    }
                    if (result != 1)
                        throw new AndroidToastStartupException(result);
                }
                finally
                {
                    handle.Free();
                }
                _viewportBuffer = Marshal.AllocHGlobal(sizeof(int) * 6);
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
            if (_viewport(_viewportBuffer) == 0)
            {
                safeArea = default;
                width = height = 0;
                return false;
            }

            width = Marshal.ReadInt32(_viewportBuffer);
            height = Marshal.ReadInt32(_viewportBuffer, sizeof(int));
            if (width <= 0 || height <= 0)
            {
                safeArea = default;
                width = height = 0;
                return false;
            }
            int left = Math.Clamp(
                Marshal.ReadInt32(_viewportBuffer, sizeof(int) * 2),
                0,
                width - 1
            );
            int top = Math.Clamp(
                Marshal.ReadInt32(_viewportBuffer, sizeof(int) * 3),
                0,
                height - 1
            );
            int right = Math.Clamp(
                Marshal.ReadInt32(_viewportBuffer, sizeof(int) * 4),
                0,
                width - left - 1
            );
            int bottom = Math.Clamp(
                Marshal.ReadInt32(_viewportBuffer, sizeof(int) * 5),
                0,
                height - top - 1
            );
            safeArea = new ToastSafeArea(left, bottom, width - left - right, height - top - bottom);
            return true;
        }

        public void RenderFrame(
            IReadOnlyList<ToastItem> active,
            ToastTheme style,
            ToastLayout layout
        )
        {
            if (active.Count == 0 && _lastCount == 0)
                return;
            long now = Environment.TickCount64;
            if (HasSameSnapshotState(active, style, layout))
            {
                if (active.Count == 0 || now < _nextHealthCheck)
                    return;
                if (_maintain() == 0)
                    throw new InvalidOperationException("Android toast view health check failed.");
                _nextHealthCheck = now + HealthIntervalMilliseconds;
                return;
            }

            byte[] bytes = CreateSnapshot(active, style, layout);
            PresentSnapshot(bytes);
            RememberSnapshotState(active, style, layout);
            _hasSnapshot = true;
            _nextHealthCheck = now + HealthIntervalMilliseconds;
        }

        private void PresentSnapshot(byte[] bytes)
        {
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

        private bool HasSameSnapshotState(
            IReadOnlyList<ToastItem> active,
            ToastTheme style,
            ToastLayout layout
        )
        {
            if (
                !_hasSnapshot
                || !ReferenceEquals(_lastTheme, style)
                || _lastThemeVersion != style.Version
                || _lastCount != active.Count
                || _lastLayout.Width != layout.Width
                || _lastLayout.MinimumHeight != layout.MinimumHeight
                || _lastLayout.Margin != layout.Margin
                || _lastLayout.Gap != layout.Gap
                || _lastLayout.TitleSize != layout.TitleSize
                || _lastLayout.TextSize != layout.TextSize
                || _lastLayout.SpacingScale != layout.SpacingScale
            )
                return false;

            for (int i = 0; i < active.Count; i++)
            {
                if (!ReferenceEquals(_lastItems[i], active[i]) || _lastAlphas[i] != active[i].Alpha)
                    return false;
            }
            return true;
        }

        private void RememberSnapshotState(
            IReadOnlyList<ToastItem> active,
            ToastTheme style,
            ToastLayout layout
        )
        {
            for (int i = active.Count; i < _lastCount; i++)
                _lastItems[i] = null;
            _lastTheme = style;
            _lastThemeVersion = style.Version;
            _lastLayout = layout;
            _lastCount = active.Count;
            for (int i = 0; i < active.Count; i++)
            {
                _lastItems[i] = active[i];
                _lastAlphas[i] = active[i].Alpha;
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
            try
            {
                _stop();
            }
            finally
            {
                Marshal.FreeHGlobal(_viewportBuffer);
                NativeLibrary.Free(_library);
            }
        }

        private T Load<T>(string name)
            where T : Delegate =>
            Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_library, name));

        private static IntPtr ReadUnityJavaVm()
        {
            try
            {
                Type? type = Type.GetType(
                    "UnityEngine.AndroidJNI, UnityEngine.AndroidJNIModule",
                    throwOnError: false
                );
                if (type == null)
                {
                    foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        type = assembly.GetType("UnityEngine.AndroidJNI", throwOnError: false);
                        if (type != null)
                            break;
                    }
                }
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
        private delegate int Maintain();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int Viewport(IntPtr values);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void Stop();
    }

    internal sealed class AndroidToastStartupException : InvalidOperationException
    {
        internal AndroidToastStartupException(int status)
            : base($"Android toast bridge could not start: {Describe(status)} (stage {status}).") =>
            Status = status;

        internal int Status { get; }
        internal bool CanRetry => Status is -1 or -2 or -3 or -5;

        private static string Describe(int status) =>
            status switch
            {
                -1 => "Java VM unavailable",
                -2 => "JNI environment unavailable",
                -3 => "Unity Activity unavailable",
                -4 => "Java helper could not be loaded",
                -5 => "Java View could not be attached",
                -6 => "embedded DEX is missing",
                _ => "unexpected native result",
            };
    }
}
