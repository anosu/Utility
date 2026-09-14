using System;
using UnityEngine;

namespace Utility.Notifications.Internal
{
    // IMGUI globals multiply the supplied colors. Isolate the whole draw so the
    // game cannot change toast opacity, and restore its state even if drawing fails.
    internal readonly struct ToastGuiState : IDisposable
    {
        private readonly Matrix4x4 _matrix;
        private readonly Color _color;
        private readonly Color _contentColor;
        private readonly Color _backgroundColor;
        private readonly bool _enabled;

        public ToastGuiState()
        {
            _matrix = GUI.matrix;
            _color = GUI.color;
            _contentColor = GUI.contentColor;
            _backgroundColor = GUI.backgroundColor;
            _enabled = GUI.enabled;
            try
            {
                GUI.matrix = Matrix4x4.identity;
                GUI.color = new Color(1f, 1f, 1f, 1f);
                GUI.contentColor = new Color(1f, 1f, 1f, 1f);
                GUI.backgroundColor = new Color(1f, 1f, 1f, 1f);
                GUI.enabled = true;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            GUI.matrix = _matrix;
            GUI.color = _color;
            GUI.contentColor = _contentColor;
            GUI.backgroundColor = _backgroundColor;
            GUI.enabled = _enabled;
        }
    }
}
