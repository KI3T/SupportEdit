using System;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Automation;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using MessageBox = SupportEdit.UI.SupportMessageBox;

namespace SupportEdit.Services
{
    // Dịch vụ UI Automation để thao tác với controls theo AutomationId khi có thể
    public class AutomationUiService
    {
        private readonly ConfigService _config;
        private IntPtr _mainWindowHandle;
        private AutomationElement _mainElement;

        public AutomationUiService(ConfigService config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        // Tìm cửa sổ chính theo title chứa keyword và thiết lập AutomationElement chính
        public IntPtr GetMainWindow()
        {
            var keyword = _config.XmlAppWindowTitleKeyword;
            IntPtr h = IntPtr.Zero;
            // tìm bằng EnumWindows giống AutomationService logic (simple implementation)
            NativeMethods.EnumWindows((hwnd, lParam) =>
            {
                if (!NativeMethods.IsWindowVisible(hwnd)) return true;
                int len = NativeMethods.GetWindowTextLength(hwnd);
                if (len == 0) return true;
                var sb = new StringBuilder(len + 1);
                NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
                var title = sb.ToString();
                if (title.IndexOf(keyword, StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    h = hwnd; return false;
                }
                return true;
            }, IntPtr.Zero);

            if (h != IntPtr.Zero)
            {
                try
                {
                    _mainWindowHandle = h;
                    _mainElement = AutomationElement.FromHandle(h);
                }
                catch { _mainElement = null; }
            }
            return h;
        }

        // Find child control by AutomationId under main element
        public AutomationElement FindChildByAutomationId(string automationId)
        {
            if (_mainElement == null) GetMainWindow();
            if (_mainElement == null) return null;
            try
            {
                var cond = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);
                var child = _mainElement.FindFirst(TreeScope.Descendants, cond);
                return child;
            }
            catch { return null; }
        }

        // Get current focused element info for diagnostics
        public Dictionary<string, string> GetFocusedElementInfo()
        {
            var info = new Dictionary<string, string>();
            try
            {
                var focused = AutomationElement.FocusedElement;
                if (focused != null)
                {
                    info["Name"] = focused.Current.Name ?? "N/A";
                    info["ControlType"] = focused.Current.ControlType?.LocalizedControlType ?? "N/A";
                    info["AutomationId"] = focused.Current.AutomationId ?? "N/A";
                    info["ClassName"] = focused.Current.ClassName ?? "N/A";
                }
                else
                {
                    info["Name"] = "No focused element";
                }
            }
            catch (Exception ex)
            {
                info["Error"] = ex.Message;
            }
            return info;
        }

        // Public helper to get main window handle
        public IntPtr GetMainWindowHandle()
        {
            if (_mainWindowHandle == IntPtr.Zero)
                GetMainWindow();
            return _mainWindowHandle;
        }

        // Public helper to get window rectangle (X, Y, Width, Height)
        public bool GetMainWindowRect(out int x, out int y, out int width, out int height)
        {
            x = y = width = height = 0;
            try
            {
                if (_mainWindowHandle == IntPtr.Zero)
                    GetMainWindow();
                if (_mainWindowHandle == IntPtr.Zero)
                    return false;

                if (NativeMethods.GetWindowRect(_mainWindowHandle, out NativeMethods.RECT rect))
                {
                    x = rect.Left;
                    y = rect.Top;
                    width = rect.Right - rect.Left;
                    height = rect.Bottom - rect.Top;
                    return true;
                }
            }
            catch { }
            return false;
        }

        // Sync the main window handle found by AutomationService so GetMainWindow() is not called again.
        // Call this from Form1 immediately after _automation.FindWindowByTitleContains succeeds.
        public void SyncMainWindowHandle(IntPtr h)
        {
            if (h == IntPtr.Zero) return;
            _mainWindowHandle = h;
            // Lazily create AutomationElement — failure is non-fatal
            try { _mainElement = AutomationElement.FromHandle(h); } catch { _mainElement = null; }
        }

        // Focus XML BEFORE Find: allowed to click mouse
        public bool FocusXmlControlFastBeforeFind(int xmlNumber)
        {
            return FocusXmlControlFast(xmlNumber);
        }

        private bool TryMouseClickXmlOffset(int xmlNumber, StringBuilder logSb = null)
        {
            try
            {
                AutomationCancellation.ThrowIfCancellationRequested();
                if (_mainWindowHandle == IntPtr.Zero)
                    GetMainWindow();

                logSb?.AppendLine($"FocusHandleBeforeFallback: 0x{_mainWindowHandle.ToInt64():X}");

                if (_mainWindowHandle == IntPtr.Zero)
                    return false;

                NativeMethods.SetForegroundWindow(_mainWindowHandle);
                Thread.Sleep(80);

                int offsetX, offsetY;
                switch (xmlNumber)
                {
                    case 1:
                        offsetX = _config.Xml1ClickOffsetX;
                        offsetY = _config.Xml1ClickOffsetY;
                        break;
                    case 2:
                        offsetX = _config.Xml2ClickOffsetX;
                        offsetY = _config.Xml2ClickOffsetY;
                        break;
                    case 3:
                        offsetX = _config.Xml3ClickOffsetX;
                        offsetY = _config.Xml3ClickOffsetY;
                        break;
                    case 4:
                        offsetX = _config.Xml4ClickOffsetX;
                        offsetY = _config.Xml4ClickOffsetY;
                        break;
                    default:
                        offsetX = _config.Xml3ClickOffsetX;
                        offsetY = _config.Xml3ClickOffsetY;
                        break;
                }

                if (!NativeMethods.GetWindowRect(_mainWindowHandle, out NativeMethods.RECT rect))
                {
                    logSb?.AppendLine("GetWindowRect failed in TryMouseClickXmlOffset");
                    return false;
                }

                int clickX = rect.Left + offsetX;
                int clickY = rect.Top + offsetY;

                logSb?.AppendLine($"WindowRect: L={rect.Left}, T={rect.Top}, R={rect.Right}, B={rect.Bottom}");
                logSb?.AppendLine($"ClickOffset: X={offsetX}, Y={offsetY}");
                logSb?.AppendLine($"ClickPoint: X={clickX}, Y={clickY}");

                NativeMethods.SetCursorPos(clickX, clickY);
                Thread.Sleep(30);
                NativeMethods.Mouse_Event(NativeMethods.MOUSEEVENTF_LEFTDOWN, (uint)clickX, (uint)clickY, 0, IntPtr.Zero);
                Thread.Sleep(30);
                NativeMethods.Mouse_Event(NativeMethods.MOUSEEVENTF_LEFTUP, (uint)clickX, (uint)clickY, 0, IntPtr.Zero);
                Thread.Sleep(150);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logSb?.AppendLine($"TryMouseClickXmlOffset exception: {ex.Message}");
                return false;
            }
        }

        // Return focus AFTER Find WITHOUT clicking: just send Esc, no mouse click, preserve caret
        public bool ReturnFocusAfterFindWithoutClick(int xmlNumber)
        {
            try
            {
                AutomationCancellation.ThrowIfCancellationRequested();
                // Just send Esc to close Find dialog
                SendKeys.SendWait("{ESC}");
                Thread.Sleep(100);
                return true;
            }
            catch (OperationCanceledException) { throw; }
            catch { return false; }
        }

        // Fast focus XML control: use pure mouse-only OR UI Automation based on config
        public bool FocusXmlControlFast(int xmlNumber)
        {
            var swTotal = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                AutomationCancellation.ThrowIfCancellationRequested();
                // First, ensure main window is active
                if (_mainWindowHandle == IntPtr.Zero)
                    GetMainWindow();
                if (_mainWindowHandle == IntPtr.Zero)
                {
                    try { _mainWindowHandle = NativeMethods.GetForegroundWindow(); } catch { }
                }
                if (_mainWindowHandle == IntPtr.Zero)
                    return false;

                var swActivate = System.Diagnostics.Stopwatch.StartNew();
                NativeMethods.SetForegroundWindow(_mainWindowHandle);
                Thread.Sleep(50);
                swActivate.Stop();

                // If UseFastMouseFocusOnly = true, use pure mouse click (NO UI Automation)
                if (_config.UseFastMouseFocusOnly)
                {
                    var swClick = System.Diagnostics.Stopwatch.StartNew();
                    var clicked = TryMouseClickXmlOffset(xmlNumber);
                    swClick.Stop();
                    swTotal.Stop();

                    // Log timing
                    if (swTotal.ElapsedMilliseconds > 1000)
                    {
                        var log = $"[WARNING] FocusXmlControlFast took {swTotal.ElapsedMilliseconds}ms (ActivateWindow: {swActivate.ElapsedMilliseconds}ms, ClickFocus: {swClick.ElapsedMilliseconds}ms, mode: MouseOnly)";
                        try { SupportEdit.Services.DebugLogService.SaveDebugLog("FocusXmlControlFast_SlowTiming", log); } catch { }
                    }

                    return clicked;
                }

                // Otherwise, use UI Automation (legacy path)
                string automationId = $"xml{xmlNumber}";
                var xmlControl = FindChildByAutomationId(automationId);

                if (xmlControl != null)
                {
                    try
                    {
                        xmlControl.SetFocus();
                        Thread.Sleep(50);

                        var focused = AutomationElement.FocusedElement;
                        if (focused != null && focused.Current.AutomationId == automationId)
                        {
                            swTotal.Stop();
                            return true;
                        }
                    }
                    catch { }

                    // Try clicking center of control
                    try
                    {
                        var clickRect = xmlControl.Current.BoundingRectangle;
                        if (clickRect.Width > 0 && clickRect.Height > 0)
                        {
                            int centerX = (int)(clickRect.Left + clickRect.Width / 2);
                            int centerY = (int)(clickRect.Top + clickRect.Height / 2);
                            NativeMethods.SetCursorPos(centerX, centerY);
                            Thread.Sleep(20);
                            NativeMethods.Mouse_Event(NativeMethods.MOUSEEVENTF_LEFTDOWN, (uint)centerX, (uint)centerY, 0, IntPtr.Zero);
                            Thread.Sleep(20);
                            NativeMethods.Mouse_Event(NativeMethods.MOUSEEVENTF_LEFTUP, (uint)centerX, (uint)centerY, 0, IntPtr.Zero);
                            Thread.Sleep(50);
                            swTotal.Stop();
                            return true;
                        }
                    }
                    catch { }
                }

                // Log timing
                swTotal.Stop();
                if (swTotal.ElapsedMilliseconds > 1000)
                {
                    var log = $"[WARNING] FocusXmlControlFast took {swTotal.ElapsedMilliseconds}ms (ActivateWindow: {swActivate.ElapsedMilliseconds}ms, mode: UiAutomation)";
                    try { SupportEdit.Services.DebugLogService.SaveDebugLog("FocusXmlControlFast_SlowTiming", log); } catch { }
                }

                return false;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                swTotal.Stop();
                return false;
            }
        }

        // Verify focus: check if focused element matches target AutomationId
        private bool VerifyFocus(string automationId)
        {
            try
            {
                var focused = AutomationElement.FocusedElement;
                if (focused != null && focused.Current.AutomationId == automationId)
                    return true;
            }
            catch { }
            return false;
        }

        // Enhanced focus with verification and click fallback
        public bool FocusControlByAutomationId(string automationId)
        {
            var el = FindChildByAutomationId(automationId);
            if (el == null) return false;

            try
            {
                // Try SetFocus first
                el.SetFocus();
                Thread.Sleep(100);

                // Verify focus
                if (VerifyFocus(automationId)) return true;

                // Fallback: try click at control center
                try
                {
                    var rect = el.Current.BoundingRectangle;
                    if (rect.Width > 0 && rect.Height > 0)
                    {
                        int x = (int)(rect.Left + rect.Width / 2);
                        int y = (int)(rect.Top + rect.Height / 2);
                        NativeMethods.SetCursorPos(x, y);
                        NativeMethods.Mouse_Event(0x0002, 0, 0, 0, IntPtr.Zero); // MOUSEEVENTF_LEFTDOWN
                        NativeMethods.Mouse_Event(0x0004, 0, 0, 0, IntPtr.Zero); // MOUSEEVENTF_LEFTUP
                        Thread.Sleep(300);

                        if (VerifyFocus(automationId)) return true;
                    }
                }
                catch { }

                // Last attempt: just SetFocus again
                el.SetFocus();
                Thread.Sleep(100);
                return VerifyFocus(automationId);
            }
            catch { return false; }
        }

        // Try to read text directly from control using UI Automation patterns
        private string ReadTextViaUiAutomation(AutomationElement el)
        {
            if (el == null) return null;
            try
            {
                // Try ValuePattern first (for editable controls)
                try
                {
                    var vp = el.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
                    if (vp != null)
                    {
                        var value = vp.Current.Value;
                        if (!string.IsNullOrEmpty(value)) return value;
                    }
                }
                catch { }

                // Try TextPattern (for rich text controls like FastColoredTextBox)
                try
                {
                    var tp = el.GetCurrentPattern(TextPattern.Pattern) as TextPattern;
                    if (tp != null)
                    {
                        var text = tp.DocumentRange.GetText(-1);
                        if (!string.IsNullOrEmpty(text)) return text;
                    }
                }
                catch { }
            }
            catch { }
            return null;
        }

        public string ReadTextFromControlByAutomationId(string automationId)
        {
            var el = FindChildByAutomationId(automationId);
            if (el == null) return null;
            return ReadTextViaUiAutomation(el);
        }

        public bool SetTextToControlByAutomationId(string automationId, string text)
        {
            var el = FindChildByAutomationId(automationId);
            if (el == null) return false;
            try
            {
                var vp = el.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
                if (vp != null && !vp.Current.IsReadOnly)
                {
                    vp.SetValue(text ?? string.Empty);
                    return true;
                }
            }
            catch { }
            return false;
        }

        public string ReadErrorText()
        {
            return ReadTextFromControlByAutomationId(_config.ErrorControlAutomationId);
        }

        // New helper: find text in currently focused control using Ctrl+F sequence in control
        public bool FindTextInFocusedControl(string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text)) return false;
                CloseFindDialogIfOpen();
                SendKeys.SendWait("^f"); Thread.Sleep(_config.FindDelayMs);
                SendKeys.SendWait("^a"); Thread.Sleep(50);
                try { Clipboard.SetText(text); } catch { }
                SendKeys.SendWait("^v"); Thread.Sleep(50);
                SendKeys.SendWait("{ENTER}"); Thread.Sleep(100);
                CloseFindDialogIfOpen();
                return true;
            }
            catch { return false; }
        }

        private void CloseFindDialogIfOpen()
        {
            try
            {
                AutomationCancellation.ThrowIfCancellationRequested();
                SendKeys.SendWait("{ESC}");
                Thread.Sleep(80);
            }
            catch (OperationCanceledException) { throw; }
            catch { }
        }

        private bool FindTextFreshFast(string text, StringBuilder logSb = null, string label = null)
        {
            try
            {
                AutomationCancellation.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(text)) return false;

                logSb?.AppendLine($"--- FindTextFreshFast: {label ?? text} ---");
                CloseFindDialogIfOpen();
                SendKeys.SendWait("^f");
                Thread.Sleep(180);
                SendKeys.SendWait("^a");
                Thread.Sleep(30);
                try { Clipboard.SetText(text); } catch { return false; }
                Thread.Sleep(_config.FastDirectClipboardDelayMs > 0 ? _config.FastDirectClipboardDelayMs : 20);
                SendKeys.SendWait("^v");
                Thread.Sleep(60);
                SendKeys.SendWait("{ENTER}");
                Thread.Sleep(220);
                CloseFindDialogIfOpen();
                Thread.Sleep(120);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logSb?.AppendLine($"FindTextFreshFast exception: {ex.Message}");
                try { CloseFindDialogIfOpen(); } catch { }
                return false;
            }
        }

        // Deprecated CopyCurrentLineOrSelection kept for compatibility with older DirectFix* methods
        public string CopyCurrentLineOrSelection()
        {
            try
            {
                // try Home, Shift+End, Ctrl+C
                MoveToPhysicalLineStart(50);
                SendKeys.SendWait("+{END}"); Thread.Sleep(50);
                SendKeys.SendWait("^c"); Thread.Sleep(100);
                try { return Clipboard.GetText(); } catch { return null; }
            }
            catch { return null; }
        }

        // New helper: replace current line via keyboard paste (kept for compatibility)
        public bool ReplaceCurrentLine(string newLine)
        {
            try
            {
                if (newLine == null) newLine = string.Empty;
                MoveToPhysicalLineStart(30);
                SendKeys.SendWait("+{END}"); Thread.Sleep(30);
                try { Clipboard.SetText(newLine); } catch { }
                SendKeys.SendWait("^v"); Thread.Sleep(100);
                return true;
            }
            catch { return false; }
        }

        // Focus xml control by number with verification and click fallback
        public bool FocusXmlControl(int xmlNumber)
        {
            var id = _config.XmlControlAutomationIdPrefix + xmlNumber.ToString();
            return FocusControlByAutomationId(id);
        }

        // Move caret to start of the focused XML control (Ctrl+Home)
        public void MoveToStartOfXml()
        {
            try
            {
                SendKeys.SendWait("^{HOME}");
                Thread.Sleep(50);
            }
            catch { }
        }

        // Copy current line (Home, Shift+End, Ctrl+C) with clipboard marker and retry
        public string CopyCurrentLine()
        {
            try
            {
                var marker = "**SUPPORTEDIT_LINE_MARKER_" + Guid.NewGuid().ToString().Substring(0, 8) + "**";
                string original = string.Empty;
                try { original = Clipboard.GetText(); } catch { }

                // move to line start and select to end
                SendKeys.SendWait("{HOME}"); Thread.Sleep(10);
                SendKeys.SendWait("+{END}"); Thread.Sleep(10);

                try { Clipboard.SetText(marker); } catch { }
                Thread.Sleep(5);
                SendKeys.SendWait("^c");

                var attempts = _config.DirectScanClipboardRetryCount > 0 ? _config.DirectScanClipboardRetryCount : 3;
                var delay = _config.DirectScanClipboardRetryDelayMs > 0 ? _config.DirectScanClipboardRetryDelayMs : 50;
                for (int i = 0; i < attempts; i++)
                {
                    Thread.Sleep(delay);
                    try
                    {
                        var clip = Clipboard.GetText();
                        if (!string.IsNullOrEmpty(clip) && clip != marker)
                        {
                            return clip;
                        }
                    }
                    catch { }
                }

                // try one more time
                try { SendKeys.SendWait("^c"); Thread.Sleep(100); return Clipboard.GetText(); } catch { }

                // restore original
                try { if (!string.IsNullOrEmpty(original)) Clipboard.SetText(original); } catch { }
                return null;
            }
            catch { return null; }
        }

        // Move to next line (Down) with configurable delay
        public void MoveNextLine()
        {
            try
            {
                SendKeys.SendWait("{DOWN}");
                Thread.Sleep(_config.DirectScanKeyDelayMs > 0 ? _config.DirectScanKeyDelayMs : 20);
            }
            catch { }
        }

        // Result for ScanToSttLine
        public class ScanPositionResult
        {
            public bool Found { get; set; }
            public int LineIndex { get; set; }
            public string CurrentLine { get; set; }
        }

        // Scan from start of control until a STT line matches targetStt or max lines reached
        // NOTE: retained for legacy DirectIndexScanEdit but DirectFindThenLocalScanEdit will use Find instead
        public ScanPositionResult ScanToSttLine(int xmlNumber, int targetStt)
        {
            var logSb = new StringBuilder();
            logSb.AppendLine($"ACTION: ScanToSttLine");
            logSb.AppendLine($"XML number: {xmlNumber}");
            logSb.AppendLine($"Target STT: {targetStt}");
            logSb.AppendLine($"Start time: {DateTime.Now:O}");
            logSb.AppendLine($"ScanMaxLines: {_config.DirectScanMaxLines}");

            try
            {
                if (!FocusXmlControl(xmlNumber))
                {
                    logSb.AppendLine("FocusXmlControl failed");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_ScanFail", logSb.ToString());
                    return new ScanPositionResult { Found = false };
                }

                MoveToStartOfXml();

                for (int lineIndex = 1; lineIndex <= _config.DirectScanMaxLines; lineIndex++)
                {
                    AutomationCancellation.ThrowIfCancellationRequested();
                    var currentLine = CopyCurrentLine() ?? string.Empty;
                    // log if STT found in this line
                    int parsedStt;
                    if (TryParseSttLine(currentLine, out parsedStt))
                    {
                        logSb.AppendLine($"Line {lineIndex}: STT {parsedStt}");
                        if (parsedStt == targetStt)
                        {
                            logSb.AppendLine($"Found target STT at line {lineIndex}");
                            SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_ScanFound", logSb.ToString());
                            return new ScanPositionResult { Found = true, LineIndex = lineIndex, CurrentLine = currentLine };
                        }
                    }

                    MoveNextLine();
                }

                logSb.AppendLine("Reached max lines without finding STT");
                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_ScanNotFound", logSb.ToString());
                return new ScanPositionResult { Found = false };
            }
            catch (Exception ex)
            {
                logSb.AppendLine("Exception: " + ex.ToString());
                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_ScanException", logSb.ToString());
                return new ScanPositionResult { Found = false };
            }
        }

        // New: Go to STT using Find dialog then close it and verify current line
        public bool GoToSttByFindThenClose(int xmlNumber, int stt)
        {
            var logSb = new StringBuilder();
            logSb.AppendLine($"ACTION: GoToSttByFindThenClose");
            logSb.AppendLine($"XML number: {xmlNumber}");
            logSb.AppendLine($"Target STT: {stt}");
            logSb.AppendLine($"Start time: {DateTime.Now:O}");

            try
            {
                if (!FocusXmlControl(xmlNumber))
                {
                    logSb.AppendLine("FocusXmlControl failed");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{stt}_GoToByFind_Fail", logSb.ToString());
                    return false;
                }

                // open Find
                SendKeys.SendWait("^f"); Thread.Sleep(_config.FindDelayMs);
                // select all in Find
                SendKeys.SendWait("^a"); Thread.Sleep(50);
                try { Clipboard.SetText($"<STT>{stt}</STT>"); } catch { }
                SendKeys.SendWait("^v"); Thread.Sleep(50);
                SendKeys.SendWait("{ENTER}"); Thread.Sleep(200);
                // close Find
                SendKeys.SendWait("{ESC}"); Thread.Sleep(100);

                // refocus control
                FocusXmlControl(xmlNumber);
                SendKeys.SendWait("{HOME}"); Thread.Sleep(30);

                var currentLine = CopyCurrentLine() ?? string.Empty;
                logSb.AppendLine($"Line after Find: {currentLine}");
                var rx = new System.Text.RegularExpressions.Regex($"<STT>\\s*{stt}\\s*</STT>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!rx.IsMatch(currentLine))
                {
                    MessageBox.Show("Find có thể đã tìm thấy STT nhưng không xác minh được dòng hiện tại.", "Verification failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    logSb.AppendLine("Verification failed after Find");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{stt}_GoToByFind_VerifyFail", logSb.ToString());
                    return false;
                }

                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{stt}_GoToByFind_Success", logSb.ToString());
                return true;
            }
            catch (Exception ex)
            {
                logSb.AppendLine("Exception: " + ex.ToString());
                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{stt}_GoToByFind_Exception", logSb.ToString());
                return false;
            }
        }

        // CopyCurrentLine variant for local scans using DirectLocal config
        public string CopyCurrentLineLocal()
        {
            try
            {
                var marker = "**SUPPORTEDIT_LINE_MARKER_" + Guid.NewGuid().ToString().Substring(0, 8) + "**";
                string original = string.Empty;
                try { original = Clipboard.GetText(); } catch { }

                SendKeys.SendWait("{HOME}"); Thread.Sleep(10);
                SendKeys.SendWait("+{END}"); Thread.Sleep(10);

                try { Clipboard.SetText(marker); } catch { }
                Thread.Sleep(5);
                SendKeys.SendWait("^c");

                var attempts = _config.DirectLocalScanClipboardRetryCount > 0 ? _config.DirectLocalScanClipboardRetryCount : 3;
                var delay = _config.DirectLocalScanClipboardRetryDelayMs > 0 ? _config.DirectLocalScanClipboardRetryDelayMs : 80;
                for (int i = 0; i < attempts; i++)
                {
                    Thread.Sleep(delay);
                    try
                    {
                        var clip = Clipboard.GetText();
                        if (!string.IsNullOrEmpty(clip) && clip != marker)
                        {
                            return clip;
                        }
                    }
                    catch { }
                }

                try { SendKeys.SendWait("^c"); Thread.Sleep(100); return Clipboard.GetText(); } catch { }
                try { if (!string.IsNullOrEmpty(original)) Clipboard.SetText(original); } catch { }
                return null;
            }
            catch { return null; }
        }

        // Move to next line using local scan delay
        public void MoveNextLineLocal()
        {
            try
            {
                SendKeys.SendWait("{DOWN}");
                Thread.Sleep(_config.DirectLocalScanKeyDelayMs > 0 ? _config.DirectLocalScanKeyDelayMs : 30);
            }
            catch { }
        }

        // Optimized: Copy current line with early exit (bail if clipboard != marker and not empty)
        public (string content, long elapsedMs) CopyCurrentLineLocalOptimized()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var marker = "**SUPPORTEDIT_LINE_MARKER_" + Guid.NewGuid().ToString().Substring(0, 8) + "**";
                string original = string.Empty;
                try { original = Clipboard.GetText(); } catch { }

                SendKeys.SendWait("{HOME}"); Thread.Sleep(10);
                SendKeys.SendWait("+{END}"); Thread.Sleep(10);

                try { Clipboard.SetText(marker); } catch { }
                Thread.Sleep(5);
                SendKeys.SendWait("^c");
                Thread.Sleep(20);

                try
                {
                    var clip = Clipboard.GetText();
                    if (!string.IsNullOrEmpty(clip) && clip != marker)
                    {
                        sw.Stop();
                        return (clip, sw.ElapsedMilliseconds);
                    }
                }
                catch { }

                // Minimal retry (up to 2 more times)
                var attempts = _config.DirectLocalScanClipboardRetryCount > 0 ? _config.DirectLocalScanClipboardRetryCount : 2;
                for (int i = 1; i < attempts; i++)
                {
                    Thread.Sleep(_config.DirectLocalScanClipboardRetryDelayMs > 0 ? _config.DirectLocalScanClipboardRetryDelayMs : 20);
                    try
                    {
                        var clip = Clipboard.GetText();
                        if (!string.IsNullOrEmpty(clip) && clip != marker)
                        {
                            sw.Stop();
                            return (clip, sw.ElapsedMilliseconds);
                        }
                    }
                    catch { }
                }

                sw.Stop();
                try { if (!string.IsNullOrEmpty(original)) Clipboard.SetText(original); } catch { }
                return (null, sw.ElapsedMilliseconds);
            }
            catch { sw.Stop(); return (null, sw.ElapsedMilliseconds); }
        }

        // ===== FAST MODE PRIMITIVES (no heavy logging, minimal delays) =====

        private void MoveToPhysicalLineStart(int delayMs = 10)
        {
            AutomationCancellation.ThrowIfCancellationRequested();
            // FastColoredTextBox/Home commonly toggles between first non-space char and column 0.
            // Press twice so Shift+End selects the whole physical line, including indentation.
            SendKeys.SendWait("{HOME}");
            Thread.Sleep(delayMs);
            SendKeys.SendWait("{HOME}");
            Thread.Sleep(delayMs);
        }

        // Fast: Move to next line (minimal delay)
        public void MoveNextLineFast()
        {
            try
            {
                AutomationCancellation.ThrowIfCancellationRequested();
                SendKeys.SendWait("{DOWN}");
                Thread.Sleep(_config.FastDirectKeyDelayMs > 0 ? _config.FastDirectKeyDelayMs : 5);
            }
            catch (OperationCanceledException) { throw; }
            catch { }
        }

        // Fast: Copy current line with minimal retry (no per-line logging)
        public string CopyCurrentLineFast()
        {
            try
            {
                AutomationCancellation.ThrowIfCancellationRequested();
                MoveToPhysicalLineStart(10);
                SendKeys.SendWait("+{END}");
                Thread.Sleep(10);

                var marker = "**FM_" + Guid.NewGuid().ToString().Substring(0, 8) + "**";
                try { Clipboard.SetText(marker); } catch { }
                Thread.Sleep(5);
                SendKeys.SendWait("^c");
                Thread.Sleep(_config.FastDirectClipboardDelayMs > 0 ? _config.FastDirectClipboardDelayMs : 20);

                try
                {
                    var clip = Clipboard.GetText();
                    if (!string.IsNullOrEmpty(clip) && clip != marker)
                        return clip;
                }
                catch { }

                // Retry once only
                Thread.Sleep(10);
                try
                {
                    var clip = Clipboard.GetText();
                    if (!string.IsNullOrEmpty(clip) && clip != marker)
                        return clip;
                }
                catch { }

                return null;
            }
            catch (OperationCanceledException) { throw; }
            catch { return null; }
        }

        private string CopyCurrentOrAdjacentLineContaining(string tagName, StringBuilder logSb = null)
        {
            AutomationCancellation.ThrowIfCancellationRequested();
            var needle = "<" + tagName;

            var current = CopyCurrentLineFast() ?? string.Empty;
            logSb?.AppendLine($"Line current for {tagName}: [{current}]");
            if (current.IndexOf(needle, StringComparison.InvariantCultureIgnoreCase) >= 0)
                return current;

            SendKeys.SendWait("{DOWN}");
            Thread.Sleep(40);
            var down = CopyCurrentLineFast() ?? string.Empty;
            logSb?.AppendLine($"Line down for {tagName}: [{down}]");
            if (down.IndexOf(needle, StringComparison.InvariantCultureIgnoreCase) >= 0)
                return down;

            SendKeys.SendWait("{UP}");
            SendKeys.SendWait("{UP}");
            Thread.Sleep(40);
            var up = CopyCurrentLineFast() ?? string.Empty;
            logSb?.AppendLine($"Line up for {tagName}: [{up}]");
            if (up.IndexOf(needle, StringComparison.InvariantCultureIgnoreCase) >= 0)
                return up;

            return string.Empty;
        }

        // Fast: Replace current line (no heavy verification)
        public bool ReplaceCurrentLineFast(string newLine)
        {
            try
            {
                AutomationCancellation.ThrowIfCancellationRequested();
                MoveToPhysicalLineStart(10);
                SendKeys.SendWait("+{END}");
                Thread.Sleep(10);

                try { Clipboard.SetText(newLine); } catch { return false; }
                Thread.Sleep(5);
                SendKeys.SendWait("^v");
                Thread.Sleep(_config.FastDirectClipboardDelayMs > 0 ? _config.FastDirectClipboardDelayMs : 20);
                return true;
            }
            catch (OperationCanceledException) { throw; }
            catch { return false; }
        }

        // ===== END FAST MODE PRIMITIVES =====

        // Fast: Go to STT using Ctrl+F with auto-focus.
        // KEY FIX: Close Find dialog with ESC BEFORE copying/verifying current line.
        // Old code verified while Find was still open → keyboard ops went to Find box, not editor.
        public bool GoToSttFast(int xmlNumber, int stt)
        {
            var result = GoToSttFastAndReport(xmlNumber, stt);
            return result.Success;
        }

        public bool GoToErrorFast(int xmlNumber, int stt, string field)
        {
            try
            {
                if (stt > 0)
                {
                    if (!GoToSttFast(xmlNumber, stt))
                        return false;
                }
                else
                {
                    if (!FocusXmlControlFastBeforeFind(xmlNumber))
                    {
                        var logSb = new StringBuilder();
                        logSb.AppendLine("FocusXmlControlFastBeforeFind returned false; trying direct mouse offset click once.");
                        if (!TryMouseClickXmlOffset(xmlNumber, logSb))
                        {
                            SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_GoToError_FocusFail", logSb.ToString());
                            return false;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(field))
                    return FindTextFreshFast("<" + field, null, field);

                return stt > 0;
            }
            catch
            {
                return false;
            }
        }

        public string FastDirectReadTagValue(int xmlNumber, int targetStt, string tagName)
        {
            var logSb = new StringBuilder();
            logSb.AppendLine("ACTION: FastDirectReadTagValue");
            logSb.AppendLine($"XML: {xmlNumber}, STT: {targetStt}, Tag: {tagName}");

            try
            {
                if (targetStt > 0)
                {
                    if (!GoToSttFast(xmlNumber, targetStt))
                    {
                        logSb.AppendLine("GoToSttFast failed");
                        SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_Read{tagName}_GoToFail", logSb.ToString());
                        return null;
                    }
                }
                else if (!FocusXmlControlFastBeforeFind(xmlNumber) && !TryMouseClickXmlOffset(xmlNumber, logSb))
                {
                    logSb.AppendLine("Focus XML failed");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_Read{tagName}_FocusFail", logSb.ToString());
                    return null;
                }

                if (!FindTextFreshFast("<" + tagName, logSb, tagName))
                {
                    logSb.AppendLine("Find tag failed");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_Read{tagName}_FindFail", logSb.ToString());
                    return null;
                }

                var line = CopyCurrentOrAdjacentLineContaining(tagName, logSb);
                var value = GetTagValueFromLine(line, tagName);
                logSb.AppendLine($"Line: {line}");
                logSb.AppendLine($"Value: {value}");
                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_Read{tagName}_Success", logSb.ToString());
                return value;
            }
            catch (Exception ex)
            {
                logSb.AppendLine("Exception: " + ex);
                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_Read{tagName}_Exception", logSb.ToString());
                return null;
            }
        }

        // Detailed report result for Test GoTo STT Fast button
        public class GoToSttFastReport
        {
            public bool Success { get; set; }
            public string SearchText { get; set; } = string.Empty;
            public string LineAfterFind { get; set; } = string.Empty;
            public string LineAfterDown { get; set; } = string.Empty;
            public string LineAfterUp { get; set; } = string.Empty;
            public long ElapsedMs { get; set; }
            public string FailReason { get; set; } = string.Empty;
            public string LogPath { get; set; } = string.Empty;
        }

        // Full GoTo implementation that returns a detailed report.
        // FLOW: click to focus → ESC (close any existing Find) → ^f → paste → Enter →
        //       ESC (close Find NOW, before any copy) → wait → copy current line → verify
        public GoToSttFastReport GoToSttFastAndReport(int xmlNumber, int stt)
        {
            AutomationCancellation.ThrowIfCancellationRequested();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var logSb = new StringBuilder();
            var report = new GoToSttFastReport();
            string searchText = $"<STT>{stt}</STT>";
            report.SearchText = searchText;

            logSb.AppendLine($"ACTION: GoToSttFastAndReport");
            logSb.AppendLine($"XML number: {xmlNumber}");
            logSb.AppendLine($"Target STT: {stt}");
            logSb.AppendLine($"SearchText: [{searchText}]");
            logSb.AppendLine($"Mode: UseFastMouseFocusOnly={_config.UseFastMouseFocusOnly}, UseUiAutomationFocusForXml={_config.UseUiAutomationFocusForXml}");

            try
            {
                // Step 1: Focus XML control with mouse click BEFORE opening Find
                logSb.AppendLine("--- Step 1: FocusXmlControlFastBeforeFind ---");
                if (!FocusXmlControlFastBeforeFind(xmlNumber))
                {
                    logSb.AppendLine("FocusXmlControlFastBeforeFind returned false; trying direct mouse offset click once.");
                    if (!TryMouseClickXmlOffset(xmlNumber, logSb))
                    {
                        sw.Stop();
                        report.FailReason = "FocusXmlControlFastBeforeFind failed and direct mouse offset fallback failed";
                        logSb.AppendLine($"DidClickBeforeFind: false");
                        logSb.AppendLine($"DidClickAfterFind: false");
                        logSb.AppendLine($"FAIL: {report.FailReason}");
                        logSb.AppendLine($"Elapsed: {sw.ElapsedMilliseconds}ms");
                        report.ElapsedMs = sw.ElapsedMilliseconds;
                        report.LogPath = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{stt}_GoToSttFast_FocusFail", logSb.ToString());
                        return report;
                    }
                }
                logSb.AppendLine($"DidClickBeforeFind: true");
                Thread.Sleep(80);

                // Step 2: Close any pre-existing Find dialog (ESC is harmless if Find is already closed)
                logSb.AppendLine("--- Step 2: Pre-ESC to close any existing Find ---");
                SendKeys.SendWait("{ESC}");
                Thread.Sleep(60);

                // Step 3: Open Find dialog
                logSb.AppendLine("--- Step 3: ^f to open Find ---");
                SendKeys.SendWait("^f");
                Thread.Sleep(180); // wait for Find dialog to appear and focus

                // Step 4: Select all in Find box then paste search text
                logSb.AppendLine("--- Step 4: ^a + paste search text ---");
                SendKeys.SendWait("^a");
                Thread.Sleep(30);
                try { Clipboard.SetText(searchText); } catch { }
                Thread.Sleep(10);
                SendKeys.SendWait("^v");
                Thread.Sleep(60);

                // Step 5: Press Enter to search — Find will navigate editor caret to found text
                logSb.AppendLine("--- Step 5: {ENTER} to search ---");
                SendKeys.SendWait("{ENTER}");
                Thread.Sleep(200); // wait for Find to scroll editor to found position

                // Step 6: CLOSE FIND DIALOG NOW — before any copy/verify operations!
                // This is the critical fix: previously ESC was called AFTER verification,
                // so all copy ops went to Find box instead of editor.
                logSb.AppendLine("--- Step 6: {ESC} to close Find (BEFORE verify) ---");
                SendKeys.SendWait("{ESC}");
                Thread.Sleep(250); // wait for editor to regain keyboard focus
                logSb.AppendLine($"DidClickAfterFind: false (no mouse click after Find)");

                // Step 7: Copy current line from editor — now editor has focus, caret at found position
                logSb.AppendLine("--- Step 7: CopyCurrentLineFast from editor ---");
                var lineAfterFind = CopyCurrentLineFast() ?? string.Empty;
                report.LineAfterFind = lineAfterFind;
                logSb.AppendLine($"LineAfterFind: [{lineAfterFind}]");

                int parsedStt;
                if (TryParseSttLine(lineAfterFind, out parsedStt) && parsedStt == stt)
                {
                    sw.Stop();
                    report.Success = true;
                    logSb.AppendLine($"Verify: SUCCESS (STT {parsedStt} on current line)");
                    logSb.AppendLine($"Elapsed: {sw.ElapsedMilliseconds}ms");
                    report.ElapsedMs = sw.ElapsedMilliseconds;
                    report.LogPath = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{stt}_GoToSttFast_Success", logSb.ToString());
                    return report;
                }

                // Step 8: STT not on current line — try Down 1 line
                logSb.AppendLine("--- Step 8: Not on current line, try Down 1 ---");
                SendKeys.SendWait("{DOWN}");
                Thread.Sleep(40);
                var lineAfterDown = CopyCurrentLineFast() ?? string.Empty;
                report.LineAfterDown = lineAfterDown;
                logSb.AppendLine($"LineAfterDown: [{lineAfterDown}]");

                if (TryParseSttLine(lineAfterDown, out parsedStt) && parsedStt == stt)
                {
                    sw.Stop();
                    report.Success = true;
                    logSb.AppendLine($"Verify: SUCCESS (STT {parsedStt} after Down 1)");
                    logSb.AppendLine($"Elapsed: {sw.ElapsedMilliseconds}ms");
                    report.ElapsedMs = sw.ElapsedMilliseconds;
                    report.LogPath = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{stt}_GoToSttFast_Success", logSb.ToString());
                    return report;
                }

                // Step 9: Try Up 2 lines from Down-1 position (= 1 line above original caret)
                logSb.AppendLine("--- Step 9: Not after Down, try Up 2 ---");
                SendKeys.SendWait("{UP}");
                SendKeys.SendWait("{UP}");
                Thread.Sleep(40);
                var lineAfterUp = CopyCurrentLineFast() ?? string.Empty;
                report.LineAfterUp = lineAfterUp;
                logSb.AppendLine($"LineAfterUp: [{lineAfterUp}]");

                if (TryParseSttLine(lineAfterUp, out parsedStt) && parsedStt == stt)
                {
                    // Caret is 1 line above the STT line — move back down to STT line
                    SendKeys.SendWait("{DOWN}");
                    Thread.Sleep(30);
                    sw.Stop();
                    report.Success = true;
                    logSb.AppendLine($"Verify: SUCCESS (STT {parsedStt} after Up 2; moved Down 1 back to STT)");
                    logSb.AppendLine($"Elapsed: {sw.ElapsedMilliseconds}ms");
                    report.ElapsedMs = sw.ElapsedMilliseconds;
                    report.LogPath = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{stt}_GoToSttFast_Success", logSb.ToString());
                    return report;
                }

                // All checks failed
                sw.Stop();
                report.Success = false;
                report.FailReason = $"STT {stt} not verified on current or adjacent lines after Find+ESC";
                logSb.AppendLine($"Verify: FAILED — {report.FailReason}");
                logSb.AppendLine("Possible causes:");
                logSb.AppendLine("  1. Find dialog did not open (xml control not focused before ^f)");
                logSb.AppendLine("  2. Find could not find the text (STT not in XML, or format mismatch)");
                logSb.AppendLine("  3. Clipboard copy failed (privilege mismatch — run both apps as Admin)");
                logSb.AppendLine("  4. Delays too short for this machine — increase FastDirectClipboardDelayMs");
                logSb.AppendLine($"Elapsed: {sw.ElapsedMilliseconds}ms");
                report.ElapsedMs = sw.ElapsedMilliseconds;
                report.LogPath = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{stt}_GoToSttFast_VerifyFail", logSb.ToString());
                return report;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                report.Success = false;
                report.FailReason = $"Exception: {ex.Message}";
                logSb.AppendLine($"Exception: {ex}");
                logSb.AppendLine($"Elapsed: {sw.ElapsedMilliseconds}ms");
                report.ElapsedMs = sw.ElapsedMilliseconds;
                report.LogPath = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{stt}_GoToSttFast_Exception", logSb.ToString());
                return report;
            }
        }

        // Go to STT using Find dialog then close it and verify current line (with timing)
        public (bool success, Dictionary<string, long> timings) GoToSttByFindThenCloseWithTiming(int xmlNumber, int stt)
        {
            var timings = new Dictionary<string, long>();
            var swTotal = System.Diagnostics.Stopwatch.StartNew();
            var logSb = new StringBuilder();
            logSb.AppendLine($"ACTION: GoToSttByFindThenCloseWithTiming");
            logSb.AppendLine($"XML number: {xmlNumber}");
            logSb.AppendLine($"Target STT: {stt}");

            try
            {
                // FocusXmlControl
                var swFocus = System.Diagnostics.Stopwatch.StartNew();
                if (!FocusXmlControl(xmlNumber))
                {
                    swFocus.Stop();
                    swTotal.Stop();
                    return (false, timings);
                }
                swFocus.Stop();
                timings["Focus"] = swFocus.ElapsedMilliseconds;
                logSb.AppendLine($"[{timings["Focus"]}ms] FocusXmlControl");

                // Find (Ctrl+F search)
                var swFind = System.Diagnostics.Stopwatch.StartNew();
                try { Clipboard.SetText("<STT>" + stt.ToString() + "</STT>"); } catch { }
                SendKeys.SendWait("^f");
                Thread.Sleep(100);
                SendKeys.SendWait("^a");
                Thread.Sleep(50);
                SendKeys.SendWait("^v");
                Thread.Sleep(50);
                SendKeys.SendWait("{ENTER}");
                Thread.Sleep(100);
                swFind.Stop();
                timings["FindSearch"] = swFind.ElapsedMilliseconds;
                logSb.AppendLine($"[{timings["FindSearch"]}ms] Ctrl+F search STT");

                // Close Find + refocus
                var swClose = System.Diagnostics.Stopwatch.StartNew();
                SendKeys.SendWait("{ESC}");
                Thread.Sleep(100);
                FocusXmlControl(xmlNumber);
                SendKeys.SendWait("{HOME}");
                Thread.Sleep(30);
                swClose.Stop();
                timings["CloseFind"] = swClose.ElapsedMilliseconds;
                logSb.AppendLine($"[{timings["CloseFind"]}ms] Close Find + refocus");

                // Verify current line
                var swVerify = System.Diagnostics.Stopwatch.StartNew();
                var (currentLine, copyMs) = CopyCurrentLineLocalOptimized();
                timings["VerifyRead"] = copyMs;
                var rx = new System.Text.RegularExpressions.Regex($"<STT>\\s*{stt}\\s*</STT>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                bool verified = !string.IsNullOrEmpty(currentLine) && rx.IsMatch(currentLine);
                swVerify.Stop();
                timings["Verify"] = swVerify.ElapsedMilliseconds;
                logSb.AppendLine($"[{timings["Verify"]}ms] Verify current line (read {copyMs}ms)");

                if (!verified && !string.IsNullOrEmpty(currentLine))
                {
                    logSb.AppendLine($"Line: {currentLine}");
                    logSb.AppendLine("Not matching target, trying adjacent lines...");

                    // Try Down
                    SendKeys.SendWait("{DOWN}");
                    Thread.Sleep(50);
                    (currentLine, copyMs) = CopyCurrentLineLocalOptimized();
                    if (!string.IsNullOrEmpty(currentLine) && rx.IsMatch(currentLine))
                    {
                        verified = true;
                        logSb.AppendLine("Found STT in line below");
                    }
                    else
                    {
                        // Try Up twice
                        SendKeys.SendWait("{UP}");
                        SendKeys.SendWait("{UP}");
                        Thread.Sleep(50);
                        (currentLine, copyMs) = CopyCurrentLineLocalOptimized();
                        if (!string.IsNullOrEmpty(currentLine) && rx.IsMatch(currentLine))
                        {
                            verified = true;
                            logSb.AppendLine("Found STT in line above");
                        }
                    }
                }

                if (!verified)
                {
                    logSb.AppendLine("Verification failed");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{stt}_GoToByFind_VerifyFail", logSb.ToString());
                    swTotal.Stop();
                    timings["Total"] = swTotal.ElapsedMilliseconds;
                    return (false, timings);
                }

                logSb.AppendLine($"Line verified: {currentLine}");
                swTotal.Stop();
                timings["Total"] = swTotal.ElapsedMilliseconds;
                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{stt}_GoToByFind_Success", logSb.ToString());
                return (true, timings);
            }
            catch (Exception ex)
            {
                logSb.AppendLine("Exception: " + ex.ToString());
                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{stt}_GoToByFind_Exception", logSb.ToString());
                swTotal.Stop();
                timings["Total"] = swTotal.ElapsedMilliseconds;
                return (false, timings);
            }
        }

        // New: Find then local-scan fix for NGAY_TH_YL (with comprehensive timing and timeout)
        public bool DirectFindThenLocalFixNgayThYl(int xmlNumber, int targetStt)
        {
            var swTotal = System.Diagnostics.Stopwatch.StartNew();
            var logSb = new StringBuilder();
            logSb.AppendLine("ACTION: DirectFindThenLocalFixNgayThYlWithTiming");
            logSb.AppendLine($"XML number: {xmlNumber}");
            logSb.AppendLine($"Target STT: {targetStt}");
            logSb.AppendLine($"Start time: {DateTime.Now:O}");
            logSb.AppendLine($"LocalScanMaxLines: {_config.DirectLocalScanMaxLinesWithinBlock}");
            logSb.AppendLine($"Timeout: {_config.DirectFindThenLocalTotalTimeoutMs}ms");

            try
            {
                // Step 1: GoToSttByFindThenClose
                var (findOk, findTimings) = GoToSttByFindThenCloseWithTiming(xmlNumber, targetStt);
                if (!findOk)
                {
                    swTotal.Stop();
                    logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Find failed");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectFind_LocalFixFail", logSb.ToString());
                    MessageBox.Show($"Không thể tới STT {targetStt} bằng Find hoặc xác minh vị trí.", "Find failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                foreach (var kvp in findTimings)
                    logSb.AppendLine($"[Find: {kvp.Key}={kvp.Value}ms]");

                if (swTotal.ElapsedMilliseconds > _config.DirectFindThenLocalTotalTimeoutMs)
                {
                    swTotal.Stop();
                    logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Timeout exceeded after Find step");
                    var logPath = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectFind_Timeout", logSb.ToString());
                    MessageBox.Show($"DirectFindThenLocalScanEdit quá thời gian (Find step).\nLog: {logPath}", "Timeout", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                // Step 2: Local scan for NGAY_YL and NGAY_TH_YL
                string ngayYlValue = null;
                string ngayThYlNew = null;
                int linesScanned = 0;

                for (int i = 0; i < _config.DirectLocalScanMaxLinesWithinBlock; i++)
                {
                    AutomationCancellation.ThrowIfCancellationRequested();
                    if (swTotal.ElapsedMilliseconds > _config.DirectFindThenLocalTotalTimeoutMs)
                    {
                        swTotal.Stop();
                        logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Timeout exceeded during local scan at line {i}");
                        var logPath = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectFind_ScanTimeout", logSb.ToString());
                        MessageBox.Show($"DirectFindThenLocalScanEdit quá thời gian (scan line {i}).\nLog: {logPath}", "Timeout", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }

                    var swScan = System.Diagnostics.Stopwatch.StartNew();
                    MoveNextLineLocal();
                    var (currentLine, copyMs) = CopyCurrentLineLocalOptimized();
                    swScan.Stop();
                    linesScanned++;

                    if (_config.VerboseLineScanLog)
                    {
                        logSb.AppendLine($"[{swScan.ElapsedMilliseconds}ms] ScanLine {i + 1}: {currentLine}");
                    }

                    int parsed;
                    if (TryParseSttLine(currentLine, out parsed))
                    {
                        if (parsed != targetStt)
                        {
                            swTotal.Stop();
                            logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Encountered new STT {parsed} at line {i}, aborting");
                            SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectFind_NewSttFound", logSb.ToString());
                            MessageBox.Show($"Đã gặp STT {parsed} trước khi thấy NGAY_TH_YL. Không sửa để tránh sai.", "Abort", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return false;
                        }
                    }

                    if (currentLine.Contains("</CHI_TIET_DVKT>"))
                    {
                        swTotal.Stop();
                        logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Encountered </CHI_TIET_DVKT> at line {i}, aborting");
                        SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectFind_BlockEnd", logSb.ToString());
                        MessageBox.Show("Đã hết block CHI_TIET_DVKT trước khi thấy NGAY_TH_YL.", "Abort", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }

                    var rxNgayYl = new System.Text.RegularExpressions.Regex("<NGAY_YL>\\s*(?<value>\\d{12})\\s*</NGAY_YL>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    var mNy = rxNgayYl.Match(currentLine);
                    if (mNy.Success)
                    {
                        ngayYlValue = mNy.Groups["value"].Value;
                        ngayThYlNew = AddMinutesToNgayYl(ngayYlValue, 10);
                        logSb.AppendLine($"[Line {i + 1}] Found NGAY_YL: {ngayYlValue}, new NGAY_TH_YL: {ngayThYlNew}");
                        continue;
                    }

                    if (currentLine.IndexOf("<NGAY_TH_YL", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        logSb.AppendLine($"[Line {i + 1}] Found NGAY_TH_YL tag");

                        if (string.IsNullOrEmpty(ngayYlValue))
                        {
                            swTotal.Stop();
                            logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] NGAY_TH_YL found but no NGAY_YL value, aborting");
                            SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectFind_NoNgayYl", logSb.ToString());
                            MessageBox.Show("Chưa tìm thấy NGAY_YL trước NGAY_TH_YL.", "Abort", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return false;
                        }

                        bool isEmpty = IsEmptyTagLine(currentLine, "NGAY_TH_YL");
                        var existing = GetTagValueFromLine(currentLine, "NGAY_TH_YL");
                        if (!string.IsNullOrEmpty(existing) && !isEmpty)
                        {
                            var ans = MessageBox.Show($"NGAY_TH_YL đã có giá trị: {existing}. Bạn có muốn ghi đè không?", "Xác nhận ghi đè", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            if (ans != DialogResult.Yes)
                            {
                                swTotal.Stop();
                                logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] User declined overwrite");
                                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectFind_UserSkipped", logSb.ToString());
                                return false;
                            }
                        }

                        var swReplace = System.Diagnostics.Stopwatch.StartNew();
                        var newLine = BuildTagLinePreserveIndent(currentLine, "NGAY_TH_YL", ngayThYlNew);
                        var previewMsg = $"STT: {targetStt}\nField = NGAY_TH_YL\nOld line:\n{currentLine}\n\nNew line:\n{newLine}";
                        var conf = MessageBox.Show(previewMsg, "Preview Direct Edit", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                        if (conf != DialogResult.OK)
                        {
                            swTotal.Stop();
                            logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] User cancelled preview");
                            SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectFind_UserCancelled", logSb.ToString());
                            return false;
                        }

                        if (!ReplaceCurrentLine(newLine))
                        {
                            swTotal.Stop();
                            logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] ReplaceCurrentLine failed");
                            SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectFind_ReplaceFailed", logSb.ToString());
                            MessageBox.Show("Không thể thay thế dòng hiện tại bằng nội dung mới.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return false;
                        }
                        swReplace.Stop();
                        logSb.AppendLine($"[{swReplace.ElapsedMilliseconds}ms] ReplaceCurrentLine");

                        var swVerifyAfter = System.Diagnostics.Stopwatch.StartNew();
                        var verifyLine = CopyCurrentLineLocal() ?? string.Empty;
                        swVerifyAfter.Stop();
                        var verified = verifyLine.Trim() == newLine.Trim() || verifyLine.Contains(ngayThYlNew);
                        logSb.AppendLine($"[{swVerifyAfter.ElapsedMilliseconds}ms] Verify after replace");
                        logSb.AppendLine($"OldLine: {currentLine}");
                        logSb.AppendLine($"NewLine: {newLine}");
                        logSb.AppendLine($"VerifyResult: {verified}");

                        swTotal.Stop();
                        logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] DirectFindThenLocalFixNgayThYl completed");
                        logSb.AppendLine($"Lines scanned: {linesScanned}");
                        SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectFind_Success", logSb.ToString());

                        if (!verified)
                        {
                            MessageBox.Show("Dòng sau khi paste không khớp với nội dung mong đợi.", "Verification failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }

                        return true;
                    }
                }

                swTotal.Stop();
                logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Not found within {_config.DirectLocalScanMaxLinesWithinBlock} lines");
                logSb.AppendLine($"Lines scanned: {linesScanned}");
                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectFind_NotFound", logSb.ToString());
                MessageBox.Show($"Không tìm thấy NGAY_TH_YL trong block STT {targetStt} (scanned {linesScanned} lines).", "Not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                swTotal.Stop();
                logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Exception: {ex.ToString()}");
                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectFind_Exception", logSb.ToString());
                MessageBox.Show("Exception during DirectFindThenLocalFixNgayThYl: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        // ===== FAST DIRECT EDIT METHODS (no per-line logging, minimal delays, 15s timeout) =====

        // Fast Direct Fix for NGAY_TH_YL
        public bool FastDirectFixNgayThYl(int xmlNumber, int targetStt)
        {
            var swTotal = System.Diagnostics.Stopwatch.StartNew();
            var logSb = new StringBuilder();
            logSb.AppendLine("ACTION: FastDirectFixNgayThYl");
            logSb.AppendLine($"XML: {xmlNumber}, STT: {targetStt}");
            logSb.AppendLine($"Mode: Fast Direct Edit (no per-line logging)");
            logSb.AppendLine($"Timeout: {_config.FastDirectTotalTimeoutMs}ms");

            try
            {
                // Step 1: Go to STT using Ctrl+F with auto-focus
                if (!GoToSttFast(xmlNumber, targetStt))
                {
                    swTotal.Stop();
                    logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] GoToSttFast failed");
                    MessageBox.Show($"Cannot go to STT {targetStt} using Find.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                if (swTotal.ElapsedMilliseconds > _config.FastDirectTotalTimeoutMs)
                {
                    swTotal.Stop();
                    logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Timeout after GoToSttFast");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirect_Timeout", logSb.ToString());
                    MessageBox.Show($"Fast Direct Edit timeout. Log: DebugReads", "Timeout", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                // Step 2: Verify current line contains STT (light verification)
                var currentLine = CopyCurrentLineFast() ?? string.Empty;
                var sttRegex = new System.Text.RegularExpressions.Regex($"<STT>\\s*{targetStt}\\s*</STT>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (!sttRegex.IsMatch(currentLine))
                {
                    // Try adjacent lines
                    SendKeys.SendWait("{DOWN}");
                    Thread.Sleep(20);
                    var nextLine = CopyCurrentLineFast() ?? string.Empty;
                    if (sttRegex.IsMatch(nextLine))
                    {
                        currentLine = nextLine;
                    }
                    else
                    {
                        SendKeys.SendWait("{UP}");
                        SendKeys.SendWait("{UP}");
                        Thread.Sleep(20);
                        var prevLine = CopyCurrentLineFast() ?? string.Empty;
                        if (sttRegex.IsMatch(prevLine))
                        {
                            currentLine = prevLine;
                        }
                        else
                        {
                            logSb.AppendLine("STT verification failed on current/adjacent lines; continuing without confirmation.");
                        }
                    }
                }

                // Step 3: Find NGAY_YL from the current STT position using a fresh Ctrl+F.
                if (!FindTextFreshFast("<NGAY_YL>", logSb, "NGAY_YL"))
                {
                    swTotal.Stop();
                    logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Ctrl+F NGAY_YL failed");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirect_NoNgayYl", logSb.ToString());
                    MessageBox.Show("Không tìm thấy NGAY_YL bằng Ctrl+F.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                var ngayYlLine = CopyCurrentOrAdjacentLineContaining("NGAY_YL", logSb);
                var rxNgayYl = new System.Text.RegularExpressions.Regex("<NGAY_YL>\\s*(?<value>\\d{12})\\s*</NGAY_YL>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var mNy = rxNgayYl.Match(ngayYlLine);
                if (!mNy.Success)
                {
                    swTotal.Stop();
                    logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] NGAY_YL line not verified");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirect_NoNgayYl", logSb.ToString());
                    MessageBox.Show("Không đọc được giá trị NGAY_YL sau khi Ctrl+F.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                var ngayYlValue = mNy.Groups["value"].Value;
                var ngayThYlNew = AddMinutesToNgayYl(ngayYlValue, 10);
                logSb.AppendLine($"Found NGAY_YL: {ngayYlValue}; New NGAY_TH_YL: {ngayThYlNew}");

                // Step 4: Find NGAY_TH_YL using a fresh Ctrl+F, then replace only that current line.
                if (!FindTextFreshFast("<NGAY_TH_YL", logSb, "NGAY_TH_YL"))
                {
                    swTotal.Stop();
                    logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Ctrl+F NGAY_TH_YL failed");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirect_NotFound", logSb.ToString());
                    MessageBox.Show("Không tìm thấy NGAY_TH_YL bằng Ctrl+F.", "Not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                var line = CopyCurrentOrAdjacentLineContaining("NGAY_TH_YL", logSb);
                if (string.IsNullOrWhiteSpace(line))
                {
                    swTotal.Stop();
                    logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] NGAY_TH_YL line not verified");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirect_NotFound", logSb.ToString());
                    MessageBox.Show("Không xác minh được dòng NGAY_TH_YL sau khi Ctrl+F.", "Not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                bool isEmpty = IsEmptyTagLine(line, "NGAY_TH_YL");
                var existing = GetTagValueFromLine(line, "NGAY_TH_YL");

                if (!string.IsNullOrEmpty(existing) && !isEmpty)
                    logSb.AppendLine($"NGAY_TH_YL existing value will be overwritten: {existing}");

                var newLine = BuildTagLinePreserveIndent(line, "NGAY_TH_YL", ngayThYlNew);

                if (!ReplaceCurrentLineFast(newLine))
                {
                    swTotal.Stop();
                    logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Replace failed");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirect_ReplaceFail", logSb.ToString());
                    MessageBox.Show("Replace failed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                var verifyLine = CopyCurrentLineFast() ?? string.Empty;
                var verified = verifyLine.Contains(ngayThYlNew);

                swTotal.Stop();
                logSb.AppendLine($"OldLine: {line}");
                logSb.AppendLine($"NewLine: {newLine}");
                logSb.AppendLine($"Verified: {verified}");
                logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms]");
                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirect_Success", logSb.ToString());

                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                swTotal.Stop();
                logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Exception: {ex}");
                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirect_Exception", logSb.ToString());
                return false;
            }
        }

        // Fast Direct Fix for NGUOI_THUC_HIEN
        public bool FastDirectFixNguoiThucHien(int xmlNumber, int targetStt, string practitionerCode)
        {
            var swTotal = System.Diagnostics.Stopwatch.StartNew();
            var logSb = new StringBuilder();
            logSb.AppendLine("ACTION: FastDirectFixNguoiThucHien");
            logSb.AppendLine($"XML: {xmlNumber}, STT: {targetStt}, Code: {practitionerCode}");
            logSb.AppendLine($"Mode: Fast Direct Edit (no per-line logging)");
            logSb.AppendLine($"Timeout: {_config.FastDirectTotalTimeoutMs}ms");

            try
            {
                // Step 1: Go to STT using Ctrl+F with auto-focus
                if (!GoToSttFast(xmlNumber, targetStt))
                {
                    swTotal.Stop();
                    logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] GoToSttFast failed");
                    MessageBox.Show($"Cannot go to STT {targetStt} using Find.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                if (swTotal.ElapsedMilliseconds > _config.FastDirectTotalTimeoutMs)
                {
                    swTotal.Stop();
                    logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Timeout after GoToSttFast");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirectNguoi_Timeout", logSb.ToString());
                    MessageBox.Show($"Fast Direct Edit timeout. Log: DebugReads", "Timeout", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                // Step 2: Verify current line contains STT (light verification)
                var currentLine = CopyCurrentLineFast() ?? string.Empty;
                var sttRegex = new System.Text.RegularExpressions.Regex($"<STT>\\s*{targetStt}\\s*</STT>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (!sttRegex.IsMatch(currentLine))
                {
                    // Try adjacent lines
                    SendKeys.SendWait("{DOWN}");
                    Thread.Sleep(20);
                    var nextLine = CopyCurrentLineFast() ?? string.Empty;
                    if (sttRegex.IsMatch(nextLine))
                    {
                        currentLine = nextLine;
                    }
                    else
                    {
                        SendKeys.SendWait("{UP}");
                        SendKeys.SendWait("{UP}");
                        Thread.Sleep(20);
                        var prevLine = CopyCurrentLineFast() ?? string.Empty;
                        if (sttRegex.IsMatch(prevLine))
                        {
                            currentLine = prevLine;
                        }
                        else
                        {
                            logSb.AppendLine("STT verification failed on current/adjacent lines; continuing without confirmation.");
                        }
                    }
                }

                // Step 3: Find NGUOI_THUC_HIEN from the current STT position using a fresh Ctrl+F.
                if (!FindTextFreshFast("<NGUOI_THUC_HIEN", logSb, "NGUOI_THUC_HIEN"))
                {
                    swTotal.Stop();
                    logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Ctrl+F NGUOI_THUC_HIEN failed");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirectNguoi_NotFound", logSb.ToString());
                    MessageBox.Show("Không tìm thấy NGUOI_THUC_HIEN bằng Ctrl+F.", "Not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                var line = CopyCurrentOrAdjacentLineContaining("NGUOI_THUC_HIEN", logSb);
                if (string.IsNullOrWhiteSpace(line))
                {
                    swTotal.Stop();
                    logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] NGUOI_THUC_HIEN line not verified");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirectNguoi_NotFound", logSb.ToString());
                    MessageBox.Show("Không xác minh được dòng NGUOI_THUC_HIEN sau khi Ctrl+F.", "Not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                bool isEmpty = IsEmptyTagLine(line, "NGUOI_THUC_HIEN");
                var existing = GetTagValueFromLine(line, "NGUOI_THUC_HIEN");

                if (!string.IsNullOrEmpty(existing) && !isEmpty)
                    logSb.AppendLine($"NGUOI_THUC_HIEN existing value will be overwritten: {existing}");

                var newLine = BuildTagLinePreserveIndent(line, "NGUOI_THUC_HIEN", practitionerCode);

                if (!ReplaceCurrentLineFast(newLine))
                {
                    swTotal.Stop();
                    logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Replace failed");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirectNguoi_ReplaceFail", logSb.ToString());
                    MessageBox.Show("Replace failed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                var verifyLine = CopyCurrentLineFast() ?? string.Empty;
                var verified = verifyLine.Contains(practitionerCode);

                swTotal.Stop();
                logSb.AppendLine($"OldLine: {line}");
                logSb.AppendLine($"NewLine: {newLine}");
                logSb.AppendLine($"Verified: {verified}");
                logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms]");
                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirectNguoi_Success", logSb.ToString());

                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                swTotal.Stop();
                logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Exception: {ex}");
                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirectNguoi_Exception", logSb.ToString());
                return false;
            }
        }

        // Fast Direct Fix for T_BNCCT: find the tag and set value to 0.
        // targetStt = 0 means the error line has no STT, so only focus XML then Ctrl+F T_BNCCT.
        public bool FastDirectFixTBncct(int xmlNumber, int targetStt)
        {
            return FastDirectFixNumericTag(xmlNumber, targetStt, "T_BNCCT", "0");
        }

        public bool FastDirectFixMucHuong(int xmlNumber, int targetStt)
        {
            return FastDirectFixNumericTag(xmlNumber, targetStt, "MUC_HUONG", "100");
        }

        public bool FastDirectClearSoCccd(int xmlNumber, int targetStt)
        {
            return FastDirectFixNumericTag(xmlNumber, targetStt, "SO_CCCD", string.Empty, true);
        }

        public bool FastDirectClearMaBenhKt(int xmlNumber)
        {
            return FastDirectFixNumericTag(xmlNumber, 0, "MA_BENH_KT", string.Empty, false, true);
        }

        public bool FastDirectFixNgayTToan(int xmlNumber, int targetStt)
        {
            var swTotal = System.Diagnostics.Stopwatch.StartNew();
            var logSb = new StringBuilder();
            logSb.AppendLine("ACTION: FastDirectFixNgayTToan");
            logSb.AppendLine($"XML: {xmlNumber}, STT: {targetStt}");
            logSb.AppendLine($"Timeout: {_config.FastDirectTotalTimeoutMs}ms");

            try
            {
                if (targetStt > 0)
                {
                    if (!GoToSttFast(xmlNumber, targetStt))
                    {
                        swTotal.Stop();
                        logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] GoToSttFast failed");
                        SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirectNgayTToan_GoToFail", logSb.ToString());
                        MessageBox.Show($"Cannot go to STT {targetStt} using Find.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }
                else
                {
                    if (!FocusXmlControlFastBeforeFind(xmlNumber))
                    {
                        logSb.AppendLine("FocusXmlControlFastBeforeFind returned false; trying direct mouse offset click once.");
                        if (!TryMouseClickXmlOffset(xmlNumber, logSb))
                        {
                            swTotal.Stop();
                            logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Focus XML failed");
                            SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_FastDirectNgayTToan_FocusFail", logSb.ToString());
                            MessageBox.Show($"Không focus được XML{xmlNumber}.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return false;
                        }
                    }
                }

                if (!FindTextFreshFast("<NGAY_RA>", logSb, "NGAY_RA"))
                {
                    swTotal.Stop();
                    logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Ctrl+F NGAY_RA failed");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirectNgayTToan_NoNgayRa", logSb.ToString());
                    MessageBox.Show("Không tìm thấy NGAY_RA bằng Ctrl+F.", "Not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                var ngayRaLine = CopyCurrentOrAdjacentLineContaining("NGAY_RA", logSb);
                var rxNgayRa = new System.Text.RegularExpressions.Regex("<NGAY_RA>\\s*(?<value>\\d{12})\\s*</NGAY_RA>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var mNgayRa = rxNgayRa.Match(ngayRaLine);
                if (!mNgayRa.Success)
                {
                    swTotal.Stop();
                    logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] NGAY_RA line not verified");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirectNgayTToan_NoNgayRa", logSb.ToString());
                    MessageBox.Show("Không đọc được giá trị NGAY_RA sau khi Ctrl+F.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                var ngayRaValue = mNgayRa.Groups["value"].Value;
                var ngayTToanNew = AddMinutesToNgayYl(ngayRaValue, 60);
                if (string.IsNullOrEmpty(ngayTToanNew))
                {
                    swTotal.Stop();
                    logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Cannot compute NGAY_TTOAN from NGAY_RA: {ngayRaValue}");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirectNgayTToan_ComputeFail", logSb.ToString());
                    MessageBox.Show("Không thể tính NGAY_TTOAN từ NGAY_RA.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                logSb.AppendLine($"Found NGAY_RA: {ngayRaValue}; New NGAY_TTOAN: {ngayTToanNew}");

                if (!FindTextFreshFast("<NGAY_TTOAN", logSb, "NGAY_TTOAN"))
                {
                    swTotal.Stop();
                    logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Ctrl+F NGAY_TTOAN failed");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirectNgayTToan_NotFound", logSb.ToString());
                    MessageBox.Show("Không tìm thấy NGAY_TTOAN bằng Ctrl+F.", "Not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                var line = CopyCurrentOrAdjacentLineContaining("NGAY_TTOAN", logSb);
                if (string.IsNullOrWhiteSpace(line))
                {
                    swTotal.Stop();
                    logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] NGAY_TTOAN line not verified");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirectNgayTToan_NotFound", logSb.ToString());
                    MessageBox.Show("Không xác minh được dòng NGAY_TTOAN sau khi Ctrl+F.", "Not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                var newLine = BuildTagLinePreserveIndent(line, "NGAY_TTOAN", ngayTToanNew);
                if (!ReplaceCurrentLineFast(newLine))
                {
                    swTotal.Stop();
                    logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Replace failed");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirectNgayTToan_ReplaceFail", logSb.ToString());
                    MessageBox.Show("Replace failed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                var verifyLine = CopyCurrentLineFast() ?? string.Empty;
                var verified = verifyLine.Contains(ngayTToanNew);

                swTotal.Stop();
                logSb.AppendLine($"OldLine: {line}");
                logSb.AppendLine($"NewLine: {newLine}");
                logSb.AppendLine($"Verified: {verified}");
                logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms]");
                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirectNgayTToan_Success", logSb.ToString());

                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                swTotal.Stop();
                logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Exception: {ex}");
                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirectNgayTToan_Exception", logSb.ToString());
                return false;
            }
        }

        private bool FastDirectFixNumericTag(
            int xmlNumber,
            int targetStt,
            string tagName,
            string newValue,
            bool clearNumericValue = false,
            bool clearAnyValue = false)
        {
            var swTotal = System.Diagnostics.Stopwatch.StartNew();
            var logSb = new StringBuilder();
            logSb.AppendLine($"ACTION: FastDirectFix{tagName}");
            logSb.AppendLine($"XML: {xmlNumber}, STT: {targetStt}");
            logSb.AppendLine($"Timeout: {_config.FastDirectTotalTimeoutMs}ms");

            try
            {
                if (targetStt > 0)
                {
                    if (!GoToSttFast(xmlNumber, targetStt))
                    {
                        swTotal.Stop();
                        logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] GoToSttFast failed");
                        SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirect{tagName}_GoToFail", logSb.ToString());
                        MessageBox.Show($"Cannot go to STT {targetStt} using Find.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }
                else
                {
                    if (!FocusXmlControlFastBeforeFind(xmlNumber))
                    {
                        logSb.AppendLine("FocusXmlControlFastBeforeFind returned false; trying direct mouse offset click once.");
                        if (!TryMouseClickXmlOffset(xmlNumber, logSb))
                        {
                            swTotal.Stop();
                            logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Focus XML failed");
                            SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_FastDirect{tagName}_FocusFail", logSb.ToString());
                            MessageBox.Show($"Không focus được XML{xmlNumber}.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return false;
                        }
                    }
                }

                if (swTotal.ElapsedMilliseconds > _config.FastDirectTotalTimeoutMs)
                {
                    swTotal.Stop();
                    logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Timeout before find {tagName}");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirect{tagName}_Timeout", logSb.ToString());
                    MessageBox.Show($"Fast Direct Edit timeout. Log: DebugReads", "Timeout", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                if (!FindTextFreshFast("<" + tagName, logSb, tagName))
                {
                    swTotal.Stop();
                    logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Ctrl+F {tagName} failed");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirect{tagName}_NotFound", logSb.ToString());
                    MessageBox.Show($"Không tìm thấy {tagName} bằng Ctrl+F.", "Not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                var line = CopyCurrentOrAdjacentLineContaining(tagName, logSb);
                if (string.IsNullOrWhiteSpace(line))
                {
                    swTotal.Stop();
                    logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] {tagName} line not verified");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirect{tagName}_NotFound", logSb.ToString());
                    MessageBox.Show($"Không xác minh được dòng {tagName} sau khi Ctrl+F.", "Not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                string newLine;
                if (clearNumericValue || clearAnyValue)
                {
                    var valuePattern = clearAnyValue ? "[^<]*" : "\\s*[0-9]+\\s*";
                    var clearTagRegex = new System.Text.RegularExpressions.Regex(
                        $"<{System.Text.RegularExpressions.Regex.Escape(tagName)}>{valuePattern}</{System.Text.RegularExpressions.Regex.Escape(tagName)}>",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (!clearTagRegex.IsMatch(line))
                    {
                        swTotal.Stop();
                        logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] {tagName} does not contain a supported value");
                        SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirect{tagName}_InvalidValue", logSb.ToString());
                        var requirement = clearAnyValue ? "nội dung hợp lệ" : "giá trị toàn chữ số";
                        MessageBox.Show($"{tagName} không chứa {requirement} nên không tự động xóa.", "Giá trị không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }

                    newLine = clearTagRegex.Replace(line, $"<{tagName}/>", 1);
                }
                else
                {
                    newLine = BuildTagLinePreserveIndent(line, tagName, newValue);
                }
                if (!ReplaceCurrentLineFast(newLine))
                {
                    swTotal.Stop();
                    logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Replace failed");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirect{tagName}_ReplaceFail", logSb.ToString());
                    MessageBox.Show("Replace failed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                var verifyLine = CopyCurrentLineFast() ?? string.Empty;
                var expectedTag = clearNumericValue || clearAnyValue
                    ? $"<{tagName}/>"
                    : $"<{tagName}>{newValue}</{tagName}>";
                var verified = verifyLine.IndexOf(expectedTag, StringComparison.InvariantCultureIgnoreCase) >= 0;

                swTotal.Stop();
                logSb.AppendLine($"OldLine: {line}");
                logSb.AppendLine($"NewLine: {newLine}");
                logSb.AppendLine($"Verified: {verified}");
                logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms]");
                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirect{tagName}_Success", logSb.ToString());

                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                swTotal.Stop();
                logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Exception: {ex}");
                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_FastDirect{tagName}_Exception", logSb.ToString());
                return false;
            }
        }

        // ===== END FAST DIRECT EDIT METHODS =====

        public bool DirectIndexFixNgayThYl(int xmlNumber, int targetStt)
        {
            var logSb = new StringBuilder();
            logSb.AppendLine("ACTION: DirectIndexFixNgayThYl");
            logSb.AppendLine($"XML number: {xmlNumber}");
            logSb.AppendLine($"Target STT: {targetStt}");
            logSb.AppendLine($"Start time: {DateTime.Now:O}");
            logSb.AppendLine($"ScanMaxLines: {_config.DirectScanMaxLines}");

            try
            {
                var scanRes = ScanToSttLine(xmlNumber, targetStt);
                if (!scanRes.Found)
                {
                    MessageBox.Show($"Không tìm thấy STT {targetStt} trong XML{xmlNumber} (scanned up to {_config.DirectScanMaxLines} lines).", "Scan not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                // after finding STT, continue scanning downward within same block
                string ngayYlValue = null;
                for (int i = 0; i < _config.DirectScanMaxLines; i++)
                {
                    AutomationCancellation.ThrowIfCancellationRequested();
                    MoveNextLine();
                    var currentLine = CopyCurrentLine() ?? string.Empty;

                    // check new STT
                    int parsed; if (TryParseSttLine(currentLine, out parsed))
                    {
                        if (parsed != targetStt)
                        {
                            MessageBox.Show($"Đã gặp STT {parsed} trước khi thấy NGAY_TH_YL. Không sửa để tránh sai.", "Abort", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            logSb.AppendLine($"Encountered new STT {parsed} at scan, aborting");
                            SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectIndexFixNgayThYl_Abort", logSb.ToString());
                            return false;
                        }
                    }

                    // check end of block
                    if (currentLine.Contains("</CHI_TIET_DVKT>"))
                    {
                        MessageBox.Show("Đã gặp </CHI_TIET_DVKT> trước khi thấy NGAY_TH_YL. Không sửa.", "Abort", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        logSb.AppendLine("Encountered </CHI_TIET_DVKT> before NGAY_TH_YL, aborting");
                        SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectIndexFixNgayThYl_Abort", logSb.ToString());
                        return false;
                    }

                    // find NGAY_YL value
                    var rxNgayYl = new System.Text.RegularExpressions.Regex("<NGAY_YL>\\s*(?<value>\\d{12})\\s*</NGAY_YL>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    var mNy = rxNgayYl.Match(currentLine);
                    if (mNy.Success)
                    {
                        ngayYlValue = mNy.Groups["value"].Value;
                        logSb.AppendLine($"Found NGAY_YL at line: {ngayYlValue}");
                    }

                    // detect NGAY_TH_YL tag
                    if (currentLine.IndexOf("<NGAY_TH_YL", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        // ensure we have NGAY_YL value
                        if (string.IsNullOrEmpty(ngayYlValue))
                        {
                            MessageBox.Show("Chưa tìm thấy NGAY_YL trước NGAY_TH_YL. Không sửa.", "Abort", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            logSb.AppendLine("NGAY_TH_YL encountered before NGAY_YL, aborting");
                            SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectIndexFixNgayThYl_Abort", logSb.ToString());
                            return false;
                        }

                        // Determine if current line is empty tag or has existing value
                        bool isEmpty = IsEmptyTagLine(currentLine, "NGAY_TH_YL");
                        var existing = GetTagValueFromLine(currentLine, "NGAY_TH_YL");

                        if (!string.IsNullOrEmpty(existing) && !isEmpty)
                        {
                            var ans = MessageBox.Show($"NGAY_TH_YL đã có giá trị: {existing}. Bạn có muốn ghi đè không?", "Xác nhận ghi đè", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            if (ans != DialogResult.Yes)
                            {
                                logSb.AppendLine("User declined overwrite for NGAY_TH_YL");
                                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectIndexFixNgayThYl_Skipped", logSb.ToString());
                                return false;
                            }
                        }

                        // compute new ngay
                        var newNgay = AddMinutesToNgayYl(ngayYlValue, 10);
                        if (newNgay == null)
                        {
                            MessageBox.Show("Không thể tính NGAY_TH_YL mới từ NGAY_YL.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            logSb.AppendLine("Failed to compute new NGAY_TH_YL");
                            SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectIndexFixNgayThYl_Error", logSb.ToString());
                            return false;
                        }

                        var newLine = BuildTagLinePreserveIndent(currentLine, "NGAY_TH_YL", newNgay);

                        // preview
                        var previewMsg = $"STT: {targetStt}\nField = NGAY_TH_YL\nOld line:\n{currentLine}\n\nNew line:\n{newLine}";
                        var conf = MessageBox.Show(previewMsg, "Preview Direct Edit", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                        if (conf != DialogResult.OK)
                        {
                            logSb.AppendLine("User cancelled preview");
                            SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectIndexFixNgayThYl_Cancelled", logSb.ToString());
                            return false;
                        }

                        // replace
                        if (!ReplaceCurrentLine(newLine))
                        {
                            MessageBox.Show("Không thể thay thế dòng hiện tại bằng nội dung mới.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            logSb.AppendLine("ReplaceCurrentLine failed");
                            SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectIndexFixNgayThYl_Error", logSb.ToString());
                            return false;
                        }

                        // verify
                        var verifyLine = CopyCurrentLine() ?? string.Empty;
                        var verified = verifyLine.Trim() == newLine.Trim();

                        logSb.AppendLine($"OldLine: {currentLine}");
                        logSb.AppendLine($"NewLine: {newLine}");
                        logSb.AppendLine($"VerifyResult: {verified}");
                        SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectIndexFixNgayThYl_Result", logSb.ToString());

                        if (!verified)
                        {
                            MessageBox.Show("Dòng sau khi paste không khớp với nội dung mong đợi.", "Verification failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }

                        return true;
                    }
                }

                MessageBox.Show($"Không tìm thấy NGAY_TH_YL trong KHỐI STT {targetStt}.", "Not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectIndexFixNgayThYl_NotFound", logSb.ToString());
                return false;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logSb.AppendLine("Exception: " + ex.ToString());
                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectIndexFixNgayThYl_Exception", logSb.ToString());
                MessageBox.Show("Exception during DirectIndexFixNgayThYl: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        // New: Find then local-scan fix for NGUOI_THUC_HIEN (with comprehensive timing and timeout)
        public bool DirectFindThenLocalFixNguoiThucHien(int xmlNumber, int targetStt, string practitionerCode)
        {
            var swTotal = System.Diagnostics.Stopwatch.StartNew();
            var logSb = new StringBuilder();
            logSb.AppendLine("ACTION: DirectFindThenLocalFixNguoiThucHienWithTiming");
            logSb.AppendLine($"XML number: {xmlNumber}");
            logSb.AppendLine($"Target STT: {targetStt}");
            logSb.AppendLine($"PractitionerCode: {practitionerCode}");
            logSb.AppendLine($"Start time: {DateTime.Now:O}");
            logSb.AppendLine($"LocalScanMaxLines: {_config.DirectLocalScanMaxLinesWithinBlock}");
            logSb.AppendLine($"Timeout: {_config.DirectFindThenLocalTotalTimeoutMs}ms");

            try
            {
                // Step 1: GoToSttByFindThenClose
                var (findOk, findTimings) = GoToSttByFindThenCloseWithTiming(xmlNumber, targetStt);
                if (!findOk)
                {
                    swTotal.Stop();
                    logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Find failed");
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectFind_LocalFixNguoi_Fail", logSb.ToString());
                    MessageBox.Show($"Không thể tới STT {targetStt} bằng Find hoặc xác minh vị trí.", "Find failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                foreach (var kvp in findTimings)
                    logSb.AppendLine($"[Find: {kvp.Key}={kvp.Value}ms]");

                if (swTotal.ElapsedMilliseconds > _config.DirectFindThenLocalTotalTimeoutMs)
                {
                    swTotal.Stop();
                    logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Timeout exceeded after Find step");
                    var logPath = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectFind_NgguoiTimeout", logSb.ToString());
                    MessageBox.Show($"DirectFindThenLocalScanEdit quá thời gian (Find step).\nLog: {logPath}", "Timeout", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                // Step 2: Local scan for NGUOI_THUC_HIEN
                int linesScanned = 0;

                for (int i = 0; i < _config.DirectLocalScanMaxLinesWithinBlock; i++)
                {
                    AutomationCancellation.ThrowIfCancellationRequested();
                    if (swTotal.ElapsedMilliseconds > _config.DirectFindThenLocalTotalTimeoutMs)
                    {
                        swTotal.Stop();
                        logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Timeout exceeded during local scan at line {i}");
                        var logPath = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectFind_NgguoiScanTimeout", logSb.ToString());
                        MessageBox.Show($"DirectFindThenLocalScanEdit quá thời gian (scan line {i}).\nLog: {logPath}", "Timeout", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }

                    var swScan = System.Diagnostics.Stopwatch.StartNew();
                    MoveNextLineLocal();
                    var (currentLine, copyMs) = CopyCurrentLineLocalOptimized();
                    swScan.Stop();
                    linesScanned++;

                    if (_config.VerboseLineScanLog)
                    {
                        logSb.AppendLine($"[{swScan.ElapsedMilliseconds}ms] ScanLine {i + 1}: {currentLine}");
                    }

                    int parsed;
                    if (TryParseSttLine(currentLine, out parsed))
                    {
                        if (parsed != targetStt)
                        {
                            swTotal.Stop();
                            logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Encountered new STT {parsed} at line {i}, aborting");
                            SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectFind_NgguoiNewSttFound", logSb.ToString());
                            MessageBox.Show($"Đã gặp STT {parsed} trước khi thấy NGUOI_THUC_HIEN. Không sửa để tránh sai.", "Abort", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return false;
                        }
                    }

                    if (currentLine.Contains("</CHI_TIET_DVKT>"))
                    {
                        swTotal.Stop();
                        logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Encountered </CHI_TIET_DVKT> at line {i}, aborting");
                        SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectFind_NgguoiBlockEnd", logSb.ToString());
                        MessageBox.Show("Đã hết block CHI_TIET_DVKT trước khi thấy NGUOI_THUC_HIEN.", "Abort", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }

                    if (currentLine.IndexOf("<NGUOI_THUC_HIEN", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        logSb.AppendLine($"[Line {i + 1}] Found NGUOI_THUC_HIEN tag");

                        var existing = GetTagValueFromLine(currentLine, "NGUOI_THUC_HIEN");
                        bool isEmpty = IsEmptyTagLine(currentLine, "NGUOI_THUC_HIEN");

                        if (!string.IsNullOrEmpty(existing) && !isEmpty)
                        {
                            var ans = MessageBox.Show($"NGUOI_THUC_HIEN đã có giá trị: {existing}. Bạn có muốn ghi đè không?", "Xác nhận ghi đè", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            if (ans != DialogResult.Yes)
                            {
                                swTotal.Stop();
                                logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] User declined overwrite");
                                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectFind_NgguoiUserSkipped", logSb.ToString());
                                return false;
                            }
                        }

                        var swReplace = System.Diagnostics.Stopwatch.StartNew();
                        var newLine = BuildTagLinePreserveIndent(currentLine, "NGUOI_THUC_HIEN", practitionerCode);
                        var previewMsg = $"STT: {targetStt}\nField = NGUOI_THUC_HIEN\nOld line:\n{currentLine}\n\nNew line:\n{newLine}";
                        var conf = MessageBox.Show(previewMsg, "Preview Direct Edit", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                        if (conf != DialogResult.OK)
                        {
                            swTotal.Stop();
                            logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] User cancelled preview");
                            SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectFind_NgguoiUserCancelled", logSb.ToString());
                            return false;
                        }

                        if (!ReplaceCurrentLine(newLine))
                        {
                            swTotal.Stop();
                            logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] ReplaceCurrentLine failed");
                            SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectFind_NgguoiReplaceFailed", logSb.ToString());
                            MessageBox.Show("Không thể thay thế dòng hiện tại bằng nội dung mới.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return false;
                        }
                        swReplace.Stop();
                        logSb.AppendLine($"[{swReplace.ElapsedMilliseconds}ms] ReplaceCurrentLine");

                        var swVerifyAfter = System.Diagnostics.Stopwatch.StartNew();
                        var verifyLine = CopyCurrentLineLocal() ?? string.Empty;
                        swVerifyAfter.Stop();
                        var verified = verifyLine.Trim() == newLine.Trim() || verifyLine.Contains(practitionerCode);
                        logSb.AppendLine($"[{swVerifyAfter.ElapsedMilliseconds}ms] Verify after replace");
                        logSb.AppendLine($"OldLine: {currentLine}");
                        logSb.AppendLine($"NewLine: {newLine}");
                        logSb.AppendLine($"VerifyResult: {verified}");

                        swTotal.Stop();
                        logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] DirectFindThenLocalFixNguoiThucHien completed");
                        logSb.AppendLine($"Lines scanned: {linesScanned}");
                        SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectFind_NgguoiSuccess", logSb.ToString());

                        if (!verified)
                        {
                            MessageBox.Show("Dòng sau khi paste không khớp với nội dung mong đợi.", "Verification failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }

                        return true;
                    }
                }

                swTotal.Stop();
                logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Not found within {_config.DirectLocalScanMaxLinesWithinBlock} lines");
                logSb.AppendLine($"Lines scanned: {linesScanned}");
                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectFind_NgguoiNotFound", logSb.ToString());
                MessageBox.Show($"Không tìm thấy NGUOI_THUC_HIEN trong block STT {targetStt} (scanned {linesScanned} lines).", "Not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                swTotal.Stop();
                logSb.AppendLine($"[TOTAL: {swTotal.ElapsedMilliseconds}ms] Exception: {ex.ToString()}");
                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectFind_NgguoiException", logSb.ToString());
                MessageBox.Show("Exception during DirectFindThenLocalFixNguoiThucHien: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        // Debug: Test timing navigation (Find + verify only, no edit)
        public (bool success, Dictionary<string, long> timings, string logPath) TestTimingSuaNgayThYl(int xmlNumber, int targetStt)
        {
            var timings = new Dictionary<string, long>();
            var swTotal = System.Diagnostics.Stopwatch.StartNew();
            var logSb = new StringBuilder();
            logSb.AppendLine("ACTION: TestTimingSuaNgayThYl (DEBUG - Find + Verify only, no edit)");
            logSb.AppendLine($"XML number: {xmlNumber}");
            logSb.AppendLine($"Target STT: {targetStt}");

            try
            {
                // Step 1: GoToSttByFindThenClose
                var (findOk, findTimings) = GoToSttByFindThenCloseWithTiming(xmlNumber, targetStt);
                foreach (var kvp in findTimings)
                {
                    timings[kvp.Key] = kvp.Value;
                    logSb.AppendLine($"[{kvp.Key}={kvp.Value}ms]");
                }

                if (!findOk)
                {
                    swTotal.Stop();
                    timings["Total"] = swTotal.ElapsedMilliseconds;
                    logSb.AppendLine($"[TOTAL: {timings["Total"]}ms] Find + Verify FAILED");
                    var logPath = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_TestTiming_Fail", logSb.ToString());
                    return (false, timings, logPath);
                }

                // Step 2: Simple scan to show timing landmarks
                int linesScanned = 0;
                for (int i = 0; i < _config.DirectLocalScanMaxLinesWithinBlock; i++)
                {
                    AutomationCancellation.ThrowIfCancellationRequested();
                    var swScan = System.Diagnostics.Stopwatch.StartNew();
                    MoveNextLineLocal();
                    var (currentLine, copyMs) = CopyCurrentLineLocalOptimized();
                    swScan.Stop();
                    linesScanned++;

                    if (_config.VerboseLineScanLog)
                        logSb.AppendLine($"[ScanLine {i + 1}: {swScan.ElapsedMilliseconds}ms] {currentLine}");

                    if (currentLine.IndexOf("<NGAY_TH_YL", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        logSb.AppendLine($"Found NGAY_TH_YL at line {i + 1} ({swScan.ElapsedMilliseconds}ms scan time)");
                        break;
                    }

                    if (currentLine.Contains("</CHI_TIET_DVKT>"))
                    {
                        logSb.AppendLine($"Reached block end at line {i + 1}");
                        break;
                    }
                }

                swTotal.Stop();
                timings["Total"] = swTotal.ElapsedMilliseconds;
                timings["LinesScanned"] = linesScanned;
                logSb.AppendLine($"[TOTAL: {timings["Total"]}ms] Lines scanned: {linesScanned}");
                logSb.AppendLine("--- Test timing complete (NO DATA WAS MODIFIED) ---");

                var logPath2 = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_TestTiming_Success", logSb.ToString());
                return (true, timings, logPath2);
            }
            catch (Exception ex)
            {
                swTotal.Stop();
                timings["Total"] = swTotal.ElapsedMilliseconds;
                logSb.AppendLine($"[TOTAL: {timings["Total"]}ms] Exception: {ex.ToString()}");
                var logPath = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_TestTiming_Exception", logSb.ToString());
                return (false, timings, logPath);
            }
        }

        // Backwards-compatible wrapper: DirectIndexFixNguoiThucHien (calls DirectFindThenLocalFixNguoiThucHien)
        public bool DirectIndexFixNguoiThucHien(int xmlNumber, int targetStt, string practitionerCode)
        {
            var logSb = new StringBuilder();
            logSb.AppendLine("ACTION: DirectIndexFixNguoiThucHien (wrapper -> DirectFindThenLocalFixNguoiThucHien)");
            logSb.AppendLine($"XML number: {xmlNumber}");
            logSb.AppendLine($"Target STT: {targetStt}");
            logSb.AppendLine($"PractitionerCode: {practitionerCode}");
            logSb.AppendLine($"Start time: {DateTime.Now:O}");
            try
            {
                // call new local-find-first implementation to preserve behavior
                var ok = DirectFindThenLocalFixNguoiThucHien(xmlNumber, targetStt, practitionerCode);
                if (!ok)
                {
                    SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectIndexFixNguoiThucHien_Wrapper_Failed", logSb.ToString());
                }
                return ok;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logSb.AppendLine("Exception: " + ex.ToString());
                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{targetStt}_DirectIndexFixNguoiThucHien_Wrapper_Exception", logSb.ToString());
                return false;
            }
        }

        // Helper: extract leading indentation from a line
        public string ExtractIndent(string line)
        {
            if (string.IsNullOrEmpty(line)) return string.Empty;
            var sb = new StringBuilder();
            foreach (var ch in line)
            {
                if (ch == ' ' || ch == '\t') sb.Append(ch); else break;
            }
            return sb.ToString();
        }

        // Helper: detect empty tag line variants
        public bool IsEmptyTagLine(string line, string tagName)
        {
            if (string.IsNullOrEmpty(line)) return false;
            var patterns = new[] {
                $"<{tagName}\\s*/>",
                $"<{tagName}\\s*></{tagName}>",
                $"<{tagName}\\s*>\\s*</{tagName}>"
            };
            foreach (var p in patterns)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(line, p, System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return true;
            }
            // also treat lines with tag start and no digits/letters inside as empty
            var rx = new System.Text.RegularExpressions.Regex($"<{tagName}[^>]*>\\s*</{tagName}>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return rx.IsMatch(line);
        }

        // Helper: get tag value from line if present
        public string GetTagValueFromLine(string line, string tagName)
        {
            if (string.IsNullOrEmpty(line)) return null;
            var rx = new System.Text.RegularExpressions.Regex($"<{tagName}[^>]*>\\s*(?<v>.*?)\\s*</{tagName}>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var m = rx.Match(line);
            if (m.Success)
            {
                var value = m.Groups["v"].Value.Trim();
                var cdata = System.Text.RegularExpressions.Regex.Match(value, "^<!\\[CDATA\\[(?<v>.*)\\]\\]>$", System.Text.RegularExpressions.RegexOptions.Singleline);
                return cdata.Success ? cdata.Groups["v"].Value.Trim() : value;
            }
            return null;
        }

        // Helper: build new tag line preserving indent from oldLine
        public string BuildTagLinePreserveIndent(string oldLine, string tagName, string value)
        {
            var indent = ExtractIndent(oldLine);
            return indent + $"<{tagName}>{value}</{tagName}>";
        }

        // Helper: add minutes to NGAY_YL value (yyyyMMddHHmm)
        public string AddMinutesToNgayYl(string ngayYl, int minutes)
        {
            if (string.IsNullOrEmpty(ngayYl)) return null;
            DateTime dt;
            if (!DateTime.TryParseExact(ngayYl, "yyyyMMddHHmm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt)) return null;
            return dt.AddMinutes(minutes).ToString("yyyyMMddHHmm");
        }

        // Helper: try parse STT line
        public bool TryParseSttLine(string line, out int stt)
        {
            stt = 0;
            if (string.IsNullOrEmpty(line)) return false;
            var rx = new System.Text.RegularExpressions.Regex("<STT>\\s*(\\d+)\\s*</STT>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var m = rx.Match(line);
            if (m.Success) { int.TryParse(m.Groups[1].Value, out stt); return true; }
            return false;
        }

        // New: try parse NGAY_YL near current position by copying small context
        public string TryGetNgayYlNearCurrentPosition()
        {
            try
            {
                var txt = CopyCurrentLineOrSelection();
                if (string.IsNullOrEmpty(txt)) return null;
                // try to find tag value in copied text
                var rx = new System.Text.RegularExpressions.Regex("<NGAY_YL>\\s*(?<d>\\d{12})\\s*</NGAY_YL>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var m = rx.Match(txt);
                if (m.Success) return m.Groups["d"].Value;
                // try to find within a larger window: expand selection by sending Shift+Down a few times
                for (int i = 0; i < 5; i++)
                {
                    SendKeys.SendWait("+{DOWN}"); Thread.Sleep(50);
                }
                SendKeys.SendWait("^c"); Thread.Sleep(100);
                try { txt = Clipboard.GetText(); } catch { txt = null; }
                if (string.IsNullOrEmpty(txt)) return null;
                m = rx.Match(txt);
                if (m.Success) return m.Groups["d"].Value;
                return null;
            }
            catch { return null; }
        }

        // New: verify current position belongs to given STT by copying context and searching for STT tag
        public bool TryVerifyCurrentPositionBelongsToStt(int stt)
        {
            try
            {
                // copy a block of lines around current pos
                var txt = CopyCurrentLineOrSelection();
                if (string.IsNullOrEmpty(txt))
                {
                    // ask user confirmation if cannot copy
                    var ans = MessageBox.Show($"Không xác minh được field còn nằm trong STT {stt}. Bạn có chắc muốn sửa dòng đang chọn không?", "Xác nhận sửa trực tiếp", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    return ans == DialogResult.Yes;
                }
                var rx = new System.Text.RegularExpressions.Regex($"<STT>\\s*{stt}\\s*</STT>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (rx.IsMatch(txt)) return true;
                // if another STT appears and is different, fail
                var rxAny = new System.Text.RegularExpressions.Regex("<STT>\\s*(\\d+)\\s*</STT>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var m = rxAny.Match(txt);
                if (m.Success)
                {
                    int found; int.TryParse(m.Groups[1].Value, out found);
                    if (found != stt)
                    {
                        MessageBox.Show($"Detected STT {found} near current position which is different from target STT {stt}. Aborting direct edit.", "Verification failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                    return true;
                }
                // else ask user
                var ans2 = MessageBox.Show($"Không xác minh được field còn nằm trong STT {stt}. Bạn có chắc muốn sửa dòng đang chọn không?", "Xác nhận sửa trực tiếp", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                return ans2 == DialogResult.Yes;
            }
            catch { return false; }
        }

        // Enhanced ReadXmlText with UI Automation patterns first, then Clipboard fallback
        public string ReadXmlText(int xmlNumber)
        {
            var id = _config.XmlControlAutomationIdPrefix + xmlNumber.ToString();

            // Step 1: Try reading directly via UI Automation patterns (ValuePattern, TextPattern)
            var directText = ReadTextFromControlByAutomationId(id);
            // Accept direct read only if it looks like full XML (avoid selection/visible-only reads)
            if (!string.IsNullOrEmpty(directText))
            {
                var trimmed = directText.Trim();
                if (trimmed.Length > 500 && (trimmed.Contains("<CHI_TIET_DVKT") || trimmed.Contains("<CHITIEU") || trimmed.Contains("<?xml")))
                {
                    return directText;
                }
                // else consider direct read insufficient and fallback to clipboard
            }

            // Step 2: Fallback to Clipboard method
            var el = FindChildByAutomationId(id);
            if (el == null)
            {
                // Control not found - cannot read
                return null;
            }

            // Before copying, ensure main window is foreground and control is focused
            try
            {
                if (_mainWindowHandle == IntPtr.Zero) GetMainWindow();
                if (_mainWindowHandle != IntPtr.Zero)
                {
                    NativeMethods.SetForegroundWindow(_mainWindowHandle);
                    Thread.Sleep(100);
                }
            }
            catch { }

            // If a Find dialog might be open, send ESC to close it
            try { SendKeys.SendWait("{ESC}"); Thread.Sleep(100); } catch { }

            // Focus control with verification
            if (!FocusControlByAutomationId(id))
            {
                return null;
            }

            Thread.Sleep(300);

            // Step 3: Copy from focused control via Clipboard with retry
            return ReadXmlViaClipboard(id);
        }

        // Helper: Read XML content via Clipboard with retry logic
        private string ReadXmlViaClipboard(string automationId)
        {
            var marker = "**SUPPORTEDIT_CLIPBOARD_MARKER_" + Guid.NewGuid().ToString().Substring(0, 8) + "**";
            var originalClip = string.Empty;

            try
            {
                originalClip = Clipboard.GetText();
            }
            catch { }

            try
            {
                Clipboard.SetText(marker);
            }
            catch { }

            Thread.Sleep(100);

            // Send Ctrl+A to select all
            SendKeys.SendWait("^a");
            Thread.Sleep(200);

            // Send Ctrl+C to copy
            SendKeys.SendWait("^c");

            var attempts = _config.ClipboardRetryCount;
            var delay = _config.ClipboardRetryDelayMs;

            for (int i = 0; i < attempts; i++)
            {
                Thread.Sleep(delay);
                try
                {
                    var clipText = Clipboard.GetText();
                    // Check if clipboard has real content (not marker, not empty, contains XML markers)
                    if (!string.IsNullOrEmpty(clipText) && 
                        clipText != marker && 
                        (clipText.Contains("<?xml") || clipText.Contains("<CHI_TIET_DVKT") || clipText.Contains("<CHITIEU")))
                    {
                        return clipText;
                    }
                }
                catch { }
            }

            // Last attempt: try Ctrl+C again
            try
            {
                SendKeys.SendWait("^c");
                Thread.Sleep(500);
                var finalClip = Clipboard.GetText();
                if (!string.IsNullOrEmpty(finalClip) && finalClip != marker)
                {
                    return finalClip;
                }
            }
            catch { }

            // Restore original clipboard if possible
            try
            {
                if (!string.IsNullOrEmpty(originalClip))
                    Clipboard.SetText(originalClip);
            }
            catch { }

            return null;
        }

        public bool SetXmlText(int xmlNumber, string xmlContent)
        {
            var id = _config.XmlControlAutomationIdPrefix + xmlNumber.ToString();
            var ok = SetTextToControlByAutomationId(id, xmlContent);
            if (ok) return true;
            // fallback to clipboard paste
            var h = GetMainWindow();
            if (h == IntPtr.Zero) return false;
            if (!FocusControlByAutomationId(id)) return false;
            try { Clipboard.SetText(xmlContent ?? string.Empty); } catch { }
            SendKeys.SendWait("^a"); Thread.Sleep(_config.FindDelayMs);
            SendKeys.SendWait("^v"); Thread.Sleep(_config.FindDelayMs);
            return true;
        }

        // New: Direct UI fix for NGAY_TH_YL
        public bool DirectFixNgayThYl(int xmlNumber, int stt)
        {
            try
            {
                AutomationCancellation.ThrowIfCancellationRequested();
                var id = _config.XmlControlAutomationIdPrefix + xmlNumber.ToString();
                if (!FocusControlByAutomationId(id)) return false;

                // record last direct edit for log
                // (we do not store them persistently, just local variables)
                int LastDirectEditXmlNumber = xmlNumber;
                int LastDirectEditStt = stt;

                // Go to STT
                if (!GoToSttInXml(xmlNumber, stt)) return false;

                // Verify position
                if (!TryVerifyCurrentPositionBelongsToStt(stt)) return false;

                // Find NGAY_YL near current pos
                if (!FindTextInFocusedControl("<NGAY_YL>"))
                {
                    MessageBox.Show("Không tìm thấy <NGAY_YL> gần vị trí hiện tại.", "Direct Edit", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                var oldNgay = TryGetNgayYlNearCurrentPosition();
                if (string.IsNullOrEmpty(oldNgay))
                {
                    MessageBox.Show("Không lấy được giá trị NGAY_YL gần vị trí hiện tại.", "Direct Edit", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                // compute newNgay
                DateTime dt;
                if (!DateTime.TryParseExact(oldNgay, "yyyyMMddHHmm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
                {
                    MessageBox.Show("Giá trị NGAY_YL không đúng định dạng.", "Direct Edit", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
                var newNgay = dt.AddMinutes(10).ToString("yyyyMMddHHmm");

                // Find NGAY_TH_YL field
                if (!FindTextInFocusedControl("<NGAY_TH_YL"))
                {
                    MessageBox.Show("Không tìm thấy <NGAY_TH_YL> để sửa.", "Direct Edit", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                // Copy current line
                var oldLine = CopyCurrentLineOrSelection();
                if (oldLine == null) oldLine = string.Empty;

                // Simpler checks: if contains numeric value, detect existing value
                var rxHasVal = new System.Text.RegularExpressions.Regex("<NGAY_TH_YL>\\s*(\\d{12})\\s*</NGAY_TH_YL>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var hasVal = rxHasVal.Match(oldLine).Success;
                if (hasVal)
                {
                    var m = rxHasVal.Match(oldLine);
                    var existVal = m.Groups[1].Value;
                    var ans = MessageBox.Show($"NGAY_TH_YL đã có giá trị: {existVal}. Bạn có muốn ghi đè không?", "Xác nhận ghi đè", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (ans != DialogResult.Yes) return false;
                }

                // Verify current position still belongs to STT
                if (!TryVerifyCurrentPositionBelongsToStt(stt)) return false;

                var newLine = $"<NGAY_TH_YL>{newNgay}</NGAY_TH_YL>";

                // Show preview
                var previewMsg = $"STT: {stt}\nField: NGAY_TH_YL\nOld: {oldLine}\nNew: {newLine}";
                var conf = MessageBox.Show(previewMsg, "Preview Direct Edit", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                if (conf != DialogResult.OK) return false;

                // Replace line
                if (!ReplaceCurrentLine(newLine)) return false;

                // Log
                var logSb = new StringBuilder();
                logSb.AppendLine("ACTION: DirectFixNgayThYl");
                logSb.AppendLine($"XML number: {xmlNumber}");
                logSb.AppendLine($"STT: {stt}");
                logSb.AppendLine($"SearchText: <NGAY_TH_YL>");
                logSb.AppendLine($"OldLine: {oldLine}");
                logSb.AppendLine($"NewLine: {newLine}");
                var fi = GetFocusedElementInfo();
                logSb.AppendLine("FocusedElement:");
                foreach (var kvp in fi) logSb.AppendLine($"{kvp.Key}: {kvp.Value}");
                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{stt}_DirectFixNgayThYl", logSb.ToString());

                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var sb = new StringBuilder();
                sb.AppendLine("ACTION: DirectFixNgayThYl");
                sb.AppendLine("Exception:");
                sb.AppendLine(ex.ToString());
                SupportEdit.Services.DebugLogService.SaveDebugLog($"DirectFixNgayThYl_Exception", sb.ToString());
                return false;
            }
        }

        public bool DirectFixNguoiThucHien(int xmlNumber, int stt, string practitionerCode)
        {
            try
            {
                AutomationCancellation.ThrowIfCancellationRequested();
                var id = _config.XmlControlAutomationIdPrefix + xmlNumber.ToString();
                if (!FocusControlByAutomationId(id)) return false;

                int LastDirectEditXmlNumber = xmlNumber;
                int LastDirectEditStt = stt;

                if (!GoToSttInXml(xmlNumber, stt)) return false;
                if (!TryVerifyCurrentPositionBelongsToStt(stt)) return false;

                if (!FindTextInFocusedControl("<NGUOI_THUC_HIEN"))
                {
                    MessageBox.Show("Không tìm thấy <NGUOI_THUC_HIEN> để sửa.", "Direct Edit", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                var oldLine = CopyCurrentLineOrSelection() ?? string.Empty;

                var rxHasVal = new System.Text.RegularExpressions.Regex("<NGUOI_THUC_HIEN>\\s*(.+?)\\s*</NGUOI_THUC_HIEN>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var hasVal = rxHasVal.Match(oldLine).Success;
                if (hasVal)
                {
                    var m = rxHasVal.Match(oldLine);
                    var existVal = m.Groups[1].Value;
                    var ans = MessageBox.Show($"NGUOI_THUC_HIEN đã có giá trị: {existVal}. Bạn có muốn ghi đè không?", "Xác nhận ghi đè", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (ans != DialogResult.Yes) return false;
                }

                if (!TryVerifyCurrentPositionBelongsToStt(stt)) return false;

                var newLine = $"<NGUOI_THUC_HIEN>{practitionerCode}</NGUOI_THUC_HIEN>";

                var previewMsg = $"STT: {stt}\nField: NGUOI_THUC_HIEN\nOld: {oldLine}\nNew: {newLine}";
                var conf = MessageBox.Show(previewMsg, "Preview Direct Edit", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                if (conf != DialogResult.OK) return false;

                if (!ReplaceCurrentLine(newLine)) return false;

                var logSb = new StringBuilder();
                logSb.AppendLine("ACTION: DirectFixNguoiThucHien");
                logSb.AppendLine($"XML number: {xmlNumber}");
                logSb.AppendLine($"STT: {stt}");
                logSb.AppendLine($"SearchText: <NGUOI_THUC_HIEN>");
                logSb.AppendLine($"OldLine: {oldLine}");
                logSb.AppendLine($"NewLine: {newLine}");
                var fi = GetFocusedElementInfo();
                logSb.AppendLine("FocusedElement:");
                foreach (var kvp in fi) logSb.AppendLine($"{kvp.Key}: {kvp.Value}");
                SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{stt}_DirectFixNguoiThucHien", logSb.ToString());

                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var sb = new StringBuilder();
                sb.AppendLine("ACTION: DirectFixNguoiThucHien");
                sb.AppendLine("Exception:");
                sb.AppendLine(ex.ToString());
                SupportEdit.Services.DebugLogService.SaveDebugLog($"DirectFixNguoiThucHien_Exception", sb.ToString());
                return false;
            }
        }

        public bool GoToSttInXml(int xmlNumber, int stt)
        {
            var id = _config.XmlControlAutomationIdPrefix + xmlNumber.ToString();
            var el = FindChildByAutomationId(id);
            if (el == null)
            {
                // fallback: cannot identify control
                return false;
            }
            try
            {
                el.SetFocus();
                // send find sequence: Ctrl+F, Ctrl+A, paste search, Enter, ESC, then refocus
                SendKeys.SendWait("^f"); Thread.Sleep(_config.FindDelayMs);
                SendKeys.SendWait("^a"); Thread.Sleep(50);
                try { Clipboard.SetText($"<STT>{stt}</STT>"); } catch { }
                SendKeys.SendWait("^v"); Thread.Sleep(50);
                SendKeys.SendWait("{ENTER}");
                Thread.Sleep(100);
                SendKeys.SendWait("{ESC}"); Thread.Sleep(50);
                // focus control again
                FocusControlByAutomationId(id);
                return true;
            }
            catch { return false; }
        }

    }

    internal static class NativeMethods
    {
        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP = 0x0004;

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
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll", EntryPoint = "mouse_event")]
        public static extern void Mouse_Event(uint dwFlags, uint dx, uint dy, uint cButtons, IntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    }
}
