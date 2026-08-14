using UnityEngine;

namespace Utility.Toast
{
    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error,
    }

    /// <summary>锚点位置</summary>
    public enum Anchor
    {
        TopLeft,
        TopCenter,
        TopRight,
        BottomLeft,
        BottomCenter,
        BottomRight,
    }

    /// <summary>单条 Toast 数据</summary>
    public class ToastData
    {
        public string Title,
            Message;
        public ToastType Type;
        public float Duration,
            Remaining;

        public ToastData(string title, string message, ToastType type, float duration)
        {
            Title = title;
            Message = message;
            Type = type;
            Duration = Mathf.Max(duration, 1f);
            Remaining = Duration;
        }

        public bool Expired => Remaining <= 0f;

        public float Alpha
        {
            get
            {
                const float In = 0.3f,
                    Out = 0.5f;
                if (Remaining > Duration - In)
                    return (Duration - Remaining) / In;
                if (Remaining < Out)
                    return Mathf.Max(0, Remaining / Out);
                return 1f;
            }
        }
    }

    /// <summary>Toast 样式配置（简约暗色风格）</summary>
    public class ToastStyle
    {
        public float Width = 425f,
            MaxHeight = 105f,
            Margin = 20f,
            Gap = 15f,
            Bar = 6f;
        public int TitleSize = 19,
            TextSize = 16,
            Max = 5;

        public Color BgColor = new(0.06f, 0.06f, 0.08f, 0.94f);
        public Color TitleColor = new(0.95f, 0.95f, 0.97f);
        public Color TextColor = new(0.70f, 0.70f, 0.75f);
        public Color InfoColor = new(0.29f, 0.56f, 0.85f);
        public Color SuccessColor = new(0.26f, 0.71f, 0.51f);
        public Color WarnColor = new(0.94f, 0.63f, 0.21f);
        public Color ErrorColor = new(0.94f, 0.28f, 0.28f);

        public Anchor Anchor = Anchor.BottomRight;

        public Color Accent(ToastType t) =>
            t switch
            {
                ToastType.Success => SuccessColor,
                ToastType.Warning => WarnColor,
                ToastType.Error => ErrorColor,
                _ => InfoColor,
            };
    }
}
