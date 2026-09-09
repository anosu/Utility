using System;
using System.Globalization;

namespace Utility.Notifications.Internal
{
    internal static class ToastTextMetrics
    {
        internal static string FitTitle(
            string title,
            float availableWidth,
            Func<string, float> measure
        )
        {
            string text = title.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
            if (availableWidth <= 0f)
                return string.Empty;
            if (measure(text) <= availableWidth)
                return text;

            const string Ellipsis = "…";
            if (measure(Ellipsis) > availableWidth)
                return string.Empty;

            int[] boundaries = StringInfo.ParseCombiningCharacters(text);
            int lower = 0;
            int upper = boundaries.Length;
            while (lower < upper)
            {
                int count = (lower + upper + 1) / 2;
                int end = count == boundaries.Length ? text.Length : boundaries[count];
                if (measure(text.Substring(0, end) + Ellipsis) <= availableWidth)
                    lower = count;
                else
                    upper = count - 1;
            }
            int length = lower == boundaries.Length ? text.Length : boundaries[lower];
            return text.Substring(0, length) + Ellipsis;
        }

        internal static float EstimateWidth(string text, int fontSize)
        {
            float width = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                char character = text[i];
                if (
                    char.IsHighSurrogate(character)
                    && i + 1 < text.Length
                    && char.IsLowSurrogate(text[i + 1])
                )
                {
                    width += fontSize;
                    i++;
                }
                else if (
                    CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark
                )
                {
                    width +=
                        IsWide(character) || character is 'W' or 'M' or '…'
                            ? fontSize
                            : fontSize * 0.55f;
                }
            }
            return width;
        }

        internal static int EstimateLineCount(string text, float availableWidth, int fontSize)
        {
            float widthLimit = Math.Max(1f, availableWidth);
            float narrowWidth = Math.Max(1f, fontSize * 0.55f);
            float wideWidth = Math.Max(1f, fontSize);
            float lineWidth = 0f;
            int lineCount = 1;

            for (int i = 0; i < text.Length; i++)
            {
                char character = text[i];
                if (character == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                        continue;

                    lineCount++;
                    lineWidth = 0f;
                    continue;
                }

                if (character == '\n')
                {
                    lineCount++;
                    lineWidth = 0f;
                    continue;
                }

                bool surrogatePair =
                    char.IsHighSurrogate(character)
                    && i + 1 < text.Length
                    && char.IsLowSurrogate(text[i + 1]);
                float characterWidth =
                    surrogatePair || IsWide(character) ? wideWidth
                    : char.IsWhiteSpace(character) ? narrowWidth * 0.65f
                    : narrowWidth;

                if (lineWidth > 0f && lineWidth + characterWidth > widthLimit)
                {
                    lineCount++;
                    lineWidth = 0f;
                }

                lineWidth += characterWidth;
                if (surrogatePair)
                    i++;
            }

            return lineCount;
        }

        internal static int ColumnWidth(char character) => IsWide(character) ? 2 : 1;

        private static bool IsWide(char character) =>
            character >= '\u1100'
            && (
                character <= '\u115f'
                || character == '\u2329'
                || character == '\u232a'
                || (character >= '\u2e80' && character <= '\ua4cf')
                || (character >= '\uac00' && character <= '\ud7a3')
                || (character >= '\uf900' && character <= '\ufaff')
                || (character >= '\ufe10' && character <= '\ufe19')
                || (character >= '\ufe30' && character <= '\ufe6f')
                || (character >= '\uff00' && character <= '\uff60')
                || (character >= '\uffe0' && character <= '\uffe6')
            );
    }
}
