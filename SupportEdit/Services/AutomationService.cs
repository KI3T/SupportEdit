using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;
using SupportEdit.Services;

namespace SupportEdit.Services
{
    // Dịch vụ tương tác với cửa sổ khác: tìm cửa sổ theo tiêu đề (contains), kích hoạt, gửi phím, copy/paste với retry
    // Lưu ý: automation theo AutomationId được triển khai trong AutomationUiService.cs; AutomationService giữ lại các chức năng cũ dùng khi cần fallback
    public class AutomationService
    {

        // PInvoke để liệt kê cửa sổ
            private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
            [DllImport("user32.dll")]
            private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

            [DllImport("user32.dll")]
            private static extern int GetWindowTextLength(IntPtr hWnd);

            [DllImport("user32.dll")]
            private static extern bool IsWindowVisible(IntPtr hWnd);

            [DllImport("user32.dll")]
            private static extern bool SetForegroundWindow(IntPtr hWnd);

            [DllImport("user32.dll")]
            private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

            [DllImport("user32.dll", SetLastError = true)]
            private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

            private const int SW_SHOW = 5;

            private readonly ConfigService _config;

            public AutomationService(ConfigService config)
            {
                _config = config ?? throw new ArgumentNullException(nameof(config));
            }

// Tìm cửa sổ có title chứa keyword (không phân biệt hoa thường) và ưu tiên theo process name nếu có cấu hình
public IntPtr FindWindowByTitleContains(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return IntPtr.Zero;
            // nếu có cấu hình process name thì ưu tiên cửa sổ vừa match title vừa match process
            var processName = _config?.GetType().GetProperty("XmlAppProcessName")?.GetValue(_config) as string;

            IntPtr bestMatch = IntPtr.Zero;
            IntPtr titleOnlyMatch = IntPtr.Zero;

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true; // continue
                int len = GetWindowTextLength(hWnd);
                if (len == 0) return true;
                var sb = new StringBuilder(len + 1);
                GetWindowText(hWnd, sb, sb.Capacity);
                var title = sb.ToString();
                if (title.IndexOf(keyword, StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    // nếu không có process filter thì return ngay
                    if (string.IsNullOrWhiteSpace(processName))
                    {
                        titleOnlyMatch = hWnd;
                        return false; // stop enumeration
                    }
                    else
                    {
                        // kiểm tra process name của window (lấy PID -> process)
                        try
                        {
                            uint pid;
                            GetWindowThreadProcessId(hWnd, out pid);
                            var proc = System.Diagnostics.Process.GetProcessById((int)pid);
                            if (proc != null && string.Equals(proc.ProcessName, processName, StringComparison.InvariantCultureIgnoreCase))
                            {
                                bestMatch = hWnd;
                                return false; // stop
                            }
                            // nếu chưa có titleOnlyMatch lưu tạm
                            if (titleOnlyMatch == IntPtr.Zero) titleOnlyMatch = hWnd;
                        }
                        catch { if (titleOnlyMatch == IntPtr.Zero) titleOnlyMatch = hWnd; }
                    }
                }
                return true;
            }, IntPtr.Zero);

            if (bestMatch != IntPtr.Zero) return bestMatch;
            return titleOnlyMatch;
        }

        // Kích hoạt cửa sổ
        public bool ActivateWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return false;
            // ensure visible then set foreground
            ShowWindow(hWnd, SW_SHOW);
            Thread.Sleep(50);
            return SetForegroundWindow(hWnd);
        }

        // Các hotkey tiện ích (dùng SendKeys)
        public void SendHotkeyCtrlA() => System.Windows.Forms.SendKeys.SendWait("^a");
        public void SendHotkeyCtrlC() => System.Windows.Forms.SendKeys.SendWait("^c");
        public void SendHotkeyCtrlV() => System.Windows.Forms.SendKeys.SendWait("^v");
        public void SendHotkeyCtrlF() => System.Windows.Forms.SendKeys.SendWait("^f");

        // Gửi text vào control đang active (dán nhanh)
        public void SendTextToActiveControl(string text)
        {
            if (text == null) text = string.Empty;
            // Set clipboard then send paste to ensure exact text
            var prev = string.Empty;
            try { prev = Clipboard.GetText(); } catch { prev = null; }
            try { Clipboard.SetText(text); } catch { }
            SendHotkeyCtrlV();
            WaitAfterAction();
            try { if (prev != null) Clipboard.SetText(prev); } catch { }
        }

        // Đợi sau hành động theo cấu hình
        public void WaitAfterAction()
        {
            var ms = _config?.FindDelayMs ?? 200;
            Thread.Sleep(ms);
        }

        // Copy toàn bộ XML hiện tại từ app ngoài: Ctrl+A, Ctrl+C, đọc Clipboard với retry
        // Enhanced with clipboard marker detection to validate real content vs stale clipboard
        public string CopyCurrentXmlFromExternalApp(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) throw new ArgumentException("hWnd is zero");

            ActivateWindow(hWnd);
            WaitAfterAction();

            // Lưu clipboard trước để so sánh
            string before = null;
            try { before = Clipboard.GetText(); } catch { before = null; }

            // Set a unique marker to verify clipboard actually changes
            var marker = "**SUPPORTEDIT_MARKER_" + Guid.NewGuid().ToString().Substring(0, 8) + "**";
            try { Clipboard.SetText(marker); } catch { }
            Thread.Sleep(100);

            SendHotkeyCtrlA();
            WaitAfterAction();
            SendHotkeyCtrlC();
            WaitAfterAction();

            var attempts = _config?.ClipboardRetryCount ?? 10;
            var delay = _config?.ClipboardRetryDelayMs ?? 300;

            for (int i = 0; i < attempts; i++)
            {
                try
                {
                    var text = Clipboard.GetText();
                    // Clipboard must have changed from marker and contain XML content
                    if (!string.IsNullOrEmpty(text) && 
                        text != marker && 
                        (text.Contains("<?xml") || text.Contains("<CHI_TIET_DVKT") || text.Contains("<CHITIEU")))
                    {
                        return text;
                    }
                }
                catch { }
                Thread.Sleep(delay);
            }

            // Last attempt: try Ctrl+C again
            try
            {
                SendHotkeyCtrlC();
                Thread.Sleep(500);
                var finalClip = Clipboard.GetText();
                if (!string.IsNullOrEmpty(finalClip) && finalClip != marker && 
                    (finalClip.Contains("<?xml") || finalClip.Contains("<CHI_TIET_DVKT")))
                {
                    return finalClip;
                }
            }
            catch { }

            return null;
        }
        // Paste xmlContent vào app ngoài: set clipboard, Ctrl+A, Ctrl+V
        public bool PasteXmlToExternalApp(IntPtr hWnd, string xmlContent)
        {
            if (hWnd == IntPtr.Zero) throw new ArgumentException("hWnd is zero");
            try
            {
                Clipboard.SetText(xmlContent ?? string.Empty);
            }
            catch { }
            ActivateWindow(hWnd);
            WaitAfterAction();
            SendHotkeyCtrlA();
            WaitAfterAction();
            SendHotkeyCtrlV();
            WaitAfterAction();
            return true;
        }
    }
}
