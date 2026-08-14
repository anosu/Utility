using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Utility.Toast
{
    internal sealed class ToastRenderer : IDisposable
    {
        private const float BarWidth = 6f;
        private const float MessageTop = 45f;
        private const float PaddingBottom = 15f;
        private const float PaddingTop = 15f;
        private const float TitleHeight = 28f;
        private const int CornerRadius = 8;

        private readonly GUIContent _content = new();
        private readonly float[] _heightCache = new float[ToastRuntime.Capacity];

        private bool _compatibilityMode;
        private bool _disabled;
        private GUIStyle? _textStyle;
        private GUIStyle? _titleStyle;
        private int _styleVersion = -1;

        internal Exception? LastError { get; private set; }

        internal void Render(IReadOnlyList<ToastData> active, ToastStyle style)
        {
            if (_disabled || active.Count == 0)
                return;

            if (_compatibilityMode)
            {
                TryRenderCompatibility(active, style);
                return;
            }

            try
            {
                RenderStyled(active, style);
                LastError = null;
            }
            catch (Exception exception)
            {
                LastError = exception;
                _compatibilityMode = true;
                TryRenderCompatibility(active, style);
            }
        }

        internal void Disable(Exception exception)
        {
            LastError = exception;
            _disabled = true;
        }

        public void Dispose()
        {
            _textStyle = null;
            _titleStyle = null;
        }

        private void RenderStyled(IReadOnlyList<ToastData> active, ToastStyle style)
        {
            int count = active.Count;
            EnsureStyles(style);

            float textWidth = style.Width - BarWidth - 24f;
            float totalHeight = 0f;
            for (int i = 0; i < count; i++)
            {
                _content.text = active[i].Message;
                float contentHeight =
                    MessageTop + _textStyle!.CalcHeight(_content, textWidth) + PaddingBottom;
                float height = Mathf.Max(contentHeight, style.MaxHeight);
                _heightCache[i] = height;
                totalHeight += height;
            }

            totalHeight += style.Gap * Math.Max(0, count - 1);
            float x = CalculateX(style);
            float y = style.Anchor switch
            {
                Anchor.BottomLeft or Anchor.BottomRight or Anchor.BottomCenter => Screen.height
                    - style.Margin
                    - totalHeight,
                _ => style.Margin,
            };

            for (int i = 0; i < count; i++)
            {
                ToastData item = active[i];
                float height = _heightCache[i];
                if (item.Alpha > 0f)
                    DrawCard(item, style, x, y, height, textWidth);

                y += height + style.Gap;
            }
        }

        private static float CalculateX(ToastStyle style) =>
            style.Anchor switch
            {
                Anchor.TopLeft or Anchor.BottomLeft => style.Margin,
                Anchor.TopCenter or Anchor.BottomCenter => (Screen.width - style.Width) * 0.5f,
                _ => Screen.width - style.Width - style.Margin,
            };

        private void DrawCard(
            ToastData item,
            ToastStyle style,
            float x,
            float y,
            float height,
            float textWidth
        )
        {
            float alpha = item.Alpha;
            float contentX = x + BarWidth + 12f;
            Color previousColor = GUI.color;
            Color previousContentColor = GUI.contentColor;

            try
            {
                Color color = style.BgColor;
                color.a *= alpha;
                GUI.color = color;
                DrawCardBackground(x, y, style.Width, height);

                color = style.Accent(item.Type);
                color.a *= alpha;
                GUI.color = color;
                GUI.DrawTexture(
                    new Rect(x, y + CornerRadius, BarWidth, height - CornerRadius * 2f),
                    Texture2D.whiteTexture
                );

                GUI.color = new Color(1f, 1f, 1f, 1f);
                GUI.contentColor = new Color(1f, 1f, 1f, 1f);
                _titleStyle!.normal.textColor = WithAlpha(style.TitleColor, alpha);
                GUI.Label(
                    new Rect(contentX, y + PaddingTop, textWidth, TitleHeight),
                    item.Title,
                    _titleStyle
                );

                _textStyle!.normal.textColor = WithAlpha(style.TextColor, alpha);
                GUI.Label(
                    new Rect(
                        contentX,
                        y + MessageTop,
                        textWidth,
                        height - MessageTop - PaddingBottom
                    ),
                    item.Message,
                    _textStyle
                );
            }
            finally
            {
                GUI.color = previousColor;
                GUI.contentColor = previousContentColor;
            }
        }

        private void EnsureStyles(ToastStyle style)
        {
            if (_styleVersion == style.Version)
                return;

            _titleStyle = new GUIStyle
            {
                fontStyle = FontStyle.Bold,
                fontSize = style.TitleSize,
                alignment = TextAnchor.UpperLeft,
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
            _textStyle = new GUIStyle
            {
                fontSize = style.TextSize,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                clipping = TextClipping.Clip,
            };
            _titleStyle.normal.textColor = style.TitleColor;
            _textStyle.normal.textColor = style.TextColor;
            _styleVersion = style.Version;
        }

        private static Color WithAlpha(Color color, float alpha) =>
            new(color.r, color.g, color.b, alpha);

        private static void DrawCardBackground(float x, float y, float width, float height)
        {
            float radius = Math.Min(CornerRadius, Math.Min(width, height) * 0.5f);
            float band = radius * 0.25f;
            Texture2D texture = Texture2D.whiteTexture;

            DrawHorizontalBand(x, y, width, texture, 0f, band, radius * 0.75f);
            DrawHorizontalBand(x, y, width, texture, band, band, radius * 0.375f);
            DrawHorizontalBand(x, y, width, texture, band * 2f, band, radius * 0.125f);
            GUI.DrawTexture(new Rect(x, y + band * 3f, width, height - band * 6f), texture);
            DrawHorizontalBand(x, y, width, texture, height - band * 3f, band, radius * 0.125f);
            DrawHorizontalBand(x, y, width, texture, height - band * 2f, band, radius * 0.375f);
            DrawHorizontalBand(x, y, width, texture, height - band, band, radius * 0.75f);
        }

        private static void DrawHorizontalBand(
            float x,
            float y,
            float width,
            Texture2D texture,
            float yOffset,
            float height,
            float inset
        ) => GUI.DrawTexture(new Rect(x + inset, y + yOffset, width - inset * 2f, height), texture);

        private void TryRenderCompatibility(IReadOnlyList<ToastData> active, ToastStyle style)
        {
            try
            {
                RenderCompatibility(active, style);
            }
            catch (Exception exception)
            {
                LastError = exception;
                _disabled = true;
            }
        }

        private void RenderCompatibility(IReadOnlyList<ToastData> active, ToastStyle style)
        {
            EnsureStyles(style);
            int maxCharacters = Math.Max(8, (int)((style.Width - 24f) / 8f));
            float totalHeight = 0f;

            for (int i = 0; i < active.Count; i++)
            {
                ToastData item = active[i];
                GetWrappedMessage(item, maxCharacters, out int lineCount);
                float height = Math.Max(28f + lineCount * 18f, style.MaxHeight);
                _heightCache[i] = height;
                totalHeight += height;
            }

            totalHeight += style.Gap * Math.Max(0, active.Count - 1);
            float x = CalculateX(style);
            float y = style.Anchor switch
            {
                Anchor.BottomLeft or Anchor.BottomRight or Anchor.BottomCenter => Screen.height
                    - style.Margin
                    - totalHeight,
                _ => style.Margin,
            };

            for (int i = 0; i < active.Count; i++)
            {
                ToastData item = active[i];
                float alpha = item.Alpha;
                Color previousColor = GUI.color;
                Color previousContentColor = GUI.contentColor;
                float height = _heightCache[i];

                try
                {
                    Color background = style.BgColor;
                    background.a *= alpha;
                    GUI.color = background;
                    DrawCardBackground(x, y, style.Width, height);

                    Color accent = style.Accent(item.Type);
                    accent.a *= alpha;
                    GUI.color = accent;
                    GUI.DrawTexture(
                        new Rect(x, y + CornerRadius, BarWidth, height - CornerRadius * 2f),
                        Texture2D.whiteTexture
                    );

                    GUI.color = new Color(1f, 1f, 1f, 1f);
                    GUI.contentColor = new Color(1f, 1f, 1f, 1f);
                    _titleStyle!.normal.textColor = WithAlpha(style.TitleColor, alpha);
                    GUI.Label(
                        new Rect(
                            x + BarWidth + 12f,
                            y + PaddingTop,
                            style.Width - 30f,
                            TitleHeight
                        ),
                        item.Title,
                        _titleStyle
                    );

                    _textStyle!.normal.textColor = WithAlpha(style.TextColor, alpha);
                    GUI.Label(
                        new Rect(
                            x + BarWidth + 12f,
                            y + MessageTop,
                            style.Width - 30f,
                            height - MessageTop - PaddingBottom
                        ),
                        item.CompatibilityMessage ?? item.Message,
                        _textStyle
                    );
                }
                finally
                {
                    GUI.color = previousColor;
                    GUI.contentColor = previousContentColor;
                }

                y += height + style.Gap;
            }
        }

        private static void GetWrappedMessage(ToastData item, int maxCharacters, out int lineCount)
        {
            if (item.CompatibilityMessage != null && item.CompatibilityCharacters == maxCharacters)
            {
                lineCount = item.CompatibilityLineCount;
                return;
            }

            string value = item.Message;
            var result = new StringBuilder(value.Length + value.Length / maxCharacters);
            int column = 0;
            lineCount = 1;

            foreach (char character in value)
            {
                if (character == '\n')
                {
                    result.Append(character);
                    column = 0;
                    lineCount++;
                    continue;
                }

                if (column >= maxCharacters)
                {
                    result.Append('\n');
                    column = 0;
                    lineCount++;
                }

                result.Append(character);
                column++;
            }

            item.CompatibilityMessage = result.ToString();
            item.CompatibilityCharacters = maxCharacters;
            item.CompatibilityLineCount = lineCount;
        }
    }
}
