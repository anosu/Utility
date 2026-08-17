using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Utility.Diagnostics;

namespace Utility.Notifications.Internal
{
    internal enum ImguiBackgroundBackend
    {
        Box,
        DrawTexture,
        None,
    }

    internal sealed class ImguiToastRenderer : IDisposable
    {
        private readonly GUIContent _content = new();
        private readonly float[] _heightCache = new float[ToastRuntime.Capacity];

        private bool _compatibilityMode;
        private bool _disabled;
        private bool _bareLabelMode;
        private bool _compatibilityFontSizeUnavailable;
        private bool _compatibilityUsesConfiguredFontSize;
        private GUIStyle? _boxStyle;
        private GUIStyle? _compatibilityTextStyle;
        private GUIStyle? _compatibilityTitleStyle;
        private GUIStyle? _textStyle;
        private GUIStyle? _titleStyle;
        private int _styleVersion = -1;
        private int _compatibilityTextSize = -1;
        private int _compatibilityTitleSize = -1;
        private int _textSize = -1;
        private int _titleSize = -1;
        private Exception? _backgroundError;
        private ImguiBackgroundBackend _backgroundBackend = ImguiBackgroundBackend.Box;

        internal Exception? LastError { get; private set; }

        internal bool IsDisabled => _disabled;

        internal void Render(IReadOnlyList<ToastItem> active, ToastTheme style, ToastLayout layout)
        {
            if (_disabled || active.Count == 0)
                return;

            if (_bareLabelMode)
            {
                TryRenderBareLabels(active, style, layout);
                return;
            }

            if (_compatibilityMode)
            {
                TryRenderCompatibility(active, style, layout);
                return;
            }

            try
            {
                RenderStyled(active, style, layout);
                LastError = _backgroundError;
            }
            catch (Exception exception)
            {
                LastError = exception;
                _compatibilityMode = true;
                Logging.WriteRecoverable(
                    LogLevel.Warning,
                    "Toast",
                    "Styled IMGUI rendering failed; switched to compatibility rendering.",
                    exception
                );
                TryRenderCompatibility(active, style, layout);
            }
        }

        internal void Disable(Exception exception)
        {
            LastError = exception;
            _disabled = true;
        }

        public void Dispose()
        {
            _boxStyle = null;
            _compatibilityTextStyle = null;
            _compatibilityTitleStyle = null;
            _compatibilityUsesConfiguredFontSize = false;
            _textStyle = null;
            _titleStyle = null;
        }

        private void RenderStyled(
            IReadOnlyList<ToastItem> active,
            ToastTheme style,
            ToastLayout layout
        )
        {
            int count = active.Count;
            EnsureStyles(style, layout);

            float textWidth = ToastMetrics.ContentWidth(layout.Width);
            float titleHeight = ToastMetrics.CalculateTitleHeight(layout);
            float messageTop = ToastMetrics.CalculateMessageTop(layout);
            float totalHeight = 0f;
            for (int i = 0; i < count; i++)
            {
                _content.text = active[i].Message;
                float contentHeight =
                    messageTop
                    + _textStyle!.CalcHeight(_content, textWidth)
                    + ToastMetrics.BottomPadding;
                _heightCache[i] = Mathf.Max(contentHeight, layout.MinimumHeight);
            }

            layout.FitCardHeights(_heightCache, count);
            for (int i = 0; i < count; i++)
                totalHeight += _heightCache[i];

            totalHeight += layout.Gap * Math.Max(0, count - 1);
            float x = layout.CalculateImguiX(style.Anchor);
            float y = layout.CalculateImguiY(style.Anchor, totalHeight);

            for (int i = 0; i < count; i++)
            {
                ToastItem item = active[i];
                float height = _heightCache[i];
                if (item.Alpha > 0f)
                    DrawCard(
                        item,
                        style,
                        x,
                        y,
                        height,
                        textWidth,
                        layout.Width,
                        titleHeight,
                        messageTop
                    );

                y += height + layout.Gap;
            }
        }

        private void DrawCard(
            ToastItem item,
            ToastTheme style,
            float x,
            float y,
            float height,
            float textWidth,
            float width,
            float titleHeight,
            float messageTop
        )
        {
            float alpha = item.Alpha;
            float contentX = x + ToastMetrics.ContentLeft;
            Color previousColor = GUI.color;
            Color previousContentColor = GUI.contentColor;

            try
            {
                DrawCardSurface(item, style, x, y, width, height, alpha);

                GUI.color = new Color(1f, 1f, 1f, 1f);
                GUI.contentColor = new Color(1f, 1f, 1f, 1f);
                _titleStyle!.normal.textColor = WithAlpha(style.TitleColor, alpha);
                GUI.Label(
                    new Rect(contentX, y + ToastMetrics.TopPadding, textWidth, titleHeight),
                    item.Title,
                    _titleStyle
                );

                _textStyle!.normal.textColor = WithAlpha(style.TextColor, alpha);
                GUI.Label(
                    new Rect(
                        contentX,
                        y + messageTop,
                        textWidth,
                        ToastMetrics.CalculateMessageHeight(height, messageTop)
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

        private void EnsureStyles(ToastTheme style, ToastLayout layout)
        {
            if (
                _styleVersion == style.Version
                && _titleSize == layout.TitleSize
                && _textSize == layout.TextSize
            )
                return;

            _titleStyle = new GUIStyle
            {
                fontStyle = FontStyle.Bold,
                fontSize = layout.TitleSize,
                alignment = TextAnchor.UpperLeft,
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
            _textStyle = new GUIStyle
            {
                fontSize = layout.TextSize,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                clipping = TextClipping.Clip,
            };
            _titleStyle.normal.textColor = style.TitleColor;
            _textStyle.normal.textColor = style.TextColor;
            _styleVersion = style.Version;
            _titleSize = layout.TitleSize;
            _textSize = layout.TextSize;
        }

        private static Color WithAlpha(Color color, float alpha) =>
            new(color.r, color.g, color.b, alpha);

        private void DrawCardSurface(
            ToastItem item,
            ToastTheme style,
            float x,
            float y,
            float width,
            float height,
            float alpha
        )
        {
            while (true)
            {
                try
                {
                    switch (_backgroundBackend)
                    {
                        case ImguiBackgroundBackend.Box:
                            DrawBoxSurface(item, style, x, y, width, height, alpha);
                            return;
                        case ImguiBackgroundBackend.DrawTexture:
                            DrawTextureSurface(item, style, x, y, width, height, alpha);
                            return;
                        case ImguiBackgroundBackend.None:
                            return;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                catch (Exception exception)
                {
                    _backgroundError = CombineBackgroundErrors(_backgroundError, exception);
                    if (_backgroundBackend == ImguiBackgroundBackend.Box)
                    {
                        _backgroundBackend = ImguiBackgroundBackend.DrawTexture;
                        _boxStyle = null;
                        Logging.WriteRecoverable(
                            LogLevel.Warning,
                            "Toast",
                            "GUI.Box background rendering failed; trying GUI.DrawTexture.",
                            exception
                        );
                        continue;
                    }

                    _backgroundBackend = ImguiBackgroundBackend.None;
                    Logging.WriteRecoverable(
                        LogLevel.Warning,
                        "Toast",
                        "GUI.DrawTexture background rendering failed; using labels without a background.",
                        exception
                    );
                    return;
                }
            }
        }

        private void DrawBoxSurface(
            ToastItem item,
            ToastTheme style,
            float x,
            float y,
            float width,
            float height,
            float alpha
        )
        {
            EnsureBoxStyle();

            GUI.color = WithAlpha(style.BackgroundColor, style.BackgroundColor.a * alpha);
            DrawRoundedBox(x, y, width, height, _boxStyle!);

            GUI.color = WithAlpha(style.Accent(item.Kind), style.Accent(item.Kind).a * alpha);
            float inset = Math.Min(ToastMetrics.CornerRadius, height * 0.5f);
            float accentHeight = Math.Max(0f, height - inset * 2f);
            if (accentHeight > 0f)
            {
                GUI.Box(
                    new Rect(x, y + inset, ToastMetrics.AccentWidth, accentHeight),
                    string.Empty,
                    _boxStyle
                );
            }
        }

        private void DrawTextureSurface(
            ToastItem item,
            ToastTheme style,
            float x,
            float y,
            float width,
            float height,
            float alpha
        )
        {
            GUI.color = WithAlpha(style.BackgroundColor, style.BackgroundColor.a * alpha);
            DrawRoundedTexture(x, y, width, height);

            Color accent = style.Accent(item.Kind);
            GUI.color = WithAlpha(accent, accent.a * alpha);
            float inset = Math.Min(ToastMetrics.CornerRadius, height * 0.5f);
            GUI.DrawTexture(
                new Rect(x, y + inset, ToastMetrics.AccentWidth, Math.Max(0f, height - inset * 2f)),
                Texture2D.whiteTexture
            );
        }

        private void EnsureBoxStyle()
        {
            if (_boxStyle != null)
                return;

            _boxStyle = new GUIStyle();
            _boxStyle.normal.background = Texture2D.whiteTexture;
        }

        private static void DrawRoundedBox(
            float x,
            float y,
            float width,
            float height,
            GUIStyle style
        )
        {
            float radius = Math.Min(ToastMetrics.CornerRadius, Math.Min(width, height) * 0.5f);
            float band = radius * 0.25f;

            DrawHorizontalBoxBand(x, y, width, style, 0f, band, radius * 0.75f);
            DrawHorizontalBoxBand(x, y, width, style, band, band, radius * 0.375f);
            DrawHorizontalBoxBand(x, y, width, style, band * 2f, band, radius * 0.125f);
            GUI.Box(new Rect(x, y + band * 3f, width, height - band * 6f), string.Empty, style);
            DrawHorizontalBoxBand(x, y, width, style, height - band * 3f, band, radius * 0.125f);
            DrawHorizontalBoxBand(x, y, width, style, height - band * 2f, band, radius * 0.375f);
            DrawHorizontalBoxBand(x, y, width, style, height - band, band, radius * 0.75f);
        }

        private static void DrawHorizontalBoxBand(
            float x,
            float y,
            float width,
            GUIStyle style,
            float yOffset,
            float height,
            float inset
        ) =>
            GUI.Box(
                new Rect(x + inset, y + yOffset, width - inset * 2f, height),
                string.Empty,
                style
            );

        private static void DrawRoundedTexture(float x, float y, float width, float height)
        {
            float radius = Math.Min(ToastMetrics.CornerRadius, Math.Min(width, height) * 0.5f);
            float band = radius * 0.25f;
            Texture2D texture = Texture2D.whiteTexture;

            DrawHorizontalTextureBand(x, y, width, texture, 0f, band, radius * 0.75f);
            DrawHorizontalTextureBand(x, y, width, texture, band, band, radius * 0.375f);
            DrawHorizontalTextureBand(x, y, width, texture, band * 2f, band, radius * 0.125f);
            GUI.DrawTexture(new Rect(x, y + band * 3f, width, height - band * 6f), texture);
            DrawHorizontalTextureBand(
                x,
                y,
                width,
                texture,
                height - band * 3f,
                band,
                radius * 0.125f
            );
            DrawHorizontalTextureBand(
                x,
                y,
                width,
                texture,
                height - band * 2f,
                band,
                radius * 0.375f
            );
            DrawHorizontalTextureBand(x, y, width, texture, height - band, band, radius * 0.75f);
        }

        private static void DrawHorizontalTextureBand(
            float x,
            float y,
            float width,
            Texture2D texture,
            float yOffset,
            float height,
            float inset
        ) => GUI.DrawTexture(new Rect(x + inset, y + yOffset, width - inset * 2f, height), texture);

        private static Exception CombineBackgroundErrors(Exception? first, Exception second) =>
            first == null
                ? second
                : new AggregateException("Every IMGUI background renderer failed.", first, second);

        private void TryRenderCompatibility(
            IReadOnlyList<ToastItem> active,
            ToastTheme style,
            ToastLayout layout
        )
        {
            try
            {
                RenderCompatibility(active, style, layout);
            }
            catch (Exception exception)
            {
                LastError = exception;
                _bareLabelMode = true;
                Logging.WriteRecoverable(
                    LogLevel.Warning,
                    "Toast",
                    "Compatibility IMGUI typography failed; switched to bare labels.",
                    exception
                );
                TryRenderBareLabels(active, style, layout);
            }
        }

        private void RenderCompatibility(
            IReadOnlyList<ToastItem> active,
            ToastTheme style,
            ToastLayout layout
        )
        {
            bool usesConfiguredFontSize = EnsureCompatibilityStyles(style, layout);
            float contentWidth = ToastMetrics.ContentWidth(layout.Width);
            float titleHeight = usesConfiguredFontSize
                ? ToastMetrics.CalculateTitleHeight(layout)
                : ToastMetrics.CompatibilityTitleHeight;
            float messageTop = usesConfiguredFontSize
                ? ToastMetrics.CalculateMessageTop(layout)
                : ToastMetrics.CompatibilityMessageTop;
            float characterWidth = usesConfiguredFontSize
                ? Math.Max(1f, layout.TextSize * 0.55f)
                : 8f;
            float lineHeight = usesConfiguredFontSize ? layout.TextSize + 4f : 18f;
            int maxColumns = Math.Max(8, (int)(contentWidth / characterWidth));
            float totalHeight = 0f;

            for (int i = 0; i < active.Count; i++)
            {
                ToastItem item = active[i];
                GetWrappedMessage(item, maxColumns, out int lineCount);
                _heightCache[i] = Math.Max(
                    messageTop + lineCount * lineHeight + ToastMetrics.BottomPadding,
                    layout.MinimumHeight
                );
            }

            layout.FitCardHeights(_heightCache, active.Count);
            for (int i = 0; i < active.Count; i++)
                totalHeight += _heightCache[i];

            totalHeight += layout.Gap * Math.Max(0, active.Count - 1);
            float x = layout.CalculateImguiX(style.Anchor);
            float y = layout.CalculateImguiY(style.Anchor, totalHeight);

            for (int i = 0; i < active.Count; i++)
            {
                ToastItem item = active[i];
                float alpha = item.Alpha;
                Color previousColor = GUI.color;
                Color previousContentColor = GUI.contentColor;
                float height = _heightCache[i];

                try
                {
                    DrawCardSurface(item, style, x, y, layout.Width, height, alpha);

                    GUI.color = new Color(1f, 1f, 1f, 1f);
                    GUI.contentColor = new Color(1f, 1f, 1f, 1f);
                    _compatibilityTitleStyle!.normal.textColor = WithAlpha(style.TitleColor, alpha);
                    GUI.Label(
                        new Rect(
                            x + ToastMetrics.ContentLeft,
                            y + ToastMetrics.TopPadding,
                            contentWidth,
                            titleHeight
                        ),
                        item.Title,
                        _compatibilityTitleStyle
                    );

                    _compatibilityTextStyle!.normal.textColor = WithAlpha(style.TextColor, alpha);
                    GUI.Label(
                        new Rect(
                            x + ToastMetrics.ContentLeft,
                            y + messageTop,
                            contentWidth,
                            ToastMetrics.CalculateMessageHeight(height, messageTop)
                        ),
                        item.CompatibilityMessage ?? item.Message,
                        _compatibilityTextStyle
                    );
                }
                finally
                {
                    GUI.color = previousColor;
                    GUI.contentColor = previousContentColor;
                }

                y += height + layout.Gap;
            }
        }

        private void TryRenderBareLabels(
            IReadOnlyList<ToastItem> active,
            ToastTheme style,
            ToastLayout layout
        )
        {
            try
            {
                RenderBareLabels(active, style, layout);
            }
            catch (Exception exception)
            {
                LastError = exception;
                _disabled = true;
            }
        }

        private void RenderBareLabels(
            IReadOnlyList<ToastItem> active,
            ToastTheme style,
            ToastLayout layout
        )
        {
            float contentWidth = ToastMetrics.ContentWidth(layout.Width);
            int maxColumns = Math.Max(8, (int)(contentWidth / 8f));
            float totalHeight = 0f;
            for (int i = 0; i < active.Count; i++)
            {
                GetWrappedMessage(active[i], maxColumns, out int lineCount);
                _heightCache[i] = Math.Max(
                    ToastMetrics.CompatibilityMessageTop
                        + lineCount * 18f
                        + ToastMetrics.BottomPadding,
                    layout.MinimumHeight
                );
            }

            layout.FitCardHeights(_heightCache, active.Count);
            for (int i = 0; i < active.Count; i++)
                totalHeight += _heightCache[i];

            totalHeight += layout.Gap * Math.Max(0, active.Count - 1);
            float x = layout.CalculateImguiX(style.Anchor);
            float y = layout.CalculateImguiY(style.Anchor, totalHeight);

            for (int i = 0; i < active.Count; i++)
            {
                ToastItem item = active[i];
                float height = _heightCache[i];
                float alpha = item.Alpha;
                Color previousColor = GUI.color;
                Color previousContentColor = GUI.contentColor;

                try
                {
                    DrawCardSurface(item, style, x, y, layout.Width, height, alpha);
                    GUI.color = new Color(1f, 1f, 1f, 1f);

                    GUI.contentColor = WithAlpha(style.TitleColor, alpha);
                    GUI.Label(
                        new Rect(
                            x + ToastMetrics.ContentLeft,
                            y + ToastMetrics.TopPadding,
                            contentWidth,
                            ToastMetrics.CompatibilityTitleHeight
                        ),
                        item.Title
                    );

                    GUI.contentColor = WithAlpha(style.TextColor, alpha);
                    GUI.Label(
                        new Rect(
                            x + ToastMetrics.ContentLeft,
                            y + ToastMetrics.CompatibilityMessageTop,
                            contentWidth,
                            ToastMetrics.CalculateMessageHeight(
                                height,
                                ToastMetrics.CompatibilityMessageTop
                            )
                        ),
                        item.CompatibilityMessage ?? item.Message
                    );
                }
                finally
                {
                    GUI.color = previousColor;
                    GUI.contentColor = previousContentColor;
                }

                y += height + layout.Gap;
            }
        }

        private static void GetWrappedMessage(ToastItem item, int maxColumns, out int lineCount)
        {
            if (item.CompatibilityMessage != null && item.CompatibilityCharacters == maxColumns)
            {
                lineCount = item.CompatibilityLineCount;
                return;
            }

            string value = item.Message;
            var result = new StringBuilder(value.Length + value.Length / maxColumns);
            int column = 0;
            lineCount = 1;

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (character == '\r')
                    continue;

                if (character == '\n')
                {
                    result.Append(character);
                    column = 0;
                    lineCount++;
                    continue;
                }

                bool surrogatePair =
                    char.IsHighSurrogate(character)
                    && i + 1 < value.Length
                    && char.IsLowSurrogate(value[i + 1]);
                int characterWidth = surrogatePair ? 2 : ToastTextMetrics.ColumnWidth(character);
                if (column > 0 && column + characterWidth > maxColumns)
                {
                    result.Append('\n');
                    column = 0;
                    lineCount++;
                }

                result.Append(character);
                if (surrogatePair)
                    result.Append(value[++i]);
                column += characterWidth;
            }

            item.CompatibilityMessage = result.ToString();
            item.CompatibilityCharacters = maxColumns;
            item.CompatibilityLineCount = lineCount;
        }

        private bool EnsureCompatibilityStyles(ToastTheme style, ToastLayout layout)
        {
            if (_compatibilityTitleStyle == null || _compatibilityTextStyle == null)
                CreateCompatibilityStyles(style);

            if (_compatibilityFontSizeUnavailable)
                return false;
            if (
                _compatibilityUsesConfiguredFontSize
                && _compatibilityTitleSize == layout.TitleSize
                && _compatibilityTextSize == layout.TextSize
            )
            {
                return true;
            }

            try
            {
                _compatibilityTitleStyle!.fontSize = layout.TitleSize;
                _compatibilityTextStyle!.fontSize = layout.TextSize;
                _compatibilityTitleSize = layout.TitleSize;
                _compatibilityTextSize = layout.TextSize;
                _compatibilityUsesConfiguredFontSize = true;
                return true;
            }
            catch (Exception exception)
            {
                _compatibilityFontSizeUnavailable = true;
                _compatibilityUsesConfiguredFontSize = false;
                _compatibilityTitleSize = -1;
                _compatibilityTextSize = -1;
                CreateCompatibilityStyles(style);
                Logging.WriteRecoverable(
                    LogLevel.Debug,
                    "Toast",
                    "Compatibility IMGUI font sizing is unavailable; using Unity's default font size and compact spacing.",
                    exception
                );
                return false;
            }
        }

        private void CreateCompatibilityStyles(ToastTheme style)
        {
            _compatibilityTitleStyle = new GUIStyle();
            _compatibilityTextStyle = new GUIStyle();
            _compatibilityTitleStyle.normal.textColor = style.TitleColor;
            _compatibilityTextStyle.normal.textColor = style.TextColor;
        }
    }
}
