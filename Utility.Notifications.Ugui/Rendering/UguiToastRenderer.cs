using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using UnityEngine.UI;
using Utility.Diagnostics;
using Utility.Notifications.Internal;
using UnityObject = UnityEngine.Object;

namespace Utility.Notifications.Ugui
{
    internal sealed class UguiToastRenderer : IFrameToastRenderer
    {
        private const float AccentWidth = 6f;
        private const float BodyTop = 45f;
        private const float BottomPadding = 15f;
        private const float HorizontalPadding = 18f;
        private const float TitleHeight = 28f;

        private readonly List<Card> _cards = new(ToastRuntime.Capacity);
        private readonly Font _font;
        private readonly GameObject _root;
        private readonly Sprite? _roundedCardSprite;

        internal UguiToastRenderer(Transform host)
        {
            _font = LoadBuiltinFont();
            _roundedCardSprite = TryLoadRoundedCardSprite();
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

        public void RenderFrame(IReadOnlyList<ToastItem> active, ToastTheme style)
        {
            EnsureCardCount(active.Count);

            float totalHeight = 0f;
            for (int i = 0; i < active.Count; i++)
            {
                float height = CalculateHeight(active[i].Message, style);
                _cards[i].Height = height;
                totalHeight += height;
            }

            totalHeight += style.Gap * Math.Max(0, active.Count - 1);
            float offset = 0f;
            for (int i = 0; i < active.Count; i++)
            {
                ToastItem item = active[i];
                Card card = _cards[i];
                PositionCard(card, style, totalHeight, offset);
                UpdateCard(card, item, style);
                offset += card.Height + style.Gap;
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
            GameObject root = CreateUiObject(
                $"Toast.{index}",
                typeof(RectTransform),
                typeof(Image)
            );
            root.transform.SetParent(_root.transform, worldPositionStays: false);
            Image background = GetRequiredComponent<Image>(root);
            background.raycastTarget = false;
            TryApplyRoundedCardSprite(background);

            GameObject accentObject = CreateUiObject(
                "Accent",
                typeof(RectTransform),
                typeof(Image)
            );
            accentObject.transform.SetParent(root.transform, worldPositionStays: false);
            RectTransform accentRect = GetRequiredComponent<RectTransform>(accentObject);
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.offsetMin = new Vector2(0f, 8f);
            accentRect.offsetMax = new Vector2(AccentWidth, -8f);
            Image accent = GetRequiredComponent<Image>(accentObject);
            accent.raycastTarget = false;

            Text title = CreateText(root.transform, "Title", FontStyle.Bold, TextAnchor.UpperLeft);
            Text body = CreateText(root.transform, "Body", FontStyle.Normal, TextAnchor.UpperLeft);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;

            return new Card(
                root,
                GetRequiredComponent<RectTransform>(root),
                background,
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
            text.raycastTarget = false;
            return text;
        }

        private static void PositionCard(
            Card card,
            ToastTheme style,
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
            float positionX = anchorX switch
            {
                0f => style.Margin,
                1f => -style.Margin,
                _ => 0f,
            };
            float positionY = bottom
                ? style.Margin + totalHeight - offset - card.Height
                : -style.Margin - offset;

            card.Rect.anchorMin = new Vector2(anchorX, bottom ? 0f : 1f);
            card.Rect.anchorMax = card.Rect.anchorMin;
            card.Rect.pivot = new Vector2(anchorX, bottom ? 0f : 1f);
            card.Rect.anchoredPosition = new Vector2(positionX, positionY);
            card.Rect.sizeDelta = new Vector2(style.Width, card.Height);

            SetTopRect(card.Title.rectTransform, HorizontalPadding, 15f, TitleHeight);
            SetTopRect(
                card.Body.rectTransform,
                HorizontalPadding,
                BodyTop,
                card.Height - BodyTop - BottomPadding
            );
        }

        private static void SetTopRect(
            RectTransform rect,
            float horizontalPadding,
            float top,
            float height
        )
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(AccentWidth * 0.5f, -top);
            rect.sizeDelta = new Vector2(-horizontalPadding * 2f - AccentWidth, height);
        }

        private static void UpdateCard(Card card, ToastItem item, ToastTheme style)
        {
            float alpha = item.Alpha;
            card.Root.SetActive(alpha > 0f);
            if (alpha <= 0f)
                return;

            card.Background.color = WithAlpha(
                style.BackgroundColor,
                style.BackgroundColor.a * alpha
            );
            Color accent = style.Accent(item.Kind);
            card.Accent.color = WithAlpha(accent, accent.a * alpha);

            card.Title.text = item.Title;
            card.Title.fontSize = style.TitleSize;
            card.Title.color = WithAlpha(style.TitleColor, style.TitleColor.a * alpha);

            card.Body.text = item.Message;
            card.Body.fontSize = style.TextSize;
            card.Body.color = WithAlpha(style.TextColor, style.TextColor.a * alpha);
        }

        private static float CalculateHeight(string message, ToastTheme style)
        {
            int maxCharacters = Math.Max(
                8,
                (int)((style.Width - 42f) / Math.Max(8f, style.TextSize * 0.55f))
            );
            int lineCount = 1;
            int column = 0;
            for (int i = 0; i < message.Length; i++)
            {
                if (message[i] == '\n')
                {
                    lineCount++;
                    column = 0;
                }
                else
                {
                    if (column >= maxCharacters)
                    {
                        lineCount++;
                        column = 0;
                    }

                    column++;
                }
            }

            float bodyHeight = lineCount * (style.TextSize + 4f);
            return Math.Max(style.MinimumHeight, BodyTop + bodyHeight + BottomPadding);
        }

        private static Font LoadBuiltinFont()
        {
            Exception? lastError = null;
            string[] candidates = { "LegacyRuntime.ttf", "Arial.ttf" };
            for (int i = 0; i < candidates.Length; i++)
            {
                try
                {
                    UnityObject? asset = Resources.GetBuiltinResource(
                        Il2CppType.Of<Font>(),
                        candidates[i]
                    );
                    Font? font = asset?.TryCast<Font>();
                    if (!ReferenceEquals(font, null))
                        return font;
                }
                catch (Exception exception)
                {
                    lastError = exception;
                    Logging.Write(
                        LogLevel.Debug,
                        "Toast",
                        $"Built-in uGUI font '{candidates[i]}' is unavailable.",
                        exception
                    );
                }
            }

            throw new InvalidOperationException(
                "No compatible built-in Unity UI font is available.",
                lastError
            );
        }

        private static Sprite? TryLoadRoundedCardSprite()
        {
            Exception? lastError = null;
            string[] candidates = { "UI/Skin/UISprite.psd", "UI/Skin/Background.psd" };
            for (int i = 0; i < candidates.Length; i++)
            {
                try
                {
                    UnityObject? asset = Resources.GetBuiltinResource(
                        Il2CppType.Of<Sprite>(),
                        candidates[i]
                    );
                    Sprite? sprite = asset?.TryCast<Sprite>();
                    if (!ReferenceEquals(sprite, null))
                        return sprite;
                }
                catch (Exception exception)
                {
                    lastError = exception;
                }
            }

            Logging.Write(
                LogLevel.Debug,
                "Toast",
                "No built-in sliced UI sprite is available; uGUI cards will use square corners.",
                lastError
            );
            return null;
        }

        private void TryApplyRoundedCardSprite(Image background)
        {
            if (ReferenceEquals(_roundedCardSprite, null))
                return;

            try
            {
                background.sprite = _roundedCardSprite;
                background.type = Image.Type.Sliced;
                background.fillCenter = true;
            }
            catch (Exception exception)
            {
                Logging.Write(
                    LogLevel.Debug,
                    "Toast",
                    "Applying the built-in rounded uGUI sprite failed; using square corners.",
                    exception
                );
            }
        }

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
                Image background,
                Image accent,
                Text title,
                Text body
            )
            {
                Root = root;
                Rect = rect;
                Background = background;
                Accent = accent;
                Title = title;
                Body = body;
            }

            internal GameObject Root { get; }
            internal RectTransform Rect { get; }
            internal Image Background { get; }
            internal Image Accent { get; }
            internal Text Title { get; }
            internal Text Body { get; }
            internal float Height { get; set; }
        }
    }
}
