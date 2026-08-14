using System.Collections.Generic;
using UnityEngine;

namespace Utility.Toast
{
    /// <summary>Unity OnGUI Toast 管理器（MonoBehaviour 单例，针对 IL2CPP 优化）</summary>
    public class ToastUI : MonoBehaviour
    {
        #region Constants

        public const int TYPE_INFO = 0,
            TYPE_SUCCESS = 1,
            TYPE_WARN = 2,
            TYPE_ERROR = 3;
        public const int ANCHOR_TL = 0,
            ANCHOR_TC = 1,
            ANCHOR_TR = 2;
        public const int ANCHOR_BL = 3,
            ANCHOR_BC = 4,
            ANCHOR_BR = 5;

        private const int TEX_SIZE = 24,
            RADIUS = 8;
        private const float PAD_TOP = 15f,
            PAD_BOT = 15f,
            TITLE_H = 28f,
            MSG_Y = 45f;
        private const int MAX_QUEUE = 50;

        #endregion

        #region Singleton

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

        // 缓存数组，避免 OnGUI 中频繁 new float[]
        private readonly float[] _heightsCache = new float[MAX_QUEUE];

        private ToastStyle _style;
        private GUIStyle _titleStyle,
            _textStyle,
            _boxStyle;
        private Texture2D _bgTex;

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

            Toast.OnUIReady();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var d = _active[i];
                d.Remaining -= dt;
                if (d.Expired)
                    _active.RemoveAt(i);
            }

            // 将入队逻辑严格限制在主线程处理，避免后台线程调用 Show() 引发集合异常
            lock (_lock)
            {
                while (_queue.Count > 0 && _active.Count < _style.Max)
                {
                    _active.Add(_queue.Dequeue());
                }
            }
        }

        private void OnGUI()
        {
            int count = _active.Count;
            if (count == 0)
                return;

            EnsureStyles();

            float textW = _style.Width - _style.Bar - 24;
            float x = CalcX();

            // 一趟：计算高度 + 总高 (使用缓存数组替代 new float[])
            float totalH = 0f;
            for (int i = 0; i < count; i++)
            {
                _gc.text = _active[i].Message;
                float h = Mathf.Max(
                    PAD_TOP + _textStyle.CalcHeight(_gc, textW) + PAD_BOT,
                    _style.MaxHeight
                );
                _heightsCache[i] = h;
                totalH += h + _style.Gap;
            }

            // 二趟：渲染
            float y = _style.Anchor switch
            {
                Anchor.BottomLeft or Anchor.BottomRight or Anchor.BottomCenter => Screen.height
                    - _style.Margin
                    - totalH
                    + _style.Gap,
                _ => _style.Margin,
            };

            for (int i = 0; i < count; i++)
            {
                var d = _active[i];
                float h = _heightsCache[i],
                    alpha = d.Alpha;
                if (alpha <= 0f)
                {
                    y += h + _style.Gap;
                    continue;
                }

                DrawCard(d, x, y, h, textW, alpha, _boxStyle);
                y += h + _style.Gap;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;

            // 清理非托管资源
            if (_bgTex != null && _bgTex)
                Destroy(_bgTex);
        }

        #endregion

        #region Public API

        public void Show(string title, string message, int type = 0, float duration = 3f)
        {
            var t = type switch
            {
                1 => ToastType.Success,
                2 => ToastType.Warning,
                3 => ToastType.Error,
                _ => ToastType.Info,
            };

            var data = new ToastData(title, message, t, duration);

            // 异步安全：只负责推入队列
            lock (_lock)
            {
                if (_queue.Count + _active.Count >= MAX_QUEUE)
                    return;
                _queue.Enqueue(data);
            }
        }

        public void Info(string title, string message, float duration = 3f) =>
            Show(title, message, TYPE_INFO, duration);

        public void Success(string title, string message, float duration = 3f) =>
            Show(title, message, TYPE_SUCCESS, duration);

        public void Warn(string title, string message, float duration = 4f) =>
            Show(title, message, TYPE_WARN, duration);

        public void Error(string title, string message, float duration = 5f) =>
            Show(title, message, TYPE_ERROR, duration);

        public void Config(
            float? width = null,
            float? maxHeight = null,
            float? margin = null,
            float? gap = null,
            int? max = null,
            int? titleSize = null,
            int? textSize = null,
            int? anchor = null
        )
        {
            if (width.HasValue)
                _style.Width = width.Value;
            if (maxHeight.HasValue)
                _style.MaxHeight = maxHeight.Value;
            if (margin.HasValue)
                _style.Margin = margin.Value;
            if (gap.HasValue)
                _style.Gap = gap.Value;
            if (max.HasValue)
                _style.Max = max.Value;
            if (titleSize.HasValue)
                _style.TitleSize = titleSize.Value;
            if (textSize.HasValue)
                _style.TextSize = textSize.Value;
            if (anchor.HasValue)
                _style.Anchor = (Anchor)Mathf.Clamp(anchor.Value, 0, 5);

            _titleStyle = null; // 触发样式重建
        }

        public void Clear()
        {
            lock (_lock)
            {
                _active.Clear();
                _queue.Clear();
            }
        }

        public int Count
        {
            get
            {
                lock (_lock)
                    return _active.Count + _queue.Count;
            }
        }

        #endregion

        #region Private Helpers

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
            // IL2CPP 安全检测：利用重载的 ! 运算符检查底层 C++ 对象是否存活
            if (_bgTex == null || !_bgTex)
            {
                _bgTex = CreateRoundTex();
                // 核心修复：防止被 Unity 在场景切换或内部清理时意外 GC
                _bgTex.hideFlags = HideFlags.HideAndDontSave;

                _boxStyle = new GUIStyle
                {
                    normal = { background = _bgTex },
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    border = new RectOffset(RADIUS, RADIUS, RADIUS, RADIUS),
                };
            }

            if (_titleStyle != null)
                return;

            _titleStyle = new GUIStyle
            {
                fontStyle = FontStyle.Bold,
                fontSize = _style.TitleSize,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = _style.TitleColor },
                wordWrap = false,
                clipping = TextClipping.Clip,
            };

            _textStyle = new GUIStyle
            {
                fontSize = _style.TextSize,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = _style.TextColor },
                wordWrap = true,
                clipping = TextClipping.Clip,
            };
        }

        private void DrawCard(
            ToastData d,
            float x,
            float y,
            float h,
            float textW,
            float alpha,
            GUIStyle boxStyle
        )
        {
            float bar = _style.Bar,
                cx = x + bar + 12;
            var old = GUI.color;

            // 背景
            var c = _style.BgColor;
            c.a *= alpha;
            GUI.color = c;
            GUI.Box(new Rect(x, y, _style.Width, h), "", boxStyle);

            // 强调条
            c = _style.Accent(d.Type);
            c.a *= alpha;
            GUI.color = c;
            GUI.Box(new Rect(x, y + RADIUS, bar, h - RADIUS * 2f), "", boxStyle);

            GUI.color = old;

            // 标题
            _titleStyle.normal.textColor = new Color(
                _style.TitleColor.r,
                _style.TitleColor.g,
                _style.TitleColor.b,
                alpha
            );
            GUI.Label(new Rect(cx, y + PAD_TOP, textW, TITLE_H), d.Title, _titleStyle);

            // 消息
            _textStyle.normal.textColor = new Color(
                _style.TextColor.r,
                _style.TextColor.g,
                _style.TextColor.b,
                alpha
            );
            GUI.Label(new Rect(cx, y + MSG_Y, textW, h - MSG_Y - PAD_BOT), d.Message, _textStyle);
        }

        private static Texture2D CreateRoundTex()
        {
            var tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.ARGB32, false);
            int r2 = RADIUS * RADIUS,
                max = TEX_SIZE - RADIUS - 1;

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
