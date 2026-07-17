using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;

namespace SupportEdit.Services
{
    public class GdFormAutomationService
    {
        private readonly ConfigService _config;
        private readonly AutomationService _automationService;

        public GdFormAutomationService(ConfigService config, AutomationService automationService)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _automationService = automationService ?? throw new ArgumentNullException(nameof(automationService));
        }

        public bool OpenGdFormFromXmlApp()
        {
            var xmlWindow = FindXmlAppWindow();
            if (xmlWindow == IntPtr.Zero)
            {
                return false;
            }

            if (!_automationService.ActivateWindow(xmlWindow))
            {
                return false;
            }

            if (ClickMaGDButton(xmlWindow))
            {
                return true;
            }

            return ClickMaGDButtonByMouse(xmlWindow);
        }

        public IntPtr FindGdFormWindow()
        {
            return FindWindowByTitleKeyword(_config.GdFormTitleKeyword);
        }

        public bool PasteTextToGdForm(string text)
        {
            var hWnd = FindGdFormWindow();
            if (hWnd == IntPtr.Zero)
            {
                return false;
            }

            if (!ActivateWindow(hWnd))
            {
                return false;
            }

            if (SetBodyTextByAutomation(hWnd, text))
            {
                return true;
            }

            return PasteBodyTextByClipboard(hWnd, text);
        }

        public bool ClickGdConvertButton()
        {
            var hWnd = FindGdFormWindow();
            if (hWnd == IntPtr.Zero)
            {
                return false;
            }

            if (!ActivateWindow(hWnd))
            {
                return false;
            }

            if (InvokeConvertButton(hWnd))
            {
                return true;
            }

            return ClickConvertButtonByMouse(hWnd);
        }

        public string WaitForResultMessageBox()
        {
            var timeout = Math.Max(1000, _config.GdFormOpenTimeoutMs);
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeout)
            {
                try
                {
                    var hwnd = FindResultMessageBoxWindow();
                    if (hwnd != IntPtr.Zero)
                    {
                        var title = GetWindowTitle(hwnd);
                        var text = ReadMessageBoxText(hwnd);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return text;
                        }
                        if (!string.IsNullOrWhiteSpace(title))
                        {
                            return title;
                        }
                    }
                }
                catch
                {
                }

                Thread.Sleep(150);
            }

            return null;
        }

        public IntPtr FindXmlAppWindow()
        {
            return FindWindowByTitleKeyword(_config.XmlAppWindowTitleKeyword);
        }

        private bool SetBodyTextByAutomation(IntPtr hWnd, string text)
        {
            try
            {
                var root = AutomationElement.FromHandle(hWnd);
                if (root == null)
                {
                    return false;
                }

                var el = FindControl(root, "txtbody", ControlType.Document) ?? FindControl(root, "txtbody", ControlType.Edit);
                if (el == null)
                {
                    return false;
                }

                if (el.TryGetCurrentPattern(ValuePattern.Pattern, out object valueObj) && valueObj is ValuePattern valuePattern && !valuePattern.Current.IsReadOnly)
                {
                    valuePattern.SetValue(text ?? string.Empty);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private bool PasteBodyTextByClipboard(IntPtr hWnd, string text)
        {
            try
            {
                Clipboard.SetText(text ?? string.Empty);
            }
            catch
            {
            }

            if (!TryFocusBodyText(hWnd))
            {
                return false;
            }

            SendKeys.SendWait("^a");
            Thread.Sleep(80);
            SendKeys.SendWait("^v");
            Thread.Sleep(200);
            return true;
        }

        private bool TryFocusBodyText(IntPtr hWnd)
        {
            try
            {
                var root = AutomationElement.FromHandle(hWnd);
                if (root != null)
                {
                    var el = FindControl(root, "txtbody", ControlType.Document) ?? FindControl(root, "txtbody", ControlType.Edit);
                    if (el != null)
                    {
                        try
                        {
                            el.SetFocus();
                            Thread.Sleep(100);
                            return true;
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }

            return ClickBodyByMouse(hWnd);
        }

        private bool InvokeConvertButton(IntPtr hWnd)
        {
            try
            {
                var root = AutomationElement.FromHandle(hWnd);
                if (root == null)
                {
                    return false;
                }

                var button = FindControl(root, "simpleButton1", ControlType.Button) ?? FindControlByName(root, "Chuyển dữ liệu", ControlType.Button);
                if (button == null)
                {
                    return false;
                }

                if (button.TryGetCurrentPattern(InvokePattern.Pattern, out object invokeObj) && invokeObj is InvokePattern invokePattern)
                {
                    invokePattern.Invoke();
                    Thread.Sleep(150);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private bool ClickMaGDButton(IntPtr hWnd)
        {
            try
            {
                var root = AutomationElement.FromHandle(hWnd);
                if (root == null)
                {
                    return false;
                }

                var button = FindControl(root, "btnMaGD", ControlType.Button) ?? FindControlByName(root, "Mã GD", ControlType.Button);
                if (button == null)
                {
                    return false;
                }

                if (button.TryGetCurrentPattern(InvokePattern.Pattern, out object invokeObj) && invokeObj is InvokePattern invokePattern)
                {
                    invokePattern.Invoke();
                    Thread.Sleep(200);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private bool ClickMaGDButtonByMouse(IntPtr hWnd)
        {
            return ClickByOffset(hWnd, _config.MaGDButtonOffsetX, _config.MaGDButtonOffsetY);
        }

        private bool ClickConvertButtonByMouse(IntPtr hWnd)
        {
            return ClickByOffset(hWnd, _config.GdConvertButtonOffsetX, _config.GdConvertButtonOffsetY);
        }

        private bool ClickBodyByMouse(IntPtr hWnd)
        {
            return ClickByOffset(hWnd, _config.GdTxtBodyOffsetX, _config.GdTxtBodyOffsetY);
        }

        private bool ClickByOffset(IntPtr hWnd, int offsetX, int offsetY)
        {
            if (hWnd == IntPtr.Zero)
            {
                return false;
            }

            if (!NativeMethods.GetWindowRect(hWnd, out NativeMethods.RECT rect))
            {
                return false;
            }

            var x = rect.Left + offsetX;
            var y = rect.Top + offsetY;
            NativeMethods.SetCursorPos(x, y);
            Thread.Sleep(40);
            NativeMethods.Mouse_Event(NativeMethods.MOUSEEVENTF_LEFTDOWN, (uint)x, (uint)y, 0, IntPtr.Zero);
            Thread.Sleep(30);
            NativeMethods.Mouse_Event(NativeMethods.MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, IntPtr.Zero);
            Thread.Sleep(150);
            return true;
        }

        private IntPtr FindResultMessageBoxWindow()
        {
            IntPtr found = IntPtr.Zero;
            NativeMethods.EnumWindows((hWnd, lParam) =>
            {
                if (!NativeMethods.IsWindowVisible(hWnd))
                {
                    return true;
                }

                var title = GetWindowTitle(hWnd);
                if (string.IsNullOrWhiteSpace(title))
                {
                    return true;
                }

                if (title.IndexOf("mã giao dịch", StringComparison.OrdinalIgnoreCase) >= 0
                    || title.IndexOf("thông báo", StringComparison.OrdinalIgnoreCase) >= 0
                    || title.IndexOf("cập nhật mã giao dịch", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    found = hWnd;
                    return false;
                }

                return true;
            }, IntPtr.Zero);
            return found;
        }

        private string ReadMessageBoxText(IntPtr hWnd)
        {
            try
            {
                var root = AutomationElement.FromHandle(hWnd);
                if (root == null)
                {
                    return null;
                }

                var text = root.FindAll(TreeScope.Descendants, Condition.TrueCondition)
                    .Cast<AutomationElement>()
                    .Select(ReadText)
                    .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
                return text;
            }
            catch
            {
                return null;
            }
        }

        private AutomationElement FindControl(AutomationElement root, string automationId, ControlType controlType)
        {
            try
            {
                var cond = new AndCondition(
                    new PropertyCondition(AutomationElement.AutomationIdProperty, automationId),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, controlType));
                return root.FindFirst(TreeScope.Descendants, cond);
            }
            catch
            {
                return null;
            }
        }

        private AutomationElement FindControlByName(AutomationElement root, string name, ControlType controlType)
        {
            try
            {
                var cond = new AndCondition(
                    new PropertyCondition(AutomationElement.NameProperty, name),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, controlType));
                return root.FindFirst(TreeScope.Descendants, cond);
            }
            catch
            {
                return null;
            }
        }

        private string ReadText(AutomationElement element)
        {
            if (element == null)
            {
                return null;
            }

            try
            {
                if (element.TryGetCurrentPattern(ValuePattern.Pattern, out object valueObj) && valueObj is ValuePattern valuePattern)
                {
                    return valuePattern.Current.Value;
                }
            }
            catch
            {
            }

            try
            {
                if (element.TryGetCurrentPattern(TextPattern.Pattern, out object textObj) && textObj is TextPattern textPattern)
                {
                    return textPattern.DocumentRange.GetText(-1);
                }
            }
            catch
            {
            }

            try
            {
                return element.Current.Name;
            }
            catch
            {
                return null;
            }
        }

        private IntPtr FindWindowByTitleKeyword(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return IntPtr.Zero;
            }

            IntPtr found = IntPtr.Zero;
            NativeMethods.EnumWindows((hWnd, lParam) =>
            {
                if (!NativeMethods.IsWindowVisible(hWnd))
                {
                    return true;
                }

                var title = GetWindowTitle(hWnd);
                if (!string.IsNullOrWhiteSpace(title) && title.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    found = hWnd;
                    return false;
                }

                return true;
            }, IntPtr.Zero);
            return found;
        }

        private string GetWindowTitle(IntPtr hWnd)
        {
            try
            {
                int len = NativeMethods.GetWindowTextLength(hWnd);
                if (len <= 0)
                {
                    return string.Empty;
                }

                var sb = new StringBuilder(len + 1);
                NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private bool ActivateWindow(IntPtr hWnd)
        {
            try
            {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
                Thread.Sleep(100);
                return NativeMethods.SetForegroundWindow(hWnd);
            }
            catch
            {
                return false;
            }
        }

        private static class NativeMethods
        {
            public const int SW_SHOW = 5;
            public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
            public const uint MOUSEEVENTF_LEFTUP = 0x0004;

            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int Left;
                public int Top;
                public int Right;
                public int Bottom;
            }

            public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

            [DllImport("user32.dll")]
            public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

            [DllImport("user32.dll")]
            public static extern int GetWindowTextLength(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool IsWindowVisible(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool SetForegroundWindow(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

            [DllImport("user32.dll")]
            public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

            [DllImport("user32.dll")]
            public static extern bool SetCursorPos(int x, int y);

            [DllImport("user32.dll", EntryPoint = "mouse_event")]
            public static extern void Mouse_Event(uint dwFlags, uint dx, uint dy, uint cButtons, IntPtr dwExtraInfo);
        }
    }
}
