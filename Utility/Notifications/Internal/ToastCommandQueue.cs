using System;
using System.Collections.Generic;

namespace Utility.Notifications.Internal
{
    internal enum ToastCommandKind
    {
        Show,
        Clear,
        Configure,
    }

    internal readonly struct ToastCommand
    {
        private ToastCommand(
            ToastCommandKind operation,
            string? title,
            string? message,
            ToastKind toastKind,
            float duration,
            ToastSettingsPatch settings
        )
        {
            Operation = operation;
            Title = title;
            Message = message;
            Kind = toastKind;
            Duration = duration;
            Settings = settings;
        }

        internal ToastCommandKind Operation { get; }
        internal string? Title { get; }
        internal string? Message { get; }
        internal ToastKind Kind { get; }
        internal float Duration { get; }
        internal ToastSettingsPatch Settings { get; }

        internal static ToastCommand Show(
            string title,
            string message,
            ToastKind kind,
            float duration
        ) => new(ToastCommandKind.Show, title, message, kind, duration, default);

        internal static ToastCommand Clear() =>
            new(ToastCommandKind.Clear, null, null, ToastKind.Info, 0f, default);

        internal static ToastCommand Configure(ToastSettingsPatch settings) =>
            new(ToastCommandKind.Configure, null, null, ToastKind.Info, 0f, settings);
    }

    internal sealed class ToastCommandQueue
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

        internal bool EnqueueShow(string title, string message, ToastKind kind, float duration)
        {
            lock (_gate)
            {
                if (_itemCount >= ToastRuntime.Capacity)
                    return false;

                _commands.Enqueue(
                    ToastCommand.Show(
                        title,
                        message,
                        NormalizeKind(kind),
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
                ToastSettingsPatch? configuration = RemoveConfigurations(
                    discardOtherCommands: true
                );
                if (configuration.HasValue)
                    _commands.Enqueue(ToastCommand.Configure(configuration.Value));
                _commands.Enqueue(ToastCommand.Clear());
                _itemCount = 0;
            }
        }

        internal void EnqueueConfigure(ToastSettingsPatch settings)
        {
            lock (_gate)
            {
                // All pending settings are applied before rendering the next frame.
                // Keep one combined patch, including across Show/Clear commands.
                ToastSettingsPatch previous =
                    RemoveConfigurations(discardOtherCommands: false) ?? default;
                _commands.Enqueue(ToastCommand.Configure(previous.Merge(settings)));
            }
        }

        private ToastSettingsPatch? RemoveConfigurations(bool discardOtherCommands)
        {
            ToastSettingsPatch? configuration = null;
            int count = _commands.Count;
            for (int i = 0; i < count; i++)
            {
                ToastCommand command = _commands.Dequeue();
                if (command.Operation == ToastCommandKind.Configure)
                    configuration = (configuration ?? default).Merge(command.Settings);
                else if (!discardOtherCommands)
                    _commands.Enqueue(command);
            }
            return configuration;
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

        private static ToastKind NormalizeKind(ToastKind kind) =>
            kind is ToastKind.Info or ToastKind.Success or ToastKind.Warning or ToastKind.Error
                ? kind
                : ToastKind.Info;
    }
}
