using System;
using System.Collections.Generic;
using UnityEngine;
using Utility.Diagnostics;

namespace Utility.Notifications.Internal
{
    internal sealed class ToastRuntime
    {
        internal const int Capacity = 50;

        private readonly List<ToastItem> _active = new(Capacity);
        private readonly ToastCommandQueue _commands = new();
        private readonly Queue<ToastItem> _waiting = new(Capacity);

        private ImguiToastRenderer? _imguiRenderer;
        private IFrameToastRenderer? _frameRenderer;
        private ToastTheme? _theme;
        private Exception? _backendError;
        private Transform? _host;
        private bool _uguiFallbackPending;

        private ToastRuntime() { }

        internal static ToastRuntime Shared { get; } = new();

        internal int Count => _commands.Count;

        internal ToastRendererKind RendererKind =>
            _frameRenderer != null ? ToastRendererKind.Ugui
            : _imguiRenderer != null ? ToastRendererKind.Imgui
            : ToastRendererKind.None;

        internal Exception? LastRenderError => _imguiRenderer?.LastError ?? _backendError;

        internal bool EnqueueShow(string title, string message, ToastKind kind, float duration) =>
            _commands.EnqueueShow(title, message, kind, duration);

        internal void EnqueueClear() => _commands.EnqueueClear();

        internal void EnqueueConfigure(ToastSettingsPatch settings) =>
            _commands.EnqueueConfigure(settings);

        internal void ProcessFrame(float deltaTime)
        {
            if (_theme == null)
                return;

            ToastTheme style = _theme;
            _commands.RunExclusive(commands =>
            {
                ApplyCommands(commands, style);

                float elapsed = float.IsFinite(deltaTime) ? Math.Max(deltaTime, 0f) : 0f;
                for (int i = _active.Count - 1; i >= 0; i--)
                {
                    ToastItem item = _active[i];
                    item.Remaining -= elapsed;
                    if (!item.Expired)
                        continue;

                    _active.RemoveAt(i);
                    _commands.RemoveExpiredItem();
                }

                while (_waiting.Count > 0 && _active.Count < style.MaximumVisible)
                    _active.Add(_waiting.Dequeue());
            });

            if (_uguiFallbackPending)
                ActivateUguiFallback();

            if (_frameRenderer == null)
                return;

            try
            {
                _frameRenderer.RenderFrame(_active, style);
            }
            catch (Exception exception)
            {
                DisableUgui(exception);
            }
        }

        internal void Render()
        {
            if (_frameRenderer != null || _imguiRenderer == null || _theme == null)
                return;

            try
            {
                _imguiRenderer.Render(_active, _theme);
            }
            catch (Exception exception)
            {
                _imguiRenderer.Disable(exception);
            }

            if (_imguiRenderer.IsDisabled)
            {
                QueueUguiFallback(
                    _imguiRenderer.LastError
                        ?? new InvalidOperationException("IMGUI rendering was disabled.")
                );
            }
        }

        internal void AttachRenderer(Transform host)
        {
            _theme ??= new ToastTheme();
            if (_frameRenderer != null || _imguiRenderer != null)
                return;

            _host = host;
            _backendError = null;
            _uguiFallbackPending = false;
            _imguiRenderer = new ImguiToastRenderer();
            Logging.Write(LogLevel.Information, "Toast", "Using the preferred IMGUI renderer.");
        }

        internal void DetachRenderer()
        {
            _imguiRenderer?.Dispose();
            try
            {
                _frameRenderer?.Dispose();
            }
            catch (Exception exception)
            {
                _backendError = exception;
                Logging.Write(
                    LogLevel.Warning,
                    "Toast",
                    "Disposing the uGUI renderer failed.",
                    exception
                );
            }

            _imguiRenderer = null;
            _frameRenderer = null;
            _theme = null;
            _host = null;
            _uguiFallbackPending = false;
        }

        internal void Reset()
        {
            _commands.Reset(() =>
            {
                _active.Clear();
                _waiting.Clear();
            });
        }

        private void ApplyCommands(Queue<ToastCommand> commands, ToastTheme style)
        {
            while (commands.Count > 0)
            {
                ToastCommand command = commands.Dequeue();
                switch (command.Operation)
                {
                    case ToastCommandKind.Show:
                        _waiting.Enqueue(
                            new ToastItem(
                                command.Title ?? string.Empty,
                                command.Message ?? string.Empty,
                                command.Kind,
                                command.Duration
                            )
                        );
                        break;
                    case ToastCommandKind.Clear:
                        _active.Clear();
                        _waiting.Clear();
                        break;
                    case ToastCommandKind.Configure:
                        style.Apply(command.Settings);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private void QueueUguiFallback(Exception exception)
        {
            _imguiRenderer?.Dispose();
            _imguiRenderer = null;
            _backendError = exception;
            _uguiFallbackPending = true;
            Logging.Write(
                LogLevel.Warning,
                "Toast",
                "IMGUI rendering failed; queued the optional uGUI fallback.",
                exception
            );
        }

        private void ActivateUguiFallback()
        {
            _uguiFallbackPending = false;
            if (ReferenceEquals(_host, null))
                return;

            _frameRenderer = OptionalRendererLoader.TryCreateUgui(_host, out Exception? error);
            if (_frameRenderer != null)
            {
                Logging.Write(
                    LogLevel.Warning,
                    "Toast",
                    "Switched from IMGUI to the optional uGUI renderer.",
                    _backendError
                );
                return;
            }

            if (error != null)
                _backendError = CombineBackendErrors(_backendError, error);

            Logging.Write(
                LogLevel.Error,
                "Toast",
                "IMGUI failed and the optional uGUI fallback could not be initialized.",
                _backendError
            );
        }

        private void DisableUgui(Exception exception)
        {
            try
            {
                _frameRenderer?.Dispose();
            }
            catch (Exception disposeException)
            {
                exception = new AggregateException(exception, disposeException);
            }

            _frameRenderer = null;
            _backendError = CombineBackendErrors(_backendError, exception);
            Logging.Write(
                LogLevel.Error,
                "Toast",
                "The uGUI fallback also failed; toast rendering is now disabled.",
                _backendError
            );
        }

        private static Exception CombineBackendErrors(Exception? first, Exception second) =>
            first == null
                ? second
                : new AggregateException("Every toast renderer failed.", first, second);
    }
}
