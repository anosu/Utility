using System;
using System.Collections.Generic;

namespace Utility.Toast
{
    internal enum ToastCommandKind
    {
        Show,
        Clear,
        Configure,
    }

    internal readonly struct ToastConfiguration
    {
        internal ToastConfiguration(
            float? width,
            float? maxHeight,
            float? margin,
            float? gap,
            int? max,
            int? titleSize,
            int? textSize,
            int? anchor
        )
        {
            Width = width;
            MaxHeight = maxHeight;
            Margin = margin;
            Gap = gap;
            Max = max;
            TitleSize = titleSize;
            TextSize = textSize;
            Anchor = anchor;
        }

        internal float? Width { get; }
        internal float? MaxHeight { get; }
        internal float? Margin { get; }
        internal float? Gap { get; }
        internal int? Max { get; }
        internal int? TitleSize { get; }
        internal int? TextSize { get; }
        internal int? Anchor { get; }
    }

    internal readonly struct ToastCommand
    {
        private ToastCommand(
            ToastCommandKind kind,
            string? title,
            string? message,
            ToastType type,
            float duration,
            ToastConfiguration configuration
        )
        {
            Kind = kind;
            Title = title;
            Message = message;
            Type = type;
            Duration = duration;
            Configuration = configuration;
        }

        internal ToastCommandKind Kind { get; }
        internal string? Title { get; }
        internal string? Message { get; }
        internal ToastType Type { get; }
        internal float Duration { get; }
        internal ToastConfiguration Configuration { get; }

        internal static ToastCommand Show(
            string title,
            string message,
            ToastType type,
            float duration
        ) => new(ToastCommandKind.Show, title, message, type, duration, default);

        internal static ToastCommand Clear() =>
            new(ToastCommandKind.Clear, null, null, ToastType.Info, 0f, default);

        internal static ToastCommand Configure(ToastConfiguration configuration) =>
            new(ToastCommandKind.Configure, null, null, ToastType.Info, 0f, configuration);
    }

    internal sealed class ToastCommandBuffer
    {
        private readonly Queue<ToastCommand> _commands = new();
        private readonly object _gate = new();
        private int _itemCount;

        internal int Count
        {
            get
            {
                lock (_gate)
                    return _itemCount;
            }
        }

        internal bool EnqueueShow(string title, string message, int type, float duration)
        {
            lock (_gate)
            {
                if (_itemCount >= ToastRuntime.Capacity)
                    return false;

                _commands.Enqueue(
                    ToastCommand.Show(
                        title,
                        message,
                        ToToastType(type),
                        float.IsFinite(duration) ? Math.Max(duration, 1f) : 3f
                    )
                );
                _itemCount++;
                return true;
            }
        }

        internal void EnqueueClear()
        {
            lock (_gate)
            {
                _commands.Enqueue(ToastCommand.Clear());
                _itemCount = 0;
            }
        }

        internal void EnqueueConfigure(ToastConfiguration configuration)
        {
            lock (_gate)
                _commands.Enqueue(ToastCommand.Configure(configuration));
        }

        internal void RunExclusive(Action<Queue<ToastCommand>> action)
        {
            lock (_gate)
                action(_commands);
        }

        internal void RemoveExpiredItem()
        {
            lock (_gate)
                _itemCount = Math.Max(0, _itemCount - 1);
        }

        internal void Reset(Action? resetState = null)
        {
            lock (_gate)
            {
                _commands.Clear();
                _itemCount = 0;
                resetState?.Invoke();
            }
        }

        private static ToastType ToToastType(int type) =>
            type switch
            {
                ToastUI.TYPE_SUCCESS => ToastType.Success,
                ToastUI.TYPE_WARN => ToastType.Warning,
                ToastUI.TYPE_ERROR => ToastType.Error,
                _ => ToastType.Info,
            };
    }

    internal sealed class ToastRuntime
    {
        internal const int Capacity = 50;

        private readonly List<ToastData> _active = new(Capacity);
        private readonly ToastCommandBuffer _commandBuffer = new();
        private readonly Queue<ToastData> _waiting = new(Capacity);

        private ToastRenderer? _renderer;
        private ToastStyle? _style;

        private ToastRuntime() { }

        internal static ToastRuntime Shared { get; } = new();

        internal int Count => _commandBuffer.Count;

        internal Exception? LastRenderError => _renderer?.LastError;

        internal bool EnqueueShow(string title, string message, int type, float duration) =>
            _commandBuffer.EnqueueShow(title, message, type, duration);

        internal void EnqueueClear() => _commandBuffer.EnqueueClear();

        internal void EnqueueConfigure(ToastConfiguration configuration) =>
            _commandBuffer.EnqueueConfigure(configuration);

        internal void ProcessFrame(float deltaTime)
        {
            if (_style == null)
                return;

            ToastStyle style = _style;
            _commandBuffer.RunExclusive(commands =>
            {
                ApplyCommands(commands, style);

                float elapsed = float.IsFinite(deltaTime) ? Math.Max(deltaTime, 0f) : 0f;
                for (int i = _active.Count - 1; i >= 0; i--)
                {
                    ToastData item = _active[i];
                    item.Remaining -= elapsed;
                    if (!item.Expired)
                        continue;

                    _active.RemoveAt(i);
                    _commandBuffer.RemoveExpiredItem();
                }

                while (_waiting.Count > 0 && _active.Count < style.Max)
                    _active.Add(_waiting.Dequeue());
            });
        }

        internal void Render()
        {
            if (_renderer == null || _style == null)
                return;

            try
            {
                _renderer.Render(_active, _style);
            }
            catch (Exception exception)
            {
                _renderer.Disable(exception);
            }
        }

        internal void AttachRenderer()
        {
            _style ??= new ToastStyle();
            _renderer ??= new ToastRenderer();
        }

        internal void DetachRenderer()
        {
            _renderer?.Dispose();
            _renderer = null;
            _style = null;
        }

        internal void Reset()
        {
            _commandBuffer.Reset(() =>
            {
                _active.Clear();
                _waiting.Clear();
            });
        }

        private void ApplyCommands(Queue<ToastCommand> commands, ToastStyle style)
        {
            while (commands.Count > 0)
            {
                ToastCommand command = commands.Dequeue();
                switch (command.Kind)
                {
                    case ToastCommandKind.Show:
                        _waiting.Enqueue(
                            new ToastData(
                                command.Title ?? string.Empty,
                                command.Message ?? string.Empty,
                                command.Type,
                                command.Duration
                            )
                        );
                        break;
                    case ToastCommandKind.Clear:
                        _active.Clear();
                        _waiting.Clear();
                        break;
                    case ToastCommandKind.Configure:
                        style.Apply(command.Configuration);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }
}
