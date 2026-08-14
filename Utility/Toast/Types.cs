using System;
using UnityEngine;

namespace Utility.Toast
{
    /// <summary>Specifies the semantic kind of a notification.</summary>
    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error,
    }

    /// <summary>Specifies a screen anchor for notifications.</summary>
    public enum Anchor
    {
        TopLeft,
        TopCenter,
        TopRight,
        BottomLeft,
        BottomCenter,
        BottomRight,
    }

    /// <summary>Represents the mutable lifetime state of one notification.</summary>
    public sealed class ToastData
    {
        /// <summary>Initializes a new instance of the <see cref="ToastData"/> class.</summary>
        public ToastData(string title, string message, ToastType type, float duration)
        {
            Title = title;
            Message = message;
            Type = type;
            Duration = float.IsFinite(duration) ? Math.Max(duration, 1f) : 3f;
            Remaining = Duration;
        }

        /// <summary>Gets the notification title.</summary>
        public string Title { get; }

        /// <summary>Gets the notification body.</summary>
        public string Message { get; }

        /// <summary>Gets the semantic notification type.</summary>
        public ToastType Type { get; }

        /// <summary>Gets the total display duration in seconds.</summary>
        public float Duration { get; }

        /// <summary>Gets or sets the remaining display time in seconds.</summary>
        public float Remaining { get; internal set; }

        internal string? CompatibilityMessage { get; set; }
        internal int CompatibilityCharacters { get; set; }
        internal int CompatibilityLineCount { get; set; }

        /// <summary>Gets a value that indicates whether the notification has expired.</summary>
        public bool Expired => Remaining <= 0f;

        /// <summary>Gets the current fade opacity.</summary>
        public float Alpha
        {
            get
            {
                const float FadeIn = 0.3f;
                const float FadeOut = 0.5f;

                if (Remaining > Duration - FadeIn)
                    return (Duration - Remaining) / FadeIn;
                if (Remaining < FadeOut)
                    return Math.Max(0f, Remaining / FadeOut);
                return 1f;
            }
        }
    }

    /// <summary>Stores the visual configuration used by the IMGUI renderer.</summary>
    public sealed class ToastStyle
    {
        public float Width = 425f;
        public float MaxHeight = 105f;
        public float Margin = 20f;
        public float Gap = 15f;
        public int TitleSize = 19;
        public int TextSize = 16;
        public int Max = 5;

        public Color BgColor = new(0.06f, 0.06f, 0.08f, 0.94f);
        public Color TitleColor = new(0.95f, 0.95f, 0.97f);
        public Color TextColor = new(0.70f, 0.70f, 0.75f);
        public Color InfoColor = new(0.29f, 0.56f, 0.85f);
        public Color SuccessColor = new(0.26f, 0.71f, 0.51f);
        public Color WarnColor = new(0.94f, 0.63f, 0.21f);
        public Color ErrorColor = new(0.94f, 0.28f, 0.28f);

        public Anchor Anchor = Anchor.BottomRight;

        internal int Version { get; private set; }

        /// <summary>Gets the accent color associated with a notification type.</summary>
        public Color Accent(ToastType type) =>
            type switch
            {
                ToastType.Success => SuccessColor,
                ToastType.Warning => WarnColor,
                ToastType.Error => ErrorColor,
                _ => InfoColor,
            };

        internal void Apply(ToastConfiguration configuration)
        {
            if (configuration.Width.HasValue)
                Width = ClampFinite(configuration.Width.Value, 120f, Width);
            if (configuration.MaxHeight.HasValue)
                MaxHeight = ClampFinite(configuration.MaxHeight.Value, 48f, MaxHeight);
            if (configuration.Margin.HasValue)
                Margin = ClampFinite(configuration.Margin.Value, 0f, Margin);
            if (configuration.Gap.HasValue)
                Gap = ClampFinite(configuration.Gap.Value, 0f, Gap);
            if (configuration.Max.HasValue)
                Max = Math.Clamp(configuration.Max.Value, 0, ToastRuntime.Capacity);
            if (configuration.TitleSize.HasValue)
                TitleSize = Math.Clamp(configuration.TitleSize.Value, 8, 72);
            if (configuration.TextSize.HasValue)
                TextSize = Math.Clamp(configuration.TextSize.Value, 8, 72);
            if (configuration.Anchor.HasValue)
                Anchor = (Anchor)Math.Clamp(configuration.Anchor.Value, 0, 5);

            Version++;
        }

        private static float ClampFinite(float value, float minimum, float fallback) =>
            float.IsFinite(value) ? Math.Max(minimum, value) : fallback;
    }
}
