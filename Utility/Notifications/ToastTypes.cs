namespace Utility.Notifications
{
    /// <summary>Specifies the semantic kind of a toast notification.</summary>
    public enum ToastKind
    {
        /// <summary>General information.</summary>
        Info,

        /// <summary>A successful operation.</summary>
        Success,

        /// <summary>A recoverable warning.</summary>
        Warning,

        /// <summary>An error or failed operation.</summary>
        Error,
    }

    /// <summary>Specifies where toast notifications are placed on screen.</summary>
    public enum ToastAnchor
    {
        /// <summary>The upper-left corner.</summary>
        TopLeft,

        /// <summary>The upper edge, centered horizontally.</summary>
        TopCenter,

        /// <summary>The upper-right corner.</summary>
        TopRight,

        /// <summary>The lower-left corner.</summary>
        BottomLeft,

        /// <summary>The lower edge, centered horizontally.</summary>
        BottomCenter,

        /// <summary>The lower-right corner.</summary>
        BottomRight,
    }

    /// <summary>Identifies the renderer currently selected by the toast system.</summary>
    public enum ToastRendererKind
    {
        /// <summary>No renderer is currently active.</summary>
        None,

        /// <summary>The immediate-mode Unity GUI renderer is active.</summary>
        Imgui,

        /// <summary>The optional Canvas-based Unity UI renderer is active.</summary>
        Ugui,
    }
}
