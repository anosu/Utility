using System;
using System.Collections.Generic;

namespace Utility.Notifications.Internal
{
    internal interface IFrameToastRenderer : IDisposable
    {
        void RenderFrame(IReadOnlyList<ToastItem> active, ToastTheme style);
    }
}
