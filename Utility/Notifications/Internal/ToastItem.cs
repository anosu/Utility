using System;

namespace Utility.Notifications.Internal
{
    internal sealed class ToastItem
    {
        internal ToastItem(string title, string message, ToastKind kind, float duration)
        {
            Title = title;
            Message = message;
            Kind = kind;
            Duration = float.IsFinite(duration) ? Math.Max(duration, 1f) : 3f;
            Remaining = Duration;
        }

        internal string Title { get; }
        internal string Message { get; }
        internal ToastKind Kind { get; }
        internal float Duration { get; }
        internal float Remaining { get; set; }
        internal string? CompatibilityMessage { get; set; }
        internal int CompatibilityCharacters { get; set; }
        internal int CompatibilityLineCount { get; set; }
        internal bool Expired => Remaining <= 0f;

        internal float Alpha
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
}
