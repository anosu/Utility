using System;
using UnityEngine;

namespace Utility.Notifications.Internal
{
    internal sealed class ToastTheme
    {
        internal float Width = 425f;
        internal float MinimumHeight = 105f;
        internal float Margin = 20f;
        internal float Gap = 15f;
        internal int TitleSize = 19;
        internal int TextSize = 16;
        internal int MaximumVisible = 4;

        internal Color BackgroundColor = new(0.06f, 0.06f, 0.08f, 0.94f);
        internal Color TitleColor = new(0.95f, 0.95f, 0.97f);
        internal Color TextColor = new(0.70f, 0.70f, 0.75f);
        internal Color InformationColor = new(0.29f, 0.56f, 0.85f);
        internal Color SuccessColor = new(0.26f, 0.71f, 0.51f);
        internal Color WarningColor = new(0.94f, 0.63f, 0.21f);
        internal Color ErrorColor = new(0.94f, 0.28f, 0.28f);

        internal ToastAnchor Anchor = ToastAnchor.BottomRight;
        internal int Version { get; private set; }

        internal Color Accent(ToastKind kind) =>
            kind switch
            {
                ToastKind.Success => SuccessColor,
                ToastKind.Warning => WarningColor,
                ToastKind.Error => ErrorColor,
                _ => InformationColor,
            };

        internal void Apply(ToastSettingsPatch settings)
        {
            if (settings.Width.HasValue)
                Width = ClampFinite(settings.Width.Value, 120f, Width);
            if (settings.MinimumHeight.HasValue)
                MinimumHeight = ClampFinite(settings.MinimumHeight.Value, 48f, MinimumHeight);
            if (settings.Margin.HasValue)
                Margin = ClampFinite(settings.Margin.Value, 0f, Margin);
            if (settings.Gap.HasValue)
                Gap = ClampFinite(settings.Gap.Value, 0f, Gap);
            if (settings.MaximumVisible.HasValue)
                MaximumVisible = Math.Clamp(
                    settings.MaximumVisible.Value,
                    0,
                    ToastRuntime.Capacity
                );
            if (settings.TitleSize.HasValue)
                TitleSize = Math.Clamp(settings.TitleSize.Value, 8, 72);
            if (settings.TextSize.HasValue)
                TextSize = Math.Clamp(settings.TextSize.Value, 8, 72);
            if (settings.Anchor.HasValue)
                Anchor = (ToastAnchor)Math.Clamp((int)settings.Anchor.Value, 0, 5);

            Version++;
        }

        private static float ClampFinite(float value, float minimum, float fallback) =>
            float.IsFinite(value) ? Math.Max(minimum, value) : fallback;
    }
}
