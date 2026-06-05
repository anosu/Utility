using System;
using System.Diagnostics;

namespace Utility.Legacy
{
    /// <summary>
    /// [备用] 基于 PowerShell 的 Windows 原生 Toast 通知。
    /// 通过常驻 PowerShell 进程调用 Windows Toast API。
    ///
    /// 用法：PowerShellToast.Init("AppName", msg => Log(msg));
    ///       PowerShellToast.Show("标题", "消息", 3);
    ///       PowerShellToast.Stop();
    /// </summary>
    public static class PowerShellToast
    {
        private static Process _ps;
        private static readonly object _lock = new();
        private static Action<string> _log;

        public static bool Running => _ps != null && !_ps.HasExited;

        /// <summary>初始化 PowerShell Toast 进程</summary>
        /// <param name="appName">应用标识名称</param>
        /// <param name="log">错误日志回调（可选）</param>
        public static void Init(string appName, Action<string> log = null)
        {
            _log = log;

            try
            {
                string script = $@"
$mgr = [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime]
$tpl = $mgr::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02)
$nf = $mgr::CreateToastNotifier('{appName}')
while ($true) {{
    $line = [Console]::In.ReadLine()
    if ([string]::IsNullOrEmpty($line)) {{ break }}
    try {{
        $d = ConvertFrom-Json $line
        $x = $tpl.CloneNode($true)
        $x.SelectSingleNode('//text[@id=""1""]').InnerText = $d.T
        $x.SelectSingleNode('//text[@id=""2""]').InnerText = $d.M
        $t = [Windows.UI.Notifications.ToastNotification]::new($x)
        $t.ExpirationTime = [DateTimeOffset]::Now.AddSeconds($d.E)
        $nf.Show($t)
    }} catch {{ [Console]::Error.WriteLine($_.Exception.Message) }}
}}
";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                _ps = new Process { StartInfo = psi };
                _ps.Start();
                _ps.BeginErrorReadLine();
                _ps.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        _log?.Invoke($"PS Toast: {e.Data}");
                };

                AppDomain.CurrentDomain.ProcessExit += (_, _) => Stop();
            }
            catch (Exception e)
            {
                _log?.Invoke($"PS Toast init failed: {e.Message}");
                _ps = null;
            }
        }

        /// <summary>显示 Windows 原生 Toast</summary>
        public static void Show(string title, string message, int duration = 3)
        {
            if (_ps == null || _ps.HasExited) return;

            try
            {
                string json = $"{{\"T\":\"{Esc(title)}\",\"M\":\"{Esc(message)}\",\"E\":{duration}}}";
                lock (_lock) { _ps.StandardInput.WriteLine(json); _ps.StandardInput.Flush(); }
            }
            catch (Exception e) { _log?.Invoke(e.Message); }
        }

        /// <summary>停止并清理 PowerShell 进程</summary>
        public static void Stop()
        {
            try
            {
                if (_ps != null && !_ps.HasExited)
                {
                    _ps.StandardInput.Close();
                    _ps.Kill();
                    _ps.Close();
                }
                _ps = null;
            }
            catch { /* 忽略清理异常 */ }
        }

        private static string Esc(string s)
        {
            return s.Replace("\"", "\"\"")
                    .Replace("&", "&")
                    .Replace("<", "<")
                    .Replace(">", ">");
        }
    }
}
