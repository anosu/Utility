using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using Utility.Diagnostics;

namespace Utility.Notifications.Internal
{
    internal enum ImguiBackgroundBackend
    {
        StyledBox,
        DrawTexture,
        None,
    }

    internal sealed class ImguiToastRenderer : IDisposable
    {
        private readonly GUIContent _content = new();
        private readonly float[] _heightCache = new float[ToastRuntime.Capacity];

        private bool _compatibilityMode;
        private bool _disabled;
        private bool _titleMeasurementUnavailable;
        private bool _bodyMeasurementUnavailable;
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
        private ImguiBackgroundBackend _backgroundBackend = ImguiBackgroundBackend.StyledBox;

        internal Exception? LastError { get; private set; }

        internal bool IsDisabled => _disabled;

        internal void Render(IReadOnlyList<ToastItem> active, ToastTheme style, ToastLayout layout)
        {
            if (_disabled || active.Count == 0)
                return;

            using var guiState = new ToastGuiState();
            RenderContents(active, style, layout);
        }

        private void RenderContents(
            IReadOnlyList<ToastItem> active,
            ToastTheme style,
            ToastLayout layout
        )
        {
            if (_disabled || active.Count == 0)
                return;

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

            float textWidth = ToastMetrics.ContentWidth(layout);
            float titleHeight = ToastMetrics.CalculateTitleHeight(layout);
            float messageTop = ToastMetrics.CalculateMessageTop(layout);
            float totalHeight = 0f;
            for (int i = 0; i < count; i++)
            {
                float contentHeight =
                    messageTop
                    + MeasureBodyHeight(_textStyle!, active[i].Message, textWidth, layout.TextSize)
                    + layout.VerticalInset;
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
                        layout,
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
            ToastLayout layout,
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
            float contentX = x + layout.ContentInset;
            Color previousColor = GUI.color;
            Color previousContentColor = GUI.contentColor;

            try
            {
                DrawCardSurface(item, style, layout, x, y, width, height, alpha);

                GUI.color = new Color(1f, 1f, 1f, 1f);
                GUI.contentColor = new Color(1f, 1f, 1f, 1f);
                _titleStyle!.normal.textColor = WithAlpha(
                    style.TitleColor,
                    style.TitleColor.a * alpha
                );
                GUI.Label(
                    new Rect(contentX, y + layout.VerticalInset, textWidth, titleHeight),
                    item.GetDisplayTitle(
                        textWidth,
                        layout.TitleSize,
                        this,
                        value => MeasureTitleWidth(_titleStyle, value, layout.TitleSize)
                    ),
                    _titleStyle
                );

                _textStyle!.normal.textColor = WithAlpha(
                    style.TextColor,
                    style.TextColor.a * alpha
                );
                GUI.Label(
                    new Rect(
                        contentX,
                        y + messageTop,
                        textWidth,
                        ToastMetrics.CalculateMessageHeight(height, messageTop, layout)
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
                font = ToastFont.Get(),
                fontStyle = FontStyle.Bold,
                fontSize = layout.TitleSize,
                alignment = TextAnchor.UpperLeft,
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
            _textStyle = new GUIStyle
            {
                font = ToastFont.Get(),
                fontStyle = FontStyle.Normal,
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
            ToastLayout layout,
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
                        case ImguiBackgroundBackend.StyledBox:
                            DrawBoxSurface(item, style, layout, x, y, width, height, alpha);
                            return;
                        case ImguiBackgroundBackend.DrawTexture:
                            DrawTextureSurface(item, style, layout, x, y, width, height, alpha);
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
                    if (_backgroundBackend == ImguiBackgroundBackend.StyledBox)
                    {
                        _backgroundBackend = ImguiBackgroundBackend.DrawTexture;
                        _boxStyle = null;
                        Logging.WriteRecoverable(
                            LogLevel.Warning,
                            "Toast",
                            "Styled GUI.Box background rendering failed; trying GUI.DrawTexture.",
                            exception
                        );
                        continue;
                    }

                    _backgroundBackend = ImguiBackgroundBackend.None;
                    _disabled = true;
                    Logging.WriteRecoverable(
                        LogLevel.Warning,
                        "Toast",
                        "GUI.DrawTexture background rendering failed; disabling IMGUI.",
                        exception
                    );
                    return;
                }
            }
        }

        private void DrawBoxSurface(
            ToastItem item,
            ToastTheme style,
            ToastLayout layout,
            float x,
            float y,
            float width,
            float height,
            float alpha
        )
        {
            EnsureBoxStyle();

            GUI.color = WithAlpha(style.BackgroundColor, style.BackgroundColor.a * alpha);
            DrawRoundedBox(x, y, width, height, _boxStyle!, layout.SpacingScale);

            GUI.color = WithAlpha(style.Accent(item.Kind), style.Accent(item.Kind).a * alpha);
            float inset = Math.Min(layout.CornerRadius, height * 0.5f);
            float accentHeight = Math.Max(0f, height - inset * 2f);
            if (accentHeight > 0f)
            {
                GUI.Box(
                    new Rect(x, y + inset, Math.Min(layout.AccentWidth, width), accentHeight),
                    string.Empty,
                    _boxStyle
                );
            }
        }

        private void DrawTextureSurface(
            ToastItem item,
            ToastTheme style,
            ToastLayout layout,
            float x,
            float y,
            float width,
            float height,
            float alpha
        )
        {
            GUI.color = WithAlpha(style.BackgroundColor, style.BackgroundColor.a * alpha);
            DrawRoundedTexture(x, y, width, height, layout.SpacingScale);

            Color accent = style.Accent(item.Kind);
            GUI.color = WithAlpha(accent, accent.a * alpha);
            float inset = Math.Min(layout.CornerRadius, height * 0.5f);
            GUI.DrawTexture(
                new Rect(
                    x,
                    y + inset,
                    Math.Min(layout.AccentWidth, width),
                    Math.Max(0f, height - inset * 2f)
                ),
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
            GUIStyle style,
            float spacingScale
        )
        {
            for (int i = 0; i < ToastMetrics.RoundedBandCount; i++)
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
                GUI.Box(
                    new Rect(x + inset, y + top, width - inset * 2f, bandHeight),
                    string.Empty,
                    style
                );
            }
        }

        private static void DrawRoundedTexture(
            float x,
            float y,
            float width,
            float height,
            float spacingScale
        )
        {
            Texture2D texture = Texture2D.whiteTexture;
            for (int i = 0; i < ToastMetrics.RoundedBandCount; i++)
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
                GUI.DrawTexture(
                    new Rect(x + inset, y + top, width - inset * 2f, bandHeight),
                    texture
                );
            }
        }

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
                _disabled = true;
                Logging.WriteRecoverable(
                    LogLevel.Warning,
                    "Toast",
                    "Compatibility IMGUI typography failed; requesting the uGUI renderer.",
                    exception
                );
            }
        }

        private void RenderCompatibility(
            IReadOnlyList<ToastItem> active,
            ToastTheme style,
            ToastLayout layout
        )
        {
            EnsureCompatibilityStyles(style, layout);
            float contentWidth = ToastMetrics.ContentWidth(layout);
            float titleHeight = ToastMetrics.CalculateTitleHeight(layout);
            float messageTop = ToastMetrics.CalculateMessageTop(layout);
            float characterWidth = Math.Max(1f, layout.TextSize * 0.55f);
            int maxColumns = Math.Max(8, (int)(contentWidth / characterWidth));
            float totalHeight = 0f;

            for (int i = 0; i < active.Count; i++)
            {
                ToastItem item = active[i];
                GetWrappedMessage(item, maxColumns);
                _heightCache[i] = Math.Max(
                    messageTop
                        + MeasureBodyHeight(
                            _compatibilityTextStyle!,
                            item.CompatibilityMessage ?? item.Message,
                            contentWidth,
                            layout.TextSize
                        )
                        + layout.VerticalInset,
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
                    DrawCardSurface(item, style, layout, x, y, layout.Width, height, alpha);

                    GUI.color = new Color(1f, 1f, 1f, 1f);
                    GUI.contentColor = new Color(1f, 1f, 1f, 1f);
                    _compatibilityTitleStyle!.normal.textColor = WithAlpha(
                        style.TitleColor,
                        style.TitleColor.a * alpha
                    );
                    GUI.Label(
                        new Rect(
                            x + layout.ContentInset,
                            y + layout.VerticalInset,
                            contentWidth,
                            titleHeight
                        ),
                        item.GetDisplayTitle(
                            contentWidth,
                            layout.TitleSize,
                            _compatibilityTitleStyle,
                            value =>
                                MeasureTitleWidth(_compatibilityTitleStyle, value, layout.TitleSize)
                        ),
                        _compatibilityTitleStyle
                    );

                    _compatibilityTextStyle!.normal.textColor = WithAlpha(
                        style.TextColor,
                        style.TextColor.a * alpha
                    );
                    GUI.Label(
                        new Rect(
                            x + layout.ContentInset,
                            y + messageTop,
                            contentWidth,
                            ToastMetrics.CalculateMessageHeight(height, messageTop, layout)
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

        private static void GetWrappedMessage(ToastItem item, int maxColumns)
        {
            if (item.CompatibilityMessage != null && item.CompatibilityCharacters == maxColumns)
            {
                return;
            }

            string value = item.Message;
            var result = new StringBuilder(value.Length + value.Length / maxColumns);
            int column = 0;

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (character == '\r')
                    continue;

                if (character == '\n')
                {
                    result.Append(character);
                    column = 0;
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
                }

                result.Append(character);
                if (surrogatePair)
                    result.Append(value[++i]);
                column += characterWidth;
            }

            item.CompatibilityMessage = result.ToString();
            item.CompatibilityCharacters = maxColumns;
        }

        private float MeasureTitleWidth(GUIStyle style, string value, int fontSize)
        {
            if (!_titleMeasurementUnavailable)
            {
                try
                {
                    float width = ReadTitleWidth(style, value);
                    if (float.IsFinite(width) && (width > 0f || value.Length == 0))
                        return width;
                    throw new InvalidOperationException("IMGUI returned an invalid text width.");
                }
                catch (Exception exception)
                {
                    _titleMeasurementUnavailable = true;
                    Logging.WriteRecoverable(
                        LogLevel.Debug,
                        "Toast",
                        "IMGUI title measurement is unavailable; using estimated widths.",
                        exception
                    );
                }
            }
            return ToastTextMetrics.EstimateWidth(value, fontSize);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private float ReadTitleWidth(GUIStyle style, string value)
        {
            _content.text = value;
            return style.CalcSize(_content).x;
        }

        private float MeasureBodyHeight(GUIStyle style, string value, float width, int fontSize)
        {
            if (!_bodyMeasurementUnavailable)
            {
                try
                {
                    float height = ReadBodyHeight(style, value, width);
                    if (float.IsFinite(height) && (height > 0f || value.Length == 0))
                        return Math.Max(height, ToastMetrics.CalculateLineHeight(fontSize));
                    throw new InvalidOperationException("IMGUI returned an invalid text height.");
                }
                catch (Exception exception)
                {
                    _bodyMeasurementUnavailable = true;
                    Logging.WriteRecoverable(
                        LogLevel.Debug,
                        "Toast",
                        "IMGUI body measurement is unavailable; using estimated heights.",
                        exception
                    );
                }
            }
            return ToastTextMetrics.EstimateLineCount(value, width, fontSize)
                * ToastMetrics.CalculateLineHeight(fontSize);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private float ReadBodyHeight(GUIStyle style, string value, float width)
        {
            _content.text = value;
            return style.CalcHeight(_content, width);
        }

        private void EnsureCompatibilityStyles(ToastTheme style, ToastLayout layout)
        {
            if (_compatibilityTitleStyle == null || _compatibilityTextStyle == null)
                CreateCompatibilityStyles(style);

            if (
                _compatibilityTitleSize == layout.TitleSize
                && _compatibilityTextSize == layout.TextSize
            )
            {
                return;
            }

            _compatibilityTitleStyle!.fontSize = layout.TitleSize;
            _compatibilityTextStyle!.fontSize = layout.TextSize;
            _compatibilityTitleSize = layout.TitleSize;
            _compatibilityTextSize = layout.TextSize;
        }

        private void CreateCompatibilityStyles(ToastTheme style)
        {
            _compatibilityTitleStyle = new GUIStyle
            {
                font = ToastFont.Get(),
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
            };
            _compatibilityTextStyle = new GUIStyle
            {
                font = ToastFont.Get(),
                fontStyle = FontStyle.Normal,
                clipping = TextClipping.Clip,
            };
            _compatibilityTitleStyle.normal.textColor = style.TitleColor;
            _compatibilityTextStyle.normal.textColor = style.TextColor;
        }
    }
}
