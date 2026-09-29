using System;
using UnityEngine;
using Utility.Notifications.Internal;
using Xunit;

namespace Utility.Lifecycle.Tests
{
    public sealed class CanvasSetupTests
    {
        [Fact]
        public void MissingRenderModeSetterKeepsAnOverlayCanvasUsable()
        {
            var canvas = new Canvas { ThrowOnRenderModeSet = true };

            ToastCanvasSetup.Configure(canvas);

            Assert.Equal(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.True(canvas.overrideSorting);
            Assert.Equal(32760, canvas.sortingOrder);
        }

        [Fact]
        public void MissingRenderModeSetterRejectsANonOverlayCanvas()
        {
            var canvas = new Canvas
            {
                Mode = RenderMode.ScreenSpaceCamera,
                ThrowOnRenderModeSet = true,
            };

            Assert.Throws<MissingMethodException>(() => ToastCanvasSetup.Configure(canvas));
        }
    }
}

namespace UnityEngine
{
    public enum RenderMode
    {
        ScreenSpaceOverlay,
        ScreenSpaceCamera,
    }

    public sealed class Canvas
    {
        public bool ThrowOnRenderModeSet { get; set; }
        public RenderMode Mode { get; set; }
        public RenderMode renderMode
        {
            get => Mode;
            set
            {
                if (ThrowOnRenderModeSet)
                    throw new MissingMethodException(
                        "Canvas.set_renderMode_Injected was stripped."
                    );
                Mode = value;
            }
        }

        public bool overrideSorting { get; set; }
        public int sortingOrder { get; set; }
    }
}
