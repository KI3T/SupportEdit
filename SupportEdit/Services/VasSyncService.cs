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
    public sealed class VasClickedWindowInfo
    {
        public int CursorX { get; set; }
        public int CursorY { get; set; }
        public IntPtr Handle { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string WindowText { get; set; } = string.Empty;
        public uint ProcessId { get; set; }
        public IntPtr ParentHandle { get; set; }
        public string ParentClassName { get; set; } = string.Empty;
        public string ParentWindowText { get; set; } = string.Empty;
        public IntPtr GrandParentHandle { get; set; }
        public string GrandParentClassName { get; set; } = string.Empty;
        public string GrandParentWindowText { get; set; } = string.Empty;
    }

    public struct VasWindowRect
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }
    }

    public sealed class VasManualCopyResult
    {
        public bool ProcessFound { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public IntPtr VasWindow { get; set; }
        public string WindowTitle { get; set; } = string.Empty;
        public string WindowSearchDebug { get; set; } = string.Empty;
        public VasWindowRect WindowRect { get; set; }
        public int TabClickX { get; set; }
        public int TabClickY { get; set; }
        public int LogClickX { get; set; }
        public int LogClickY { get; set; }
        public bool Activated { get; set; }
        public bool ClickedSuccessTab { get; set; }
        public bool ClickedLog { get; set; }
        public VasClickedWindowInfo ClickedWindow { get; set; }
        public VasClickedWindowInfo FocusedWindowAfterClick { get; set; }
        public bool ForcedEditFocus { get; set; }
        public bool DirectEditTextAttempted { get; set; }
        public bool DirectEditTextSucceeded { get; set; }
        public string DirectEditTextDebug { get; set; } = string.Empty;
        public bool EnumerateEditTextAttempted { get; set; }
        public bool EnumerateEditTextSucceeded { get; set; }
        public string EnumerateEditTextDebug { get; set; } = string.Empty;
        public int ClipboardBeforeLength { get; set; }
        public string ClipboardBeforePreview { get; set; } = string.Empty;
        public int ClipboardAfterLength { get; set; }
        public string Text { get; set; }
        public bool DidCtrlHome { get; set; }
        public bool DidCtrlShiftEnd { get; set; }
        public bool DidCtrlC { get; set; }
        public string CtrlHomeInputDebug { get; set; } = string.Empty;
        public string CtrlShiftEndInputDebug { get; set; } = string.Empty;
        public string CtrlCInputDebug { get; set; } = string.Empty;
        public bool DirectEditCopyAttempted { get; set; }
        public bool DirectEditCopySucceeded { get; set; }
        public string DirectEditCopyDebug { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public string ValidationFlags { get; set; } = string.Empty;
        public string DebugLogPath { get; set; } = string.Empty;
        public Exception Error { get; set; }
    }

    public class VasSyncService
    {
        private readonly ConfigService _config;
        private readonly Win32InputService _input;

        public VasSyncService(ConfigService config, Win32InputService input)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _input = input ?? throw new ArgumentNullException(nameof(input));
        }

        public Process FindVasProcess()
        {
            var name = (_config.VasProcessName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            try
            {
                return Process.GetProcessesByName(name).FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        public IntPtr FindVasWindow()
        {
            return FindVasWindow(out _);
        }

        private IntPtr FindVasWindow(out string searchDebug)
        {
            searchDebug = string.Empty;
            var process = FindVasProcess();
            if (process == null)
            {
                return IntPtr.Zero;
            }

            try { process.Refresh(); } catch { }

            IntPtr titleMatch = IntPtr.Zero;
            IntPtr mainWindowMatch = IntPtr.Zero;
            IntPtr largestVisibleMatch = IntPtr.Zero;
            IntPtr firstVisibleMatch = IntPtr.Zero;
            long largestArea = -1;
            var keyword1 = (_config.VasWindowTitleKeyword ?? string.Empty).Trim();
            var keyword2 = (_config.VasMainWindowTitleKeyword ?? string.Empty).Trim();
            var debug = new StringBuilder();
            debug.AppendLine($"FindVasWindow PID={process.Id}, ProcessName={process.ProcessName}");
            debug.AppendLine($"Process.MainWindowHandle=0x{process.MainWindowHandle.ToInt64():X}");
            debug.AppendLine($"Process.MainWindowTitle={process.MainWindowTitle}");
            debug.AppendLine($"Keyword1={keyword1}");
            debug.AppendLine($"Keyword2={keyword2}");

            NativeMethods.EnumWindows((hWnd, lParam) =>
            {
                uint pid = 0;
                try { NativeMethods.GetWindowThreadProcessId(hWnd, out pid); } catch { }
                if (pid != process.Id)
                {
                    return true;
                }

                var visible = NativeMethods.IsWindowVisible(hWnd);
                var title = GetWindowTitle(hWnd);
                var className = GetClassName(hWnd);
                NativeMethods.GetWindowRect(hWnd, out var rect);
                var width = Math.Max(0, rect.Right - rect.Left);
                var height = Math.Max(0, rect.Bottom - rect.Top);
                var area = (long)width * height;
                debug.AppendLine($"Candidate hwnd=0x{hWnd.ToInt64():X}, visible={visible}, rect={rect.Left},{rect.Top},{rect.Right},{rect.Bottom}, area={area}, class={className}, title={title}");

                if (!NativeMethods.IsWindowVisible(hWnd))
                {
                    return true;
                }

                if (firstVisibleMatch == IntPtr.Zero)
                {
                    firstVisibleMatch = hWnd;
                }

                if (hWnd == process.MainWindowHandle)
                {
                    mainWindowMatch = hWnd;
                }

                bool isTitleMatch = false;
                if (!string.IsNullOrWhiteSpace(keyword1) && title.IndexOf(keyword1, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isTitleMatch = true;
                }
                if (!isTitleMatch && !string.IsNullOrWhiteSpace(keyword2) && title.IndexOf(keyword2, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isTitleMatch = true;
                }

                if (isTitleMatch)
                {
                    titleMatch = hWnd;
                    return false;
                }

                if (area > largestArea)
                {
                    largestArea = area;
                    largestVisibleMatch = hWnd;
                }

                return true;
            }, IntPtr.Zero);

            var selected = titleMatch != IntPtr.Zero
                ? titleMatch
                : mainWindowMatch != IntPtr.Zero
                    ? mainWindowMatch
                    : largestVisibleMatch != IntPtr.Zero
                        ? largestVisibleMatch
                        : firstVisibleMatch;

            debug.AppendLine($"Selected hwnd=0x{selected.ToInt64():X}");
            searchDebug = debug.ToString();
            return selected;
        }

        public bool ActivateVasWindow()
        {
            var hWnd = FindVasWindow();
            if (hWnd == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
                Thread.Sleep(100);
                return NativeMethods.SetForegroundWindow(hWnd);
            }
            catch
            {
                return false;
            }
        }

        public string ReadVasSuccessLog()
        {
            return ReadVasLogByClipboardFallback();
        }

        public string ReadVasSuccessLogByAutomation()
        {
            try
            {
                var hWnd = FindVasWindow();
                if (hWnd == IntPtr.Zero)
                {
                    return null;
                }

                var root = AutomationElement.FromHandle(hWnd);
                if (root == null)
                {
                    return null;
                }

                var element = FindVasLogElement(root);
                if (element == null)
                {
                    return null;
                }

                var text = ReadTextFromElement(element);
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
            catch
            {
                return null;
            }
        }

        public string ReadVasSuccessLogByMouseClipboard()
        {
            return ReadVasLogByClipboardFallback();
        }

        public string ReadVasLogByClipboardFallback()
        {
            var result = RunVasManualCopySequence(true);
            return string.IsNullOrWhiteSpace(result.Text) ? null : result.Text;
        }

        public VasManualCopyResult RunVasManualCopySequence(bool saveDebugLog)
        {
            var result = new VasManualCopyResult();
            try
            {
                var process = FindVasProcess();
                result.ProcessFound = process != null;
                if (process != null)
                {
                    result.ProcessName = process.ProcessName;
                    result.ProcessId = process.Id;
                }

                var hWnd = FindVasWindow(out var searchDebug);
                result.WindowSearchDebug = searchDebug;
                result.VasWindow = hWnd;
                result.WindowTitle = GetWindowTitle(hWnd);
                if (hWnd == IntPtr.Zero)
                {
                    return SaveManualCopyDebug(result, saveDebugLog);
                }

                NativeMethods.GetWindowRect(hWnd, out var rect);
                result.WindowRect = new VasWindowRect { Left = rect.Left, Top = rect.Top, Right = rect.Right, Bottom = rect.Bottom };
                result.TabClickX = rect.Left + _config.VasSuccessTabOffsetX;
                result.TabClickY = rect.Top + _config.VasSuccessTabOffsetY;
                result.LogClickX = rect.Left + _config.VasLogClickOffsetX;
                result.LogClickY = rect.Top + _config.VasLogClickOffsetY;

                result.Activated = ActivateVasWindow();
                Thread.Sleep(200);

                result.ClickedSuccessTab = ClickVasSuccessTab(hWnd);
                Thread.Sleep(150);

                result.ClickedLog = ClickVasLogArea(hWnd);
                Thread.Sleep(200);
                result.ClickedWindow = GetWindowInfoFromPoint(result.LogClickX, result.LogClickY);
                result.ForcedEditFocus = ForceFocusWindow(result.ClickedWindow?.Handle ?? IntPtr.Zero);
                Thread.Sleep(100);
                result.FocusedWindowAfterClick = GetFocusedWindowInfo();

                result.DirectEditTextAttempted = true;
                result.DirectEditTextSucceeded = TryReadEditTextByMessage(result.ClickedWindow?.Handle ?? IntPtr.Zero, out var directWindowText, out var directWindowTextDebug);
                result.DirectEditTextDebug = directWindowTextDebug;
                if (!string.IsNullOrEmpty(directWindowText))
                {
                    result.Text = directWindowText;
                    result.ClipboardAfterLength = result.Text.Length;
                    result.IsValid = ValidateVasLogText(result.Text);
                    result.ValidationFlags = BuildValidationFlags(result.Text);
                    if (result.IsValid)
                    {
                        return SaveManualCopyDebug(result, saveDebugLog);
                    }
                }

                result.EnumerateEditTextAttempted = true;
                result.EnumerateEditTextSucceeded = TryReadBestEditTextByEnumeration(hWnd, out var enumeratedText, out var enumerateDebug);
                result.EnumerateEditTextDebug = enumerateDebug;
                if (!string.IsNullOrEmpty(enumeratedText))
                {
                    result.Text = enumeratedText;
                    result.ClipboardAfterLength = result.Text.Length;
                    result.IsValid = ValidateVasLogText(result.Text);
                    result.ValidationFlags = BuildValidationFlags(result.Text);
                    if (result.IsValid)
                    {
                        return SaveManualCopyDebug(result, saveDebugLog);
                    }
                }

                string clipboardBefore = null;
                try { clipboardBefore = Clipboard.ContainsText() ? Clipboard.GetText() : null; } catch { }
                result.ClipboardBeforeLength = string.IsNullOrEmpty(clipboardBefore) ? 0 : clipboardBefore.Length;
                result.ClipboardBeforePreview = Preview(clipboardBefore);

                const string marker = "SUPPORTEDIT_VAS_MARKER";
                try { Clipboard.SetText(marker); } catch { }
                Thread.Sleep(50);

                result.DidCtrlHome = _input.SendCtrlHome();
                result.CtrlHomeInputDebug = _input.LastSendInputDebug;
                Thread.Sleep(100);

                result.DidCtrlShiftEnd = _input.SendCtrlShiftEnd();
                result.CtrlShiftEndInputDebug = _input.LastSendInputDebug;
                Thread.Sleep(250);

                result.DidCtrlC = _input.SendCtrlC();
                result.CtrlCInputDebug = _input.LastSendInputDebug;

                const int retryCount = 10;
                const int delay = 200;
                for (int i = 0; i < retryCount; i++)
                {
                    Thread.Sleep(delay);
                    try
                    {
                        var clip = Clipboard.ContainsText() ? Clipboard.GetText() : null;
                        if (!string.IsNullOrEmpty(clip) && clip != marker)
                        {
                            result.Text = clip;
                            break;
                        }
                    }
                    catch
                    {
                    }
                }

                if (string.IsNullOrEmpty(result.Text))
                {
                    result.DirectEditCopyAttempted = true;
                    result.DirectEditCopySucceeded = TryCopyEditControlByMessage(result.ClickedWindow?.Handle ?? IntPtr.Zero, out var directText, out var directDebug);
                    result.DirectEditCopyDebug = directDebug;
                    if (!string.IsNullOrEmpty(directText))
                    {
                        result.Text = directText;
                    }
                }

                result.ClipboardAfterLength = string.IsNullOrEmpty(result.Text) ? 0 : result.Text.Length;
                result.IsValid = ValidateVasLogText(result.Text);
                result.ValidationFlags = BuildValidationFlags(result.Text);
                return SaveManualCopyDebug(result, saveDebugLog);
            }
            catch (Exception ex)
            {
                result.Error = ex;
                result.IsValid = false;
                return SaveManualCopyDebug(result, saveDebugLog);
            }
        }

        public bool GetVasWindowRect(out int x, out int y, out int width, out int height)
        {
            x = y = width = height = 0;
            var hWnd = FindVasWindow();
            if (hWnd == IntPtr.Zero || !NativeMethods.GetWindowRect(hWnd, out var rect))
            {
                return false;
            }

            x = rect.Left;
            y = rect.Top;
            width = rect.Right - rect.Left;
            height = rect.Bottom - rect.Top;
            return true;
        }

        public VasClickedWindowInfo TestClickVasLog()
        {
            var hWnd = FindVasWindow();
            if (hWnd == IntPtr.Zero)
            {
                return null;
            }

            ActivateVasWindow();
            Thread.Sleep(200);
            ClickVasSuccessTab(hWnd);
            Thread.Sleep(150);
            ClickVasLogArea(hWnd);
            Thread.Sleep(150);

            if (NativeMethods.GetWindowRect(hWnd, out var rect))
            {
                return GetWindowInfoFromPoint(rect.Left + _config.VasLogClickOffsetX, rect.Top + _config.VasLogClickOffsetY);
            }

            return null;
        }

        public string SaveTestClickVasLogDebug(VasClickedWindowInfo info)
        {
            var sb = new StringBuilder();
            sb.AppendLine("ACTION: TestClickVasLog");
            AppendClickedWindowInfo(sb, info);
            return DebugLogService.SaveDebugLog("TestClickVasLog", sb.ToString());
        }

        public bool TryClickVasLogArea()
        {
            try
            {
                var hWnd = FindVasWindow();
                if (hWnd == IntPtr.Zero)
                {
                    return false;
                }

                return ClickVasLogArea(hWnd);
            }
            catch
            {
                return false;
            }
        }

        public bool ValidateVasLogText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            return text.IndexOf("Gửi tiếp nhận file", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Gửi tiếp nhận", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Mã giao dịch", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Mã giao dich", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("HSKCB", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("79408_", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("SignatureXml", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private AutomationElement FindVasLogElement(AutomationElement root)
        {
            if (root == null)
            {
                return null;
            }

            try
            {
                var byAutomationId = root.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "cmthanhcong"));
                if (byAutomationId != null)
                {
                    return byAutomationId;
                }
            }
            catch
            {
            }

            try
            {
                var byName = root.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.NameProperty, "cmthanhcong"));
                if (byName != null)
                {
                    return byName;
                }
            }
            catch
            {
            }

            try
            {
                var candidate = root.FindAll(TreeScope.Descendants, Condition.TrueCondition)
                    .Cast<AutomationElement>()
                    .FirstOrDefault(x =>
                    {
                        try
                        {
                            return string.Equals(x.Current.AutomationId, "cmthanhcong", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(x.Current.Name, "cmthanhcong", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(x.Current.ControlType?.ProgrammaticName, "ControlType.Document", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(x.Current.ControlType?.ProgrammaticName, "ControlType.Edit", StringComparison.OrdinalIgnoreCase);
                        }
                        catch
                        {
                            return false;
                        }
                    });
                return candidate;
            }
            catch
            {
                return null;
            }
        }

        private string ReadTextFromElement(AutomationElement element)
        {
            if (element == null)
            {
                return null;
            }

            try
            {
                if (element.TryGetCurrentPattern(ValuePattern.Pattern, out object valuePatternObj) && valuePatternObj is ValuePattern valuePattern)
                {
                    var value = valuePattern.Current.Value;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
            catch
            {
            }

            try
            {
                if (element.TryGetCurrentPattern(TextPattern.Pattern, out object textPatternObj) && textPatternObj is TextPattern textPattern)
                {
                    var value = textPattern.DocumentRange.GetText(-1);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
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

        private bool ClickVasLogArea(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                return false;
            }

            if (!NativeMethods.GetWindowRect(hWnd, out NativeMethods.RECT rect))
            {
                return false;
            }

            var x = rect.Left + _config.VasLogClickOffsetX;
            var y = rect.Top + _config.VasLogClickOffsetY;
            NativeMethods.SetCursorPos(x, y);
            Thread.Sleep(40);
            NativeMethods.Mouse_Event(NativeMethods.MOUSEEVENTF_LEFTDOWN, (uint)x, (uint)y, 0, IntPtr.Zero);
            Thread.Sleep(30);
            NativeMethods.Mouse_Event(NativeMethods.MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, IntPtr.Zero);
            return true;
        }

        private bool ClickVasSuccessTab(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                return false;
            }

            if (!NativeMethods.GetWindowRect(hWnd, out NativeMethods.RECT rect))
            {
                return false;
            }

            var x = rect.Left + _config.VasSuccessTabOffsetX;
            var y = rect.Top + _config.VasSuccessTabOffsetY;
            NativeMethods.SetCursorPos(x, y);
            Thread.Sleep(40);
            NativeMethods.Mouse_Event(NativeMethods.MOUSEEVENTF_LEFTDOWN, (uint)x, (uint)y, 0, IntPtr.Zero);
            Thread.Sleep(30);
            NativeMethods.Mouse_Event(NativeMethods.MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, IntPtr.Zero);
            return true;
        }

        private VasClickedWindowInfo GetWindowInfoFromPoint(int x, int y)
        {
            var info = new VasClickedWindowInfo { CursorX = x, CursorY = y };
            try
            {
                var point = new NativeMethods.POINT { X = x, Y = y };
                var hWnd = NativeMethods.WindowFromPoint(point);
                info.Handle = hWnd;
                info.ClassName = GetClassName(hWnd);
                info.WindowText = GetWindowTitle(hWnd);
                NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
                info.ProcessId = pid;

                var parent = NativeMethods.GetParent(hWnd);
                info.ParentHandle = parent;
                info.ParentClassName = GetClassName(parent);
                info.ParentWindowText = GetWindowTitle(parent);

                var grandParent = NativeMethods.GetParent(parent);
                info.GrandParentHandle = grandParent;
                info.GrandParentClassName = GetClassName(grandParent);
                info.GrandParentWindowText = GetWindowTitle(grandParent);
            }
            catch
            {
            }
            return info;
        }

        private VasClickedWindowInfo GetFocusedWindowInfo()
        {
            var info = new VasClickedWindowInfo();
            try
            {
                var foreground = NativeMethods.GetForegroundWindow();
                var targetThread = NativeMethods.GetWindowThreadProcessId(foreground, out _);
                var currentThread = NativeMethods.GetCurrentThreadId();
                var attached = false;
                if (targetThread != 0 && targetThread != currentThread)
                {
                    attached = NativeMethods.AttachThreadInput(currentThread, targetThread, true);
                }

                try
                {
                    var focus = NativeMethods.GetFocus();
                    info.Handle = focus;
                    info.ClassName = GetClassName(focus);
                    info.WindowText = GetWindowTitle(focus);
                    NativeMethods.GetWindowThreadProcessId(focus, out uint pid);
                    info.ProcessId = pid;

                    var parent = NativeMethods.GetParent(focus);
                    info.ParentHandle = parent;
                    info.ParentClassName = GetClassName(parent);
                    info.ParentWindowText = GetWindowTitle(parent);

                    var grandParent = NativeMethods.GetParent(parent);
                    info.GrandParentHandle = grandParent;
                    info.GrandParentClassName = GetClassName(grandParent);
                    info.GrandParentWindowText = GetWindowTitle(grandParent);
                }
                finally
                {
                    if (attached)
                    {
                        NativeMethods.AttachThreadInput(currentThread, targetThread, false);
                    }
                }
            }
            catch
            {
            }

            return info;
        }

        private bool ForceFocusWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                var foreground = NativeMethods.GetForegroundWindow();
                var targetThread = NativeMethods.GetWindowThreadProcessId(hWnd, out _);
                var foregroundThread = NativeMethods.GetWindowThreadProcessId(foreground, out _);
                var currentThread = NativeMethods.GetCurrentThreadId();
                var attachedTarget = false;
                var attachedForeground = false;

                if (targetThread != 0 && targetThread != currentThread)
                {
                    attachedTarget = NativeMethods.AttachThreadInput(currentThread, targetThread, true);
                }
                if (foregroundThread != 0 && foregroundThread != currentThread && foregroundThread != targetThread)
                {
                    attachedForeground = NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);
                }

                try
                {
                    NativeMethods.SetFocus(hWnd);
                    NativeMethods.SetActiveWindow(NativeMethods.GetAncestor(hWnd, NativeMethods.GA_ROOT));
                    NativeMethods.SetForegroundWindow(NativeMethods.GetAncestor(hWnd, NativeMethods.GA_ROOT));
                    return NativeMethods.GetFocus() == hWnd;
                }
                finally
                {
                    if (attachedForeground)
                    {
                        NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
                    }
                    if (attachedTarget)
                    {
                        NativeMethods.AttachThreadInput(currentThread, targetThread, false);
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private bool TryCopyEditControlByMessage(IntPtr editHwnd, out string text, out string debugText)
        {
            text = null;
            var debug = new StringBuilder();
            try
            {
                debug.AppendLine($"edit hwnd: 0x{editHwnd.ToInt64():X}");
                if (editHwnd == IntPtr.Zero)
                {
                    debugText = debug.ToString();
                    return false;
                }

                var className = GetClassName(editHwnd);
                debug.AppendLine($"edit class: {className}");
                if (className.IndexOf("EDIT", StringComparison.OrdinalIgnoreCase) < 0
                    && className.IndexOf("RichEdit", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    debug.AppendLine("abort: clicked hwnd is not an edit-like control");
                    debugText = debug.ToString();
                    return false;
                }

                var root = NativeMethods.GetAncestor(editHwnd, NativeMethods.GA_ROOT);
                NativeMethods.SetForegroundWindow(root);
                Thread.Sleep(100);
                NativeMethods.SendMessageTimeout(editHwnd, NativeMethods.WM_SETFOCUS, IntPtr.Zero, IntPtr.Zero, NativeMethods.SMTO_ABORTIFHUNG, 1000, out var focusResult);
                debug.AppendLine($"WM_SETFOCUS result: 0x{focusResult.ToInt64():X}");

                const string marker = "SUPPORTEDIT_VAS_MARKER_DIRECT";
                try { Clipboard.SetText(marker); } catch (Exception ex) { debug.AppendLine("Clipboard marker error: " + ex.Message); }
                Thread.Sleep(50);

                NativeMethods.SendMessageTimeout(editHwnd, NativeMethods.EM_SETSEL, IntPtr.Zero, new IntPtr(-1), NativeMethods.SMTO_ABORTIFHUNG, 1000, out var setSelResult);
                debug.AppendLine($"EM_SETSEL(0,-1) result: 0x{setSelResult.ToInt64():X}");
                Thread.Sleep(100);

                NativeMethods.SendMessageTimeout(editHwnd, NativeMethods.WM_COPY, IntPtr.Zero, IntPtr.Zero, NativeMethods.SMTO_ABORTIFHUNG, 1000, out var copyResult);
                debug.AppendLine($"WM_COPY result: 0x{copyResult.ToInt64():X}");

                for (int i = 0; i < 10; i++)
                {
                    Thread.Sleep(200);
                    try
                    {
                        var clip = Clipboard.ContainsText() ? Clipboard.GetText() : null;
                        debug.AppendLine($"Clipboard retry {i + 1}: length={(clip == null ? 0 : clip.Length)}, changed={clip != marker}");
                        if (!string.IsNullOrEmpty(clip) && clip != marker)
                        {
                            text = clip;
                            debugText = debug.ToString();
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        debug.AppendLine($"Clipboard retry {i + 1} error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                debug.AppendLine("Direct edit copy exception:");
                debug.AppendLine(ex.ToString());
            }

            debugText = debug.ToString();
            return false;
        }

        private bool TryReadEditTextByMessage(IntPtr editHwnd, out string text, out string debugText)
        {
            text = null;
            var debug = new StringBuilder();
            try
            {
                debug.AppendLine($"edit hwnd: 0x{editHwnd.ToInt64():X}");
                if (editHwnd == IntPtr.Zero)
                {
                    debugText = debug.ToString();
                    return false;
                }

                var className = GetClassName(editHwnd);
                debug.AppendLine($"edit class: {className}");
                if (className.IndexOf("EDIT", StringComparison.OrdinalIgnoreCase) < 0
                    && className.IndexOf("RichEdit", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    debug.AppendLine("abort: clicked hwnd is not an edit-like control");
                    debugText = debug.ToString();
                    return false;
                }

                NativeMethods.SendMessageTimeout(editHwnd, NativeMethods.WM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero, NativeMethods.SMTO_ABORTIFHUNG, 2000, out var lengthResult);
                var length = lengthResult.ToInt64();
                debug.AppendLine($"WM_GETTEXTLENGTH: {length}");
                if (length <= 0 || length > int.MaxValue - 1)
                {
                    debugText = debug.ToString();
                    return false;
                }

                var buffer = new StringBuilder((int)length + 1);
                NativeMethods.SendMessageTimeoutText(editHwnd, NativeMethods.WM_GETTEXT, new IntPtr(buffer.Capacity), buffer, NativeMethods.SMTO_ABORTIFHUNG, 5000, out var getTextResult);
                text = buffer.ToString();
                debug.AppendLine($"WM_GETTEXT copied chars: {getTextResult.ToInt64()}");
                debug.AppendLine($"result length: {(text == null ? 0 : text.Length)}");
                debug.AppendLine("first 500 chars:");
                debug.AppendLine(Preview(text));
                debugText = debug.ToString();
                return !string.IsNullOrEmpty(text);
            }
            catch (Exception ex)
            {
                debug.AppendLine("Direct edit text exception:");
                debug.AppendLine(ex.ToString());
            }

            debugText = debug.ToString();
            return false;
        }

        private bool TryReadBestEditTextByEnumeration(IntPtr rootHwnd, out string text, out string debugText)
        {
            text = null;
            var debug = new StringBuilder();
            var bestText = string.Empty;
            var bestScore = int.MinValue;
            var bestHwnd = IntPtr.Zero;

            try
            {
                debug.AppendLine($"root hwnd: 0x{rootHwnd.ToInt64():X}");
                if (rootHwnd == IntPtr.Zero)
                {
                    debugText = debug.ToString();
                    return false;
                }

                var candidates = new System.Collections.Generic.List<IntPtr>();
                NativeMethods.EnumChildWindows(rootHwnd, (hWnd, lParam) =>
                {
                    var className = GetClassName(hWnd);
                    if (className.IndexOf("EDIT", StringComparison.OrdinalIgnoreCase) >= 0
                        || className.IndexOf("RichEdit", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        candidates.Add(hWnd);
                    }
                    return true;
                }, IntPtr.Zero);

                debug.AppendLine($"edit candidate count: {candidates.Count}");
                foreach (var hWnd in candidates)
                {
                    var ok = TryReadEditTextByMessage(hWnd, out var candidateText, out var candidateDebug);
                    var length = candidateText == null ? 0 : candidateText.Length;
                    var valid = ValidateVasLogText(candidateText);
                    var flags = BuildValidationFlags(candidateText);
                    var score = (valid ? 1000000 : 0) + length;
                    NativeMethods.GetWindowRect(hWnd, out var rect);

                    debug.AppendLine("---- candidate ----");
                    debug.AppendLine($"hwnd: 0x{hWnd.ToInt64():X}");
                    debug.AppendLine($"class: {GetClassName(hWnd)}");
                    debug.AppendLine($"rect: {rect.Left},{rect.Top},{rect.Right},{rect.Bottom}");
                    debug.AppendLine($"read ok: {ok}");
                    debug.AppendLine($"length: {length}");
                    debug.AppendLine($"valid: {valid}");
                    debug.AppendLine($"flags: {flags}");
                    debug.AppendLine("first 300 chars:");
                    debug.AppendLine(string.IsNullOrEmpty(candidateText) ? string.Empty : candidateText.Substring(0, Math.Min(300, candidateText.Length)));
                    debug.AppendLine("read debug:");
                    debug.AppendLine(candidateDebug);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestText = candidateText ?? string.Empty;
                        bestHwnd = hWnd;
                    }
                }

                debug.AppendLine($"best hwnd: 0x{bestHwnd.ToInt64():X}");
                debug.AppendLine($"best score: {bestScore}");
                debug.AppendLine($"best length: {bestText.Length}");
                debug.AppendLine($"best flags: {BuildValidationFlags(bestText)}");
                text = bestText;
                debugText = debug.ToString();
                return !string.IsNullOrEmpty(text);
            }
            catch (Exception ex)
            {
                debug.AppendLine("Enumerate edit text exception:");
                debug.AppendLine(ex.ToString());
            }

            debugText = debug.ToString();
            return false;
        }

        private string GetClassName(IntPtr hWnd)
        {
            try
            {
                if (hWnd == IntPtr.Zero)
                {
                    return string.Empty;
                }

                var sb = new StringBuilder(256);
                var len = NativeMethods.GetClassName(hWnd, sb, sb.Capacity);
                return len <= 0 ? string.Empty : sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private VasManualCopyResult SaveManualCopyDebug(VasManualCopyResult result, bool saveDebugLog)
        {
            if (!saveDebugLog)
            {
                return result;
            }

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("ACTION = ReadVasByManualCopy");
                sb.AppendLine($"process found: {result.ProcessFound}");
                sb.AppendLine($"process: {result.ProcessName} PID={result.ProcessId}");
                sb.AppendLine($"window title: {result.WindowTitle}");
                sb.AppendLine($"VAS window handle: 0x{result.VasWindow.ToInt64():X}");
                sb.AppendLine("VAS window search:");
                sb.AppendLine(result.WindowSearchDebug ?? string.Empty);
                sb.AppendLine($"VAS window rect: L={result.WindowRect.Left}, T={result.WindowRect.Top}, R={result.WindowRect.Right}, B={result.WindowRect.Bottom}");
                sb.AppendLine($"success tab offset: {_config.VasSuccessTabOffsetX}/{_config.VasSuccessTabOffsetY}");
                sb.AppendLine($"log click offset: {_config.VasLogClickOffsetX}/{_config.VasLogClickOffsetY}");
                sb.AppendLine($"tab click point: {result.TabClickX},{result.TabClickY}");
                sb.AppendLine($"log click point: {result.LogClickX},{result.LogClickY}");
                sb.AppendLine($"activated: {result.Activated}");
                sb.AppendLine($"clicked success tab: {result.ClickedSuccessTab}");
                sb.AppendLine($"clicked log: {result.ClickedLog}");
                AppendClickedWindowInfo(sb, result.ClickedWindow);
                sb.AppendLine($"forced edit focus: {result.ForcedEditFocus}");
                sb.AppendLine("focused hwnd after click:");
                AppendClickedWindowInfo(sb, result.FocusedWindowAfterClick);
                sb.AppendLine($"direct edit text attempted: {result.DirectEditTextAttempted}");
                sb.AppendLine($"direct edit text succeeded: {result.DirectEditTextSucceeded}");
                sb.AppendLine("direct edit text debug:");
                sb.AppendLine(result.DirectEditTextDebug);
                sb.AppendLine($"enumerate edit text attempted: {result.EnumerateEditTextAttempted}");
                sb.AppendLine($"enumerate edit text succeeded: {result.EnumerateEditTextSucceeded}");
                sb.AppendLine("enumerate edit text debug:");
                sb.AppendLine(result.EnumerateEditTextDebug);
                sb.AppendLine($"clipboard before length: {result.ClipboardBeforeLength}");
                sb.AppendLine("clipboard before first 500 chars:");
                sb.AppendLine(result.ClipboardBeforePreview);
                sb.AppendLine($"did Ctrl+Home: {result.DidCtrlHome}");
                sb.AppendLine("Ctrl+Home SendInput debug:");
                sb.AppendLine(result.CtrlHomeInputDebug);
                sb.AppendLine($"did Ctrl+Shift+End: {result.DidCtrlShiftEnd}");
                sb.AppendLine("Ctrl+Shift+End SendInput debug:");
                sb.AppendLine(result.CtrlShiftEndInputDebug);
                sb.AppendLine($"did Ctrl+C: {result.DidCtrlC}");
                sb.AppendLine("Ctrl+C SendInput debug:");
                sb.AppendLine(result.CtrlCInputDebug);
                sb.AppendLine($"direct edit copy attempted: {result.DirectEditCopyAttempted}");
                sb.AppendLine($"direct edit copy succeeded: {result.DirectEditCopySucceeded}");
                sb.AppendLine("direct edit copy debug:");
                sb.AppendLine(result.DirectEditCopyDebug);
                sb.AppendLine($"clipboard after length: {result.ClipboardAfterLength}");
                sb.AppendLine("first 500 chars:");
                sb.AppendLine(Preview(result.Text));
                sb.AppendLine($"validation result: {result.IsValid}");
                sb.AppendLine($"validation flags: {result.ValidationFlags}");
                if (result.Error != null)
                {
                    sb.AppendLine("error:");
                    sb.AppendLine(result.Error.ToString());
                }

                result.DebugLogPath = DebugLogService.SaveDebugLog("ReadVasByManualCopy", sb.ToString());
            }
            catch
            {
            }

            return result;
        }

        private void AppendClickedWindowInfo(StringBuilder sb, VasClickedWindowInfo info)
        {
            if (info == null)
            {
                sb.AppendLine("clicked hwnd: <null>");
                return;
            }

            sb.AppendLine($"cursor position: {info.CursorX},{info.CursorY}");
            sb.AppendLine($"WindowFromPoint handle: 0x{info.Handle.ToInt64():X}");
            sb.AppendLine($"clicked class: {info.ClassName}");
            sb.AppendLine($"clicked text: {info.WindowText}");
            sb.AppendLine($"clicked process id: {info.ProcessId}");
            sb.AppendLine($"parent handle: 0x{info.ParentHandle.ToInt64():X}");
            sb.AppendLine($"parent class: {info.ParentClassName}");
            sb.AppendLine($"parent text: {info.ParentWindowText}");
            sb.AppendLine($"grandparent handle: 0x{info.GrandParentHandle.ToInt64():X}");
            sb.AppendLine($"grandparent class: {info.GrandParentClassName}");
            sb.AppendLine($"grandparent text: {info.GrandParentWindowText}");
        }

        private string BuildValidationFlags(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "EMPTY";
            }

            var flags = new System.Collections.Generic.List<string>();
            AddFlag(flags, text, "Gửi tiếp nhận file");
            AddFlag(flags, text, "Gửi tiếp nhận");
            AddFlag(flags, text, "Mã giao dịch");
            AddFlag(flags, text, "Mã giao dich");
            AddFlag(flags, text, "HSKCB");
            AddFlag(flags, text, "79408_");
            AddFlag(flags, text, "SignatureXml");
            return flags.Count == 0 ? "NO_MATCH" : string.Join(", ", flags);
        }

        private void AddFlag(System.Collections.Generic.List<string> flags, string text, string token)
        {
            if (text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                flags.Add(token);
            }
        }

        private string Preview(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Substring(0, Math.Min(500, text.Length));
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

        private static class NativeMethods
        {
            public const int SW_SHOW = 5;
            public const int SW_RESTORE = 9;
            public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
            public const uint MOUSEEVENTF_LEFTUP = 0x0004;
            public const uint WM_SETFOCUS = 0x0007;
            public const uint WM_GETTEXT = 0x000D;
            public const uint WM_GETTEXTLENGTH = 0x000E;
            public const uint EM_SETSEL = 0x00B1;
            public const uint WM_COPY = 0x0301;
            public const uint SMTO_ABORTIFHUNG = 0x0002;

            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int Left;
                public int Top;
                public int Right;
                public int Bottom;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct POINT
            {
                public int X;
                public int Y;
            }

            public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
            public delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

            [DllImport("user32.dll")]
            public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

            [DllImport("user32.dll")]
            public static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

            [DllImport("user32.dll")]
            public static extern int GetWindowTextLength(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool IsWindowVisible(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool SetForegroundWindow(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern IntPtr SetActiveWindow(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern IntPtr SetFocus(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern IntPtr GetFocus();

            [DllImport("user32.dll")]
            public static extern IntPtr GetForegroundWindow();

            [DllImport("kernel32.dll")]
            public static extern uint GetCurrentThreadId();

            [DllImport("user32.dll")]
            public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

            [DllImport("user32.dll")]
            public static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

            public const uint GA_ROOT = 2;

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

            [DllImport("user32.dll", EntryPoint = "SendMessageTimeout", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern IntPtr SendMessageTimeoutText(IntPtr hWnd, uint msg, IntPtr wParam, StringBuilder lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

            [DllImport("user32.dll")]
            public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

            [DllImport("user32.dll")]
            public static extern IntPtr WindowFromPoint(POINT point);

            [DllImport("user32.dll")]
            public static extern IntPtr GetParent(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

            [DllImport("user32.dll")]
            public static extern bool SetCursorPos(int x, int y);

            [DllImport("user32.dll", EntryPoint = "mouse_event")]
            public static extern void Mouse_Event(uint dwFlags, uint dx, uint dy, uint cButtons, IntPtr dwExtraInfo);

            [StructLayout(LayoutKind.Sequential)]
            public struct INPUT
            {
                public uint type;
                public InputUnion U;

                public static INPUT KeyDown(ushort key)
                {
                    return new INPUT
                    {
                        type = 1,
                        U = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = key,
                                wScan = 0,
                                dwFlags = 0,
                                time = 0,
                                dwExtraInfo = IntPtr.Zero
                            }
                        }
                    };
                }

                public static INPUT KeyUp(ushort key)
                {
                    return new INPUT
                    {
                        type = 1,
                        U = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = key,
                                wScan = 0,
                                dwFlags = 0x0002,
                                time = 0,
                                dwExtraInfo = IntPtr.Zero
                            }
                        }
                    };
                }
            }

            [StructLayout(LayoutKind.Explicit)]
            public struct InputUnion
            {
                [FieldOffset(0)] public KEYBDINPUT ki;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct KEYBDINPUT
            {
                public ushort wVk;
                public ushort wScan;
                public uint dwFlags;
                public uint time;
                public IntPtr dwExtraInfo;
            }

            [DllImport("user32.dll", SetLastError = true)]
            public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        }
    }
}
