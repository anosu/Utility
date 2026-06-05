using System.Collections.Generic;
using UnityEngine;

namespace Utility.Toast
{
    // 公开 API 使用 int 常量代替枚举，避免 IL2CPP 互操作问题
    // type: 0=Info 1=Success 2=Warning 3=Error
    // anchor: 0=TL 1=TC 2=TR 3=BL 4=BC 5=BR

    /// <summary>Unity OnGUI Toast 管理器（MonoBehaviour 单例）</summary>
    public class ToastUI : MonoBehaviour
    {
        #region Constants

        public const int TYPE_INFO = 0, TYPE_SUCCESS = 1, TYPE_WARN = 2, TYPE_ERROR = 3;
        public const int ANCHOR_TL = 0, ANCHOR_TC = 1, ANCHOR_TR = 2;
        public const int ANCHOR_BL = 3, ANCHOR_BC = 4, ANCHOR_BR = 5;

        private const int TEX_SIZE = 24, RADIUS = 8;
        private const float PAD_TOP = 15f, PAD_BOT = 15f, TITLE_H = 28f, MSG_Y = 45f;
        private const int MAX_QUEUE = 50;

        #endregion

        #region Singleton

        // IL2CPP AOT 运行时裁剪了 GameObject.AddComponent 的泛型和非泛型重载，
        // 因此不能在此库内部调用 AddComponent。请由宿主插件通过框架自带方法注入：
        //
        //   BepInEx:     ToastUI.Instance = AddComponent<ToastUI>();
        //   MelonLoader: ToastUI.Instance = ...   // 类似
        //
        // Awake() 也会自动注册，因此以上赋值可省略。

        private static ToastUI _instance;

        public static ToastUI Instance
        {
            get => _instance;
            set
            {
                if (_instance != null && _instance != value)
                    Destroy(_instance.gameObject);
                _instance = value;
            }
        }

        #endregion

        #region Fields

        private readonly Queue<ToastData> _queue = new();
        private readonly List<ToastData> _active = new();
        private readonly object _lock = new();
        private readonly GUIContent _gc = new();

        private ToastStyle _style;
        private GUIStyle _titleStyle, _textStyle;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_instance != null)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            _style = new ToastStyle();
        }

        private void Update()
        {
            FlushQueue();

            float dt = Time.deltaTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var d = _active[i];
                d.Remaining -= dt;
                if (d.Expired) _active.RemoveAt(i);
            }

            FlushQueue();
        }

        private void OnGUI()
        {
            int count = _active.Count;
            if (count == 0) return;

            EnsureStyles();

            // 每帧新建纹理和样式（IL2CPP AOT 兼容 — 缓存纹理会跨帧失效）
            var bgTex = CreateRoundTex();
            var boxStyle = new GUIStyle
            {
                normal = { background = bgTex },
                padding = new RectOffset(0, 0, 0, 0),
                margin  = new RectOffset(0, 0, 0, 0),
                border  = new RectOffset(RADIUS, RADIUS, RADIUS, RADIUS)
            };

            float textW = _style.Width - _style.Bar - 24;
            float x = CalcX();

            // 一趟：计算高度 + 总高
            var heights = new float[count];
            float totalH = 0f;
            for (int i = 0; i < count; i++)
            {
                _gc.text = _active[i].Message;
                float h = Mathf.Max(PAD_TOP + _textStyle.CalcHeight(_gc, textW) + PAD_BOT, _style.MaxHeight);
                heights[i] = h;
                totalH += h + _style.Gap;
            }

            // 二趟：渲染
            float y = _style.Anchor switch
            {
                Anchor.BottomLeft or Anchor.BottomRight or Anchor.BottomCenter
                    => Screen.height - _style.Margin - totalH + _style.Gap,
                _ => _style.Margin
            };

            for (int i = 0; i < count; i++)
            {
                var d = _active[i];
                float h = heights[i], alpha = d.Alpha;
                if (alpha <= 0f) { y += h + _style.Gap; continue; }

                DrawCard(d, x, y, h, textW, alpha, boxStyle);
                y += h + _style.Gap;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        #endregion

        #region Public API

        /// <summary>显示 Toast。type: 0=Info 1=Success 2=Warning 3=Error</summary>
        public void Show(string title, string message, int type = 0, float duration = 3f)
        {
            var t = type switch
            {
                1 => ToastType.Success,
                2 => ToastType.Warning,
                3 => ToastType.Error,
                _ => ToastType.Info
            };

            var data = new ToastData(title, message, t, duration);
            lock (_lock)
            {
                if (_queue.Count > MAX_QUEUE) return;
                if (_active.Count < _style.Max)
                    _active.Add(data);
                else
                    _queue.Enqueue(data);
            }
        }

        public void Info(string title, string message, float duration = 3f)
            => Show(title, message, TYPE_INFO, duration);

        public void Success(string title, string message, float duration = 3f)
            => Show(title, message, TYPE_SUCCESS, duration);

        public void Warn(string title, string message, float duration = 4f)
            => Show(title, message, TYPE_WARN, duration);

        public void Error(string title, string message, float duration = 5f)
            => Show(title, message, TYPE_ERROR, duration);

        /// <summary>配置样式。anchor: 0=TL 1=TC 2=TR 3=BL 4=BC 5=BR</summary>
        public void Config(
            float? width = null, float? maxHeight = null,
            float? margin = null, float? gap = null,
            int? max = null, int? titleSize = null,
            int? textSize = null, int? anchor = null)
        {
            if (width.HasValue) _style.Width = width.Value;
            if (maxHeight.HasValue) _style.MaxHeight = maxHeight.Value;
            if (margin.HasValue) _style.Margin = margin.Value;
            if (gap.HasValue) _style.Gap = gap.Value;
            if (max.HasValue) _style.Max = max.Value;
            if (titleSize.HasValue) _style.TitleSize = titleSize.Value;
            if (textSize.HasValue) _style.TextSize = textSize.Value;
            if (anchor.HasValue) _style.Anchor = (Anchor)Mathf.Clamp(anchor.Value, 0, 5);

            _titleStyle = null; // 触发样式重建
        }

        public void Clear()
        {
            lock (_lock) { _active.Clear(); _queue.Clear(); }
        }

        public int Count
        {
            get { lock (_lock) return _active.Count + _queue.Count; }
        }

        #endregion

        #region Private Helpers

        private void FlushQueue()
        {
            lock (_lock)
            {
                while (_queue.Count > 0 && _active.Count < _style.Max)
                    _active.Add(_queue.Dequeue());
            }
        }

        private float CalcX()
        {
            switch (_style.Anchor)
            {
                case Anchor.TopLeft or Anchor.BottomLeft:
                    return _style.Margin;
                case Anchor.TopCenter or Anchor.BottomCenter:
                    return (Screen.width - _style.Width) * 0.5f;
                default:
                    return Screen.width - _style.Width - _style.Margin;
            }
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;

            _titleStyle = new GUIStyle
            {
                fontStyle = FontStyle.Bold,
                fontSize = _style.TitleSize,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = _style.TitleColor },
                wordWrap = false,
                clipping = TextClipping.Clip
            };

            _textStyle = new GUIStyle
            {
                fontSize = _style.TextSize,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = _style.TextColor },
                wordWrap = true,
                clipping = TextClipping.Clip
            };
        }

        private void DrawCard(ToastData d, float x, float y, float h, float textW, float alpha, GUIStyle boxStyle)
        {
            float bar = _style.Bar, cx = x + bar + 12;
            var old = GUI.color;

            // 背景
            var c = _style.BgColor; c.a *= alpha;
            GUI.color = c;
            GUI.Box(new Rect(x, y, _style.Width, h), "", boxStyle);

            // 强调条
            c = _style.Accent(d.Type); c.a *= alpha;
            GUI.color = c;
            GUI.Box(new Rect(x, y + RADIUS, bar, h - RADIUS * 2f), "", boxStyle);

            GUI.color = old;

            // 标题
            _titleStyle.normal.textColor = new Color(_style.TitleColor.r, _style.TitleColor.g, _style.TitleColor.b, alpha);
            GUI.Label(new Rect(cx, y + PAD_TOP, textW, TITLE_H), d.Title, _titleStyle);

            // 消息
            _textStyle.normal.textColor = new Color(_style.TextColor.r, _style.TextColor.g, _style.TextColor.b, alpha);
            GUI.Label(new Rect(cx, y + MSG_Y, textW, h - MSG_Y - PAD_BOT), d.Message, _textStyle);
        }

        private static Texture2D CreateRoundTex()
        {
            var tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.ARGB32, false);
            int r2 = RADIUS * RADIUS, max = TEX_SIZE - RADIUS - 1;

            for (int y = 0; y < TEX_SIZE; y++)
            {
                for (int x = 0; x < TEX_SIZE; x++)
                {
                    bool outside = false;
                    if (x < RADIUS && y < RADIUS)
                        outside = (x - RADIUS) * (x - RADIUS) + (y - RADIUS) * (y - RADIUS) > r2;
                    else if (x > max && y < RADIUS)
                        outside = (x - max) * (x - max) + (y - RADIUS) * (y - RADIUS) > r2;
                    else if (x < RADIUS && y > max)
                        outside = (x - RADIUS) * (x - RADIUS) + (y - max) * (y - max) > r2;
                    else if (x > max && y > max)
                        outside = (x - max) * (x - max) + (y - max) * (y - max) > r2;
                    tex.SetPixel(x, y, outside ? Color.clear : Color.white);
                }
            }
            tex.Apply();
            return tex;
        }

        #endregion
    }
}
