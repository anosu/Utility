using System.Collections.Generic;

namespace Utility.Toast
{
    public static class Toast
    {
        private struct PendingMsg { public string Title, Msg; public int Type; public float Dur; }
        private static readonly Queue<PendingMsg> _pendingQueue = new();

        // 提供给 ToastUI.Awake() 调用的内部方法
        internal static void OnUIReady()
        {
            while (_pendingQueue.Count > 0)
            {
                var p = _pendingQueue.Dequeue();
                ToastUI.Instance.Show(p.Title, p.Msg, p.Type, p.Dur);
            }
        }

        public static void Show(string title, string message, int type = 0, float duration = 3f)
        {
            if (ToastUI.Instance != null)
                ToastUI.Instance.Show(title, message, type, duration);
            else
                _pendingQueue.Enqueue(new PendingMsg { Title = title, Msg = message, Type = type, Dur = duration });
        }

        public static void Info(string title, string message, float duration = 3f) => Show(title, message, ToastUI.TYPE_INFO, duration);
        public static void Success(string title, string message, float duration = 3f) => Show(title, message, ToastUI.TYPE_SUCCESS, duration);
        public static void Warn(string title, string message, float duration = 4f) => Show(title, message, ToastUI.TYPE_WARN, duration);
        public static void Error(string title, string message, float duration = 5f) => Show(title, message, ToastUI.TYPE_ERROR, duration);
        
        public static void Clear() => ToastUI.Instance?.Clear();
    }
}