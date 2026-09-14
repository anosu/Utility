using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using UnityEngine.UI;
using Utility.Diagnostics;
using UnityObject = UnityEngine.Object;

namespace Utility.Notifications.Internal
{
    internal sealed class UguiToastRenderer : IFrameToastRenderer
    {
        private readonly List<Card> _cards = new(ToastRuntime.Capacity);
        private readonly float[] _heightCache = new float[ToastRuntime.Capacity];
        private readonly Font _font;
        private readonly GameObject _root;
        private bool _titleMeasurementUnavailable;
        private bool _bodyMeasurementUnavailable;

        internal UguiToastRenderer(Transform host)
        {
            _font = ToastFont.Get();
            _root = CreateUiObject(
                "Utility.Notifications.uGUI",
                typeof(RectTransform),
                typeof(Canvas)
            );
            try
            {
                _root.hideFlags = HideFlags.HideAndDontSave;
                _root.transform.SetParent(host, worldPositionStays: false);

                Canvas canvas = GetRequiredComponent<Canvas>(_root);
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 32760;
            }
            catch
            {
                UnityObject.Destroy(_root);
                throw;
            }
        }

        public void Dispose()
        {
            _cards.Clear();
            UnityObject.Destroy(_root);
        }

        public void RenderFrame(
            IReadOnlyList<ToastItem> active,
            ToastTheme style,
            ToastLayout layout
        )
        {
            EnsureCardCount(active.Count);

            float totalHeight = 0f;
            for (int i = 0; i < active.Count; i++)
            {
                PrepareText(_cards[i], active[i], layout);
                _heightCache[i] = CalculateHeight(_cards[i], active[i].Message, layout);
            }

            layout.FitCardHeights(_heightCache, active.Count);
            for (int i = 0; i < active.Count; i++)
            {
                _cards[i].Height = _heightCache[i];
                totalHeight += _heightCache[i];
            }

            totalHeight += layout.Gap * Math.Max(0, active.Count - 1);
            float offset = 0f;
            for (int i = 0; i < active.Count; i++)
            {
                ToastItem item = active[i];
                Card card = _cards[i];
                PositionCard(card, style, layout, totalHeight, offset);
                UpdateCard(card, item, style);
                offset += card.Height + layout.Gap;
            }

            for (int i = active.Count; i < _cards.Count; i++)
                _cards[i].Root.SetActive(false);
        }

        private void EnsureCardCount(int count)
        {
            while (_cards.Count < count)
                _cards.Add(CreateCard(_cards.Count));
        }

        private Card CreateCard(int index)
        {
            GameObject root = CreateUiObject($"Toast.{index}", typeof(RectTransform));
            root.transform.SetParent(_root.transform, worldPositionStays: false);
            var backgroundBands = new Image[ToastMetrics.RoundedBandCount];
            for (int i = 0; i < backgroundBands.Length; i++)
            {
                GameObject bandObject = CreateUiObject(
                    $"Background.{i}",
                    typeof(RectTransform),
                    typeof(Image)
                );
                bandObject.transform.SetParent(root.transform, worldPositionStays: false);
                backgroundBands[i] = GetRequiredComponent<Image>(bandObject);
                backgroundBands[i].raycastTarget = false;
            }

            GameObject accentObject = CreateUiObject(
                "Accent",
                typeof(RectTransform),
                typeof(Image)
            );
            accentObject.transform.SetParent(root.transform, worldPositionStays: false);
            RectTransform accentRect = GetRequiredComponent<RectTransform>(accentObject);
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            Image accent = GetRequiredComponent<Image>(accentObject);
            accent.raycastTarget = false;

            Text title = CreateText(root.transform, "Title", FontStyle.Bold, TextAnchor.UpperLeft);
            Text body = CreateText(root.transform, "Body", FontStyle.Normal, TextAnchor.UpperLeft);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;

            return new Card(
                root,
                GetRequiredComponent<RectTransform>(root),
                backgroundBands,
                accent,
                title,
                body
            );
        }

        private Text CreateText(
            Transform parent,
            string name,
            FontStyle fontStyle,
            TextAnchor alignment
        )
        {
            GameObject gameObject = CreateUiObject(name, typeof(RectTransform), typeof(Text));
            gameObject.transform.SetParent(parent, worldPositionStays: false);
            Text text = GetRequiredComponent<Text>(gameObject);
            text.font = _font;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.resizeTextForBestFit = false;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        private static void PositionCard(
            Card card,
            ToastTheme style,
            ToastLayout layout,
            float totalHeight,
            float offset
        )
        {
            bool bottom =
                style.Anchor
                is ToastAnchor.BottomLeft
                    or ToastAnchor.BottomCenter
                    or ToastAnchor.BottomRight;
            float anchorX = style.Anchor switch
            {
                ToastAnchor.TopLeft or ToastAnchor.BottomLeft => 0f,
                ToastAnchor.TopCenter or ToastAnchor.BottomCenter => 0.5f,
                _ => 1f,
            };
            float positionX = layout.CalculateUguiX(style.Anchor);
            float positionY = bottom
                ? layout.UguiBottomInset + totalHeight - offset - card.Height
                : -layout.UguiTopInset - offset;

            card.Rect.anchorMin = new Vector2(anchorX, bottom ? 0f : 1f);
            card.Rect.anchorMax = card.Rect.anchorMin;
            card.Rect.pivot = new Vector2(anchorX, bottom ? 0f : 1f);
            card.Rect.anchoredPosition = new Vector2(positionX, positionY);
            card.Rect.sizeDelta = new Vector2(layout.Width, card.Height);

            PositionBackgroundBands(
                card.BackgroundBands,
                layout.Width,
                card.Height,
                layout.SpacingScale
            );

            float accentInset = Math.Min(layout.CornerRadius, card.Height * 0.5f);
            RectTransform accentRect = card.Accent.rectTransform;
            accentRect.offsetMin = new Vector2(0f, accentInset);
            accentRect.offsetMax = new Vector2(
                Math.Min(layout.AccentWidth, layout.Width),
                -accentInset
            );

            float titleHeight = ToastMetrics.CalculateTitleHeight(layout);
            float bodyTop = ToastMetrics.CalculateMessageTop(layout);
            SetTopRect(card.Title.rectTransform, layout.VerticalInset, titleHeight, layout);
            SetTopRect(
                card.Body.rectTransform,
                bodyTop,
                ToastMetrics.CalculateMessageHeight(card.Height, bodyTop, layout),
                layout
            );
        }

        private static void SetTopRect(
            RectTransform rect,
            float top,
            float height,
            ToastLayout layout
        )
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(-layout.ContentInset * 2f, height);
        }

        private static void UpdateCard(Card card, ToastItem item, ToastTheme style)
        {
            float alpha = item.Alpha;
            card.Root.SetActive(alpha > 0f);
            if (alpha <= 0f)
                return;

            Color backgroundColor = WithAlpha(
                style.BackgroundColor,
                style.BackgroundColor.a * alpha
            );
            for (int i = 0; i < card.BackgroundBands.Length; i++)
                card.BackgroundBands[i].color = backgroundColor;
            Color accent = style.Accent(item.Kind);
            card.Accent.color = WithAlpha(accent, accent.a * alpha);

            card.Title.color = WithAlpha(style.TitleColor, style.TitleColor.a * alpha);

            card.Body.color = WithAlpha(style.TextColor, style.TextColor.a * alpha);
        }

        private static void PositionBackgroundBands(
            Image[] bands,
            float width,
            float height,
            float spacingScale
        )
        {
            for (int i = 0; i < bands.Length; i++)
            {
                ToastMetrics.GetRoundedBand(
                    width,
                    height,
                    i,
                    out float top,
                    out float bandHeight,
                    out float inset,
                    spacingScale
                );
                RectTransform rect = bands[i].rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -top);
                rect.sizeDelta = new Vector2(-inset * 2f, bandHeight);
            }
        }

        private void PrepareText(Card card, ToastItem item, ToastLayout layout)
        {
            float height = card.Height > 0f ? card.Height : layout.MinimumHeight;
            card.Rect.sizeDelta = new Vector2(layout.Width, height);
            float bodyTop = ToastMetrics.CalculateMessageTop(layout);
            SetTopRect(
                card.Body.rectTransform,
                bodyTop,
                ToastMetrics.CalculateMessageHeight(height, bodyTop, layout),
                layout
            );
            card.Title.fontSize = layout.TitleSize;
            card.Title.text = item.GetDisplayTitle(
                ToastMetrics.ContentWidth(layout),
                layout.TitleSize,
                this,
                value => MeasureTitleWidth(card.Title, value, layout.TitleSize)
            );
            card.Body.fontSize = layout.TextSize;
            card.Body.text = item.Message;
        }

        private float MeasureTitleWidth(Text title, string value, int fontSize)
        {
            if (!_titleMeasurementUnavailable)
            {
                try
                {
                    float width = ReadTitleWidth(title, value);
                    if (float.IsFinite(width) && (width > 0f || value.Length == 0))
                        return width;
                    throw new InvalidOperationException("uGUI returned an invalid text width.");
                }
                catch (Exception exception)
                {
                    _titleMeasurementUnavailable = true;
                    Logging.WriteRecoverable(
                        LogLevel.Debug,
                        "Toast",
                        "uGUI title measurement is unavailable; using estimated widths.",
                        exception
                    );
                }
            }
            return ToastTextMetrics.EstimateWidth(value, fontSize);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float ReadTitleWidth(Text title, string value)
        {
            title.text = value;
            return title.preferredWidth;
        }

        private float CalculateHeight(Card card, string message, ToastLayout layout)
        {
            float bodyTop = ToastMetrics.CalculateMessageTop(layout);
            float width = ToastMetrics.ContentWidth(layout);
            if (
                card.MeasuredMessage != message
                || card.MeasuredWidth != width
                || card.MeasuredFontSize != layout.TextSize
            )
            {
                card.MeasuredBodyHeight = MeasureBodyHeight(
                    card.Body,
                    message,
                    width,
                    layout.TextSize
                );
                card.MeasuredMessage = message;
                card.MeasuredWidth = width;
                card.MeasuredFontSize = layout.TextSize;
            }
            return Math.Max(
                layout.MinimumHeight,
                bodyTop + card.MeasuredBodyHeight + layout.VerticalInset
            );
        }

        private float MeasureBodyHeight(Text body, string message, float width, int fontSize)
        {
            if (!_bodyMeasurementUnavailable)
            {
                try
                {
                    float height = ReadBodyHeight(body);
                    if (float.IsFinite(height) && (height > 0f || message.Length == 0))
                        return Math.Max(height, ToastMetrics.CalculateLineHeight(fontSize));
                    throw new InvalidOperationException("uGUI returned an invalid text height.");
                }
                catch (Exception exception)
                {
                    _bodyMeasurementUnavailable = true;
                    Logging.WriteRecoverable(
                        LogLevel.Debug,
                        "Toast",
                        "uGUI body measurement is unavailable; using estimated heights.",
                        exception
                    );
                }
            }
            return ToastTextMetrics.EstimateLineCount(message, width, fontSize)
                * ToastMetrics.CalculateLineHeight(fontSize);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float ReadBodyHeight(Text body) => body.preferredHeight;

        private static GameObject CreateUiObject(string name, params Type[] components)
        {
            var il2CppTypes = new Il2CppSystem.Type[components.Length];
            for (int i = 0; i < components.Length; i++)
                il2CppTypes[i] = Il2CppType.From(components[i]);

            return new GameObject(name, il2CppTypes);
        }

        private static T GetRequiredComponent<T>(GameObject gameObject)
            where T : Component
        {
            Component? component = gameObject.GetComponent(Il2CppType.Of<T>());
            T? typed = component?.TryCast<T>();
            return typed
                ?? throw new InvalidOperationException(
                    $"Unity did not create the required {typeof(T).FullName} component."
                );
        }

        private static Color WithAlpha(Color color, float alpha) =>
            new(color.r, color.g, color.b, alpha);

        private sealed class Card
        {
            internal Card(
                GameObject root,
                RectTransform rect,
                Image[] backgroundBands,
                Image accent,
                Text title,
                Text body
            )
            {
                Root = root;
                Rect = rect;
                BackgroundBands = backgroundBands;
                Accent = accent;
                Title = title;
                Body = body;
            }

            internal GameObject Root { get; }
            internal RectTransform Rect { get; }
            internal Image[] BackgroundBands { get; }
            internal Image Accent { get; }
            internal Text Title { get; }
            internal Text Body { get; }
            internal float Height { get; set; }
            internal string? MeasuredMessage { get; set; }
            internal float MeasuredWidth { get; set; }
            internal int MeasuredFontSize { get; set; }
            internal float MeasuredBodyHeight { get; set; }
        }
    }
}
