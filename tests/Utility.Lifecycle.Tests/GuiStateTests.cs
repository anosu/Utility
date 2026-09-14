using System;
using UnityEngine;
using Utility.Notifications.Internal;
using Xunit;

namespace Utility.Lifecycle.Tests
{
    public sealed class GuiStateTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RenderingUsesNeutralGlobalsAndRestoresGameState(bool throwDuringDraw)
        {
            var original = new Color(0.2f, 0.3f, 0.4f, 0.25f);
            GUI.color = original;
            GUI.contentColor = original;
            GUI.backgroundColor = original;
            GUI.enabled = false;
            GUI.matrix = new Matrix4x4 { Marker = 7 };

            Exception? error = Record.Exception(() =>
            {
                using var scope = new ToastGuiState();
                var white = new Color(1f, 1f, 1f, 1f);
                Assert.Equal(white, GUI.backgroundColor);
                Assert.Equal(white, GUI.contentColor);
                Assert.Equal(white, GUI.color);
                Assert.True(GUI.enabled);
                Assert.Equal(Matrix4x4.identity, GUI.matrix);
                GUI.color = new Color(0.06f, 0.06f, 0.08f, 0.94f * 0.5f);
                Assert.Equal(0.47f, GUI.color.a * GUI.backgroundColor.a);
                if (throwDuringDraw)
                    throw new InvalidOperationException("Simulated drawing failure.");
            });

            if (throwDuringDraw)
                Assert.IsType<InvalidOperationException>(error);
            else
                Assert.Null(error);
            Assert.Equal(original, GUI.color);
            Assert.Equal(original, GUI.contentColor);
            Assert.Equal(original, GUI.backgroundColor);
            Assert.False(GUI.enabled);
            Assert.Equal(7, GUI.matrix.Marker);
        }
    }
}

namespace UnityEngine
{
    public readonly record struct Color(float r, float g, float b, float a);

    public struct Matrix4x4
    {
        public int Marker;
        public static Matrix4x4 identity => new() { Marker = 1 };
    }

    public static class GUI
    {
        public static Color color { get; set; }
        public static Color contentColor { get; set; }
        public static Color backgroundColor { get; set; }
        public static Matrix4x4 matrix { get; set; }
        public static bool enabled { get; set; }
    }
}
