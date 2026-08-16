namespace Utility.Notifications.Internal
{
    internal readonly struct ToastSettingsPatch
    {
        internal ToastSettingsPatch(
            float? width,
            float? minimumHeight,
            float? margin,
            float? gap,
            int? maximumVisible,
            int? titleSize,
            int? textSize,
            ToastAnchor? anchor
        )
        {
            Width = width;
            MinimumHeight = minimumHeight;
            Margin = margin;
            Gap = gap;
            MaximumVisible = maximumVisible;
            TitleSize = titleSize;
            TextSize = textSize;
            Anchor = anchor;
        }

        internal float? Width { get; }
        internal float? MinimumHeight { get; }
        internal float? Margin { get; }
        internal float? Gap { get; }
        internal int? MaximumVisible { get; }
        internal int? TitleSize { get; }
        internal int? TextSize { get; }
        internal ToastAnchor? Anchor { get; }
    }
}
