using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;

namespace SupportEdit.Services
{
    public sealed class AttendanceCheckResult
    {
        public bool Success { get; init; }
        public bool IsWorking { get; init; }
        public string TimeIn { get; init; } = string.Empty;
        public string TimeOut { get; init; } = string.Empty;
        public string ReplacementPractitionerCode { get; init; } = string.Empty;
        public string ReplacementTimeIn { get; init; } = string.Empty;
        public string ReplacementTimeOut { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
    }

    public sealed class AttendanceCheckService
    {
        private const int AttendanceWindowTimeoutMs = 8000;
        private const int AttendanceResultTimeoutMs = 6000;
        private static readonly TimeSpan MinimumCheckoutTime = new(16, 0, 0);

        private readonly ConfigService _config;
        private readonly AutomationService _automation;

        public AttendanceCheckService(ConfigService config, AutomationService automation)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        public AttendanceCheckResult Check(
            string categoryName,
            DateTime workDate,
            string practitionerCode,
            bool findReplacementPractitioner = false)
        {
            var log = new StringBuilder();
            log.AppendLine("ACTION: CheckPractitionerAttendance");
            log.AppendLine($"CategoryName: {categoryName}");
            log.AppendLine($"WorkDate: {workDate:dd/MM/yyyy}");
            log.AppendLine($"PractitionerCode: {practitionerCode}");

            try
            {
                if (string.IsNullOrWhiteSpace(categoryName) || string.IsNullOrWhiteSpace(practitionerCode))
                {
                    return Fail("Thiếu khoa hoặc mã chứng chỉ hành nghề để kiểm tra chấm công.", log);
                }

                var mainWindow = _automation.FindWindowByTitleContains(_config.XmlAppWindowTitleKeyword);
                if (mainWindow == IntPtr.Zero)
                {
                    return Fail("Không tìm thấy cửa sổ phần mềm XML để mở Tra cứu chấm công.", log);
                }

                var attendanceWindow = FindWindow("Tra cứu chấm công", mainWindow);
                if (attendanceWindow == IntPtr.Zero)
                {
                    if (!_automation.ActivateWindow(mainWindow))
                    {
                        return Fail("Không thể kích hoạt cửa sổ phần mềm XML.", log);
                    }

                    if (!TryOpenAttendanceWindow(mainWindow, log))
                    {
                        return Fail("Không tìm thấy hoặc không bấm được nút Tra cứu chấm công trên phần mềm XML.", log);
                    }

                    attendanceWindow = WaitForWindow("Tra cứu chấm công", mainWindow, AttendanceWindowTimeoutMs);
                    if (attendanceWindow == IntPtr.Zero)
                    {
                        return Fail("Không thấy cửa sổ Tra cứu chấm công sau khi bấm nút.", log);
                    }
                }
                else
                {
                    log.AppendLine("Reusing the attendance window that is already open.");
                }

                ActivateWindow(attendanceWindow);
                var attendanceRoot = AutomationElement.FromHandle(attendanceWindow);

                if (!SelectDepartmentByNativeHandle(attendanceWindow, categoryName, log)
                    && !SelectDepartment(attendanceRoot, categoryName, log))
                {
                    return Fail($"Không chọn được khoa '{categoryName}' trong cửa sổ Tra cứu chấm công.", log);
                }

                if (!SetAttendanceDateByNativeHandle(attendanceWindow, workDate, log)
                    && !SetAttendanceDate(attendanceRoot, workDate, log))
                {
                    return Fail($"Không nhập được ngày {workDate:dd/MM/yyyy} trong cửa sổ Tra cứu chấm công.", log);
                }

                if (!ClickSearchButtonByNativeHandle(attendanceWindow, log))
                {
                    return Fail("Không tìm thấy hoặc không bấm được nút Tra cứu.", log);
                }

                var attendanceGrid = FindAttendanceGrid(attendanceWindow, attendanceRoot);
                var sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < AttendanceResultTimeoutMs)
                {
                    if (TryReadPractitionerRow(attendanceGrid, practitionerCode, out var timeIn, out var timeOut))
                    {
                        var result = BuildAttendanceResult(timeIn, timeOut, log);
                        if (!result.IsWorking && findReplacementPractitioner)
                        {
                            result = FindAndAttachReplacement(attendanceGrid, practitionerCode, result, log);
                        }
                        if (result.IsWorking || !string.IsNullOrWhiteSpace(result.ReplacementPractitionerCode))
                        {
                            ActivateWindow(mainWindow);
                        }
                        return result;
                    }

                    Thread.Sleep(250);
                }

                if (findReplacementPractitioner
                    && TryFindEligibleReplacement(
                        attendanceGrid,
                        practitionerCode,
                        out var replacementCode,
                        out var replacementTimeIn,
                        out var replacementTimeOut))
                {
                    log.AppendLine($"Original practitioner was not found; replacement selected: {replacementCode} ({replacementTimeIn} - {replacementTimeOut}).");
                    var replacementResult = BuildReplacementResult(replacementCode, replacementTimeIn, replacementTimeOut);
                    ActivateWindow(mainWindow);
                    return replacementResult;
                }

                if (TryFindAndCopyPractitionerRow(attendanceRoot, practitionerCode, log, out var copiedTimeIn, out var copiedTimeOut))
                {
                    var result = BuildAttendanceResult(copiedTimeIn, copiedTimeOut, log);
                    if (!result.IsWorking && findReplacementPractitioner)
                    {
                        result = FindAndAttachReplacement(attendanceGrid, practitionerCode, result, log);
                    }
                    if (result.IsWorking || !string.IsNullOrWhiteSpace(result.ReplacementPractitionerCode))
                    {
                        ActivateWindow(mainWindow);
                    }
                    return result;
                }

                return Fail($"Không tìm thấy chứng chỉ hành nghề '{practitionerCode}' trong bảng chấm công.", log);
            }
            catch (Exception ex)
            {
                log.AppendLine(ex.ToString());
                return Fail("Có lỗi khi kiểm tra chấm công: " + ex.Message, log);
            }
        }

        private static AttendanceCheckResult BuildAttendanceResult(string timeIn, string timeOut, StringBuilder log)
        {
            timeIn = NormalizeAttendanceValue(timeIn);
            timeOut = NormalizeAttendanceValue(timeOut);
            log.AppendLine($"TimeIn: {timeIn}");
            log.AppendLine($"TimeOut: {timeOut}");

            var isWorking = HasValidAttendance(timeIn, timeOut);
            return new AttendanceCheckResult
            {
                Success = true,
                IsWorking = isWorking,
                TimeIn = timeIn,
                TimeOut = timeOut,
                Message = isWorking
                    ? "Đã xác nhận người thực hiện có đủ giờ vào và giờ ra sau 16:00:00."
                    : "NGUOI_THUC_HIEN này off nên không thể thay thế"
            };
        }

        private static AttendanceCheckResult FindAndAttachReplacement(
            AutomationElement attendanceGrid,
            string originalPractitionerCode,
            AttendanceCheckResult originalResult,
            StringBuilder log)
        {
            if (!TryFindEligibleReplacement(
                    attendanceGrid,
                    originalPractitionerCode,
                    out var replacementCode,
                    out var replacementTimeIn,
                    out var replacementTimeOut))
            {
                log.AppendLine("No eligible replacement practitioner was found.");
                return originalResult;
            }

            log.AppendLine($"Replacement selected: {replacementCode} ({replacementTimeIn} - {replacementTimeOut}).");
            return new AttendanceCheckResult
            {
                Success = true,
                IsWorking = false,
                TimeIn = originalResult.TimeIn,
                TimeOut = originalResult.TimeOut,
                ReplacementPractitionerCode = replacementCode,
                ReplacementTimeIn = replacementTimeIn,
                ReplacementTimeOut = replacementTimeOut,
                Message = originalResult.Message
            };
        }

        private static AttendanceCheckResult BuildReplacementResult(
            string replacementCode,
            string replacementTimeIn,
            string replacementTimeOut)
        {
            return new AttendanceCheckResult
            {
                Success = true,
                IsWorking = false,
                ReplacementPractitionerCode = replacementCode,
                ReplacementTimeIn = replacementTimeIn,
                ReplacementTimeOut = replacementTimeOut,
                Message = "NGUOI_THUC_HIEN này off nên không thể thay thế"
            };
        }

        private static AttendanceCheckResult Fail(string message, StringBuilder log)
        {
            log.AppendLine("FAILED: " + message);
            try
            {
                DebugLogService.SaveDebugLog("AttendanceCheck_Failed", log.ToString());
            }
            catch
            {
            }

            return new AttendanceCheckResult
            {
                Success = false,
                IsWorking = false,
                Message = message
            };
        }

        private static bool TryOpenAttendanceWindow(IntPtr mainWindow, StringBuilder log)
        {
            var sw = Stopwatch.StartNew();

            // DevExpress BarButtonItem does not own a Win32 child handle. Locate it
            // directly in the toolbar strip by accessibility-at-point instead of
            // traversing the very large XML editor accessibility tree.
            if (TryClickAccessibleToolbarItem(mainWindow, "Tra cứu chấm công"))
            {
                log.AppendLine($"Attendance toolbar button clicked by point accessibility in {sw.ElapsedMilliseconds} ms.");
                return true;
            }

            // Some DevExpress versions expose bar items only through MSAA. Keep this
            // fallback deliberately shallow so it never walks through XML grids/editors.
            if (TryInvokeAccessibleAction(mainWindow, "Tra cứu chấm công"))
            {
                log.AppendLine($"Attendance toolbar button invoked by shallow MSAA in {sw.ElapsedMilliseconds} ms.");
                return true;
            }

            log.AppendLine($"Attendance toolbar button was not exposed to accessibility after {sw.ElapsedMilliseconds} ms.");
            return false;
        }

        private static bool TryClickAccessibleToolbarItem(IntPtr windowHandle, string caption)
        {
            try
            {
                var clientOrigin = new NativeMethods.POINT();
                if (!NativeMethods.ClientToScreen(windowHandle, ref clientOrigin)
                    || !NativeMethods.GetClientRect(windowHandle, out var clientRect))
                {
                    return false;
                }

                var wanted = NormalizeSearch(caption);
                var clientWidth = Math.Max(0, clientRect.Right - clientRect.Left);
                // bar2 is docked at the top of the client area. Two scan lines make
                // this independent of toolbar height and Windows display scaling.
                foreach (var clientY in new[] { 14, 25, 36 })
                {
                    for (var clientX = 8; clientX < clientWidth; clientX += 18)
                    {
                        var point = new NativeMethods.POINT
                        {
                            X = clientOrigin.X + clientX,
                            Y = clientOrigin.Y + clientY
                        };
                        if (NativeMethods.AccessibleObjectFromPoint(point, out var accessible, out var childId) < 0
                            || accessible == null)
                        {
                            continue;
                        }

                        string name;
                        try { name = accessible.get_accName(childId) ?? string.Empty; }
                        catch { continue; }
                        if (NormalizeSearch(name) != wanted)
                        {
                            continue;
                        }

                        ClickAt(point.X, point.Y);
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool SelectDepartmentByNativeHandle(IntPtr attendanceWindow, string categoryName, StringBuilder log)
        {
            var combo = FindNativeChild(attendanceWindow, className =>
                className.Contains("COMBOBOX", StringComparison.OrdinalIgnoreCase));
            if (combo == IntPtr.Zero)
            {
                log.AppendLine("Native cbbDepartment handle not found; trying UI Automation fallback.");
                return false;
            }

            var wanted = NormalizeDepartment(categoryName);
            var count = NativeMethods.SendMessage(combo, NativeMethods.CB_GETCOUNT, IntPtr.Zero, IntPtr.Zero).ToInt32();
            for (var index = 0; index < count; index++)
            {
                var length = NativeMethods.SendMessage(combo, NativeMethods.CB_GETLBTEXTLEN, new IntPtr(index), IntPtr.Zero).ToInt32();
                if (length < 0) continue;

                var text = new StringBuilder(length + 1);
                NativeMethods.SendMessage(combo, NativeMethods.CB_GETLBTEXT, new IntPtr(index), text);
                if (NormalizeDepartment(text.ToString()) != wanted) continue;

                if (NativeMethods.SendMessage(combo, NativeMethods.CB_SETCURSEL, new IntPtr(index), IntPtr.Zero).ToInt32() < 0)
                {
                    return false;
                }

                var parent = NativeMethods.GetParent(combo);
                var controlId = NativeMethods.GetDlgCtrlID(combo);
                NativeMethods.SendMessage(
                    parent,
                    NativeMethods.WM_COMMAND,
                    NativeMethods.MakeWParam(controlId, NativeMethods.CBN_SELCHANGE),
                    combo);
                log.AppendLine("Department selected directly on native cbbDepartment: " + text);
                return true;
            }

            log.AppendLine($"Department '{categoryName}' was not present in native cbbDepartment.");
            return false;
        }

        private static bool SetAttendanceDateByNativeHandle(IntPtr attendanceWindow, DateTime date, StringBuilder log)
        {
            var datePicker = FindNativeChild(attendanceWindow, className =>
                className.Contains("SysDateTimePick32", StringComparison.OrdinalIgnoreCase)
                || className.Contains("DateTimePicker", StringComparison.OrdinalIgnoreCase));
            if (datePicker == IntPtr.Zero)
            {
                log.AppendLine("Native dtpkDate handle not found by class name; trying the known form layout.");
                return SetAttendanceDateByKnownLayout(attendanceWindow, date, log);
            }

            if (!SetNativeDateTimePickerValue(datePicker, date))
            {
                log.AppendLine("Direct DatePicker update was unavailable; entering DD/MM/YYYY by keyboard on the exact native control.");
                return SetAttendanceDateByNativeKeyboard(attendanceWindow, datePicker, date, log);
            }

            log.AppendLine("Attendance date set directly on native dtpkDate: " + date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
            return true;
        }

        private static bool SetAttendanceDateByKnownLayout(IntPtr attendanceWindow, DateTime date, StringBuilder log)
        {
            var combo = FindNativeChild(attendanceWindow, className =>
                className.Contains("COMBOBOX", StringComparison.OrdinalIgnoreCase));
            var searchButton = FindNativeChild(
                attendanceWindow,
                className => className.Contains("BUTTON", StringComparison.OrdinalIgnoreCase),
                text => NormalizeSearch(text) == "tra cuu");

            if (combo == IntPtr.Zero
                || searchButton == IntPtr.Zero
                || !NativeMethods.GetWindowRect(combo, out var comboRect)
                || !NativeMethods.GetWindowRect(searchButton, out var buttonRect))
            {
                log.AppendLine("Could not calculate dtpkDate position from cbbDepartment and btnSearch.");
                return false;
            }

            var gap = buttonRect.Left - comboRect.Right;
            if (gap < 100)
            {
                log.AppendLine($"Invalid space for dtpkDate between cbbDepartment and btnSearch: {gap}px.");
                return false;
            }

            // Frm_TracuuChamcong designer places dtpkDate between cbbDepartment and
            // btnSearch on the same row. Calculate the date field from those two
            // stable native controls so this also works when WinForms does not expose
            // the DateTimePicker HWND/UIA element on a particular machine.
            var dateLeft = comboRect.Right + (int)Math.Round(gap * 0.38);
            var dateRight = buttonRect.Left - Math.Max(12, (int)Math.Round(gap * 0.07));
            var centerY = ((comboRect.Top + comboRect.Bottom) + (buttonRect.Top + buttonRect.Bottom)) / 4;
            if (dateRight - dateLeft < 70)
            {
                log.AppendLine("Calculated dtpkDate rectangle is too small.");
                return false;
            }

            return EnterDateByKeyboard(
                attendanceWindow,
                dateLeft + Math.Max(8, (dateRight - dateLeft) / 10),
                centerY,
                date,
                log,
                "known Frm_TracuuChamcong layout");
        }

        private static bool SetAttendanceDateByNativeKeyboard(
            IntPtr attendanceWindow,
            IntPtr datePicker,
            DateTime date,
            StringBuilder log)
        {
            if (!NativeMethods.GetWindowRect(datePicker, out var rect)
                || rect.Right <= rect.Left
                || rect.Bottom <= rect.Top)
            {
                return false;
            }

            return EnterDateByKeyboard(
                attendanceWindow,
                rect.Left + Math.Max(8, (rect.Right - rect.Left) / 10),
                rect.Top + ((rect.Bottom - rect.Top) / 2),
                date,
                log,
                "native dtpkDate rectangle");
        }

        private static bool EnterDateByKeyboard(
            IntPtr attendanceWindow,
            int x,
            int y,
            DateTime date,
            StringBuilder log,
            string source)
        {
            try
            {
                ActivateWindow(attendanceWindow);
                ClickAt(x, y);
                Thread.Sleep(80);

                // A WinForms DateTimePicker edits one segment at a time. HOME selects
                // the first (DD) segment; RIGHT advances to MM and then YYYY.
                SendKeys.SendWait("{HOME}");
                SendKeys.SendWait(date.ToString("dd", CultureInfo.InvariantCulture));
                SendKeys.SendWait("{RIGHT}");
                SendKeys.SendWait(date.ToString("MM", CultureInfo.InvariantCulture));
                SendKeys.SendWait("{RIGHT}");
                SendKeys.SendWait(date.ToString("yyyy", CultureInfo.InvariantCulture));
                SendKeys.SendWait("{TAB}");
                Thread.Sleep(120);

                if (!NativeMethods.IsWindow(attendanceWindow))
                {
                    log.AppendLine("Attendance window disappeared while entering the date.");
                    return false;
                }

                log.AppendLine($"Attendance date entered as DD/MM/YYYY from {source}: {date:dd/MM/yyyy}");
                return true;
            }
            catch (Exception ex)
            {
                log.AppendLine("DatePicker keyboard input failed: " + ex.Message);
                return false;
            }
        }

        private static bool ClickSearchButtonByNativeHandle(IntPtr attendanceWindow, StringBuilder log)
        {
            var searchButton = FindNativeChild(
                attendanceWindow,
                className => className.Contains("BUTTON", StringComparison.OrdinalIgnoreCase),
                text => NormalizeSearch(text) == "tra cuu");
            if (searchButton == IntPtr.Zero)
            {
                log.AppendLine("Native btnSearch handle with caption 'Tra cứu' not found.");
                return false;
            }

            if (!NativeMethods.IsWindowVisible(searchButton) || !NativeMethods.IsWindowEnabled(searchButton))
            {
                log.AppendLine("Native btnSearch is hidden or disabled.");
                return false;
            }

            if (!NativeMethods.GetWindowRect(searchButton, out var buttonRect))
            {
                log.AppendLine("Cannot read native btnSearch rectangle.");
                return false;
            }

            ActivateWindow(attendanceWindow);
            ClickAt(
                buttonRect.Left + ((buttonRect.Right - buttonRect.Left) / 2),
                buttonRect.Top + ((buttonRect.Bottom - buttonRect.Top) / 2));
            log.AppendLine("Native btnSearch clicked at its window rectangle center.");
            return true;
        }

        private static AutomationElement FindAttendanceGrid(IntPtr attendanceWindow, AutomationElement attendanceRoot)
        {
            var gridHandle = FindNativeChild(attendanceWindow, className =>
                className.Contains("GridControl", StringComparison.OrdinalIgnoreCase)
                || className.Contains("XtraGrid", StringComparison.OrdinalIgnoreCase));
            if (gridHandle != IntPtr.Zero)
            {
                try { return AutomationElement.FromHandle(gridHandle); }
                catch { }
            }

            return WaitForElementByAutomationId(attendanceRoot, "gridControl1", 500)
                ?? attendanceRoot;
        }

        private static IntPtr FindNativeChild(
            IntPtr parent,
            Func<string, bool> classPredicate,
            Func<string, bool>? textPredicate = null)
        {
            var found = IntPtr.Zero;
            NativeMethods.EnumChildWindows(parent, (child, _) =>
            {
                var className = GetWindowClassName(child);
                if (!classPredicate(className)) return true;

                var text = GetWindowTitle(child);
                if (textPredicate != null && !textPredicate(text)) return true;

                found = child;
                return false;
            }, IntPtr.Zero);
            return found;
        }

        private static string GetWindowClassName(IntPtr hWnd)
        {
            var className = new StringBuilder(256);
            return NativeMethods.GetClassName(hWnd, className, className.Capacity) > 0
                ? className.ToString()
                : string.Empty;
        }

        private static bool SelectDepartment(AutomationElement root, string categoryName, StringBuilder log)
        {
            var combo = FindByAutomationId(root, "cbbDepartment")
                ?? FindAllByControlType(root, ControlType.ComboBox).FirstOrDefault();
            if (combo == null)
            {
                log.AppendLine("Department ComboBox not found.");
                return false;
            }

            try
            {
                if (combo.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expandObj) && expandObj is ExpandCollapsePattern expand)
                {
                    expand.Expand();
                    Thread.Sleep(200);
                }
            }
            catch
            {
            }

            var wanted = NormalizeDepartment(categoryName);
            var items = FindAllByControlType(combo, ControlType.ListItem)
                .Concat(FindAllByControlType(root, ControlType.ListItem))
                .GroupBy(GetRuntimeIdKey)
                .Select(group => group.First())
                .ToList();

            var match = items.FirstOrDefault(item => NormalizeDepartment(ReadText(item)) == wanted);
            if (match != null)
            {
                try
                {
                    if (match.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectObj) && selectObj is SelectionItemPattern selection)
                    {
                        selection.Select();
                        Thread.Sleep(150);
                        log.AppendLine("Department selected from ComboBox list: " + ReadText(match));
                        return true;
                    }
                }
                catch
                {
                }

                if (InvokeOrClick(match))
                {
                    log.AppendLine("Department clicked in ComboBox list: " + ReadText(match));
                    return true;
                }
            }

            var displayName = NormalizeSearch(categoryName).StartsWith("khoa ", StringComparison.Ordinal)
                ? categoryName.Trim()
                : "Khoa " + categoryName.Trim();
            if (SetValue(combo, displayName))
            {
                var actual = NormalizeDepartment(ReadText(combo));
                if (actual == wanted)
                {
                    log.AppendLine("Department set by ValuePattern: " + displayName);
                    return true;
                }
            }

            return SetValueByKeyboard(combo, displayName, log, "Department");
        }

        private static bool SetAttendanceDate(AutomationElement root, DateTime date, StringBuilder log)
        {
            var dateText = date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            var knownDatePicker = FindByAutomationId(root, "dtpkDate");
            if (knownDatePicker != null && SetNativeDateTimePickerValue(knownDatePicker, date))
            {
                log.AppendLine("Attendance date set directly on dtpkDate: " + dateText);
                return true;
            }

            var comboRect = FindAllByControlType(root, ControlType.ComboBox)
                .Select(TryGetBoundingRectangle)
                .FirstOrDefault(rect => !rect.IsEmpty);

            var candidates = GetAllDescendants(root)
                .Where(element =>
                {
                    var rect = TryGetBoundingRectangle(element);
                    return comboRect.IsEmpty || rect.IsEmpty || !comboRect.Contains(rect.TopLeft);
                })
                .ToList();

            var dateEdit = knownDatePicker
                ?? candidates
                .Where(element => Regex.IsMatch(ReadText(element) ?? string.Empty, @"\b\d{1,2}/\d{1,2}/\d{4}\b"))
                .OrderBy(element => IsControlType(element, ControlType.Edit) ? 0 : 1)
                .FirstOrDefault()
                ?? candidates.FirstOrDefault(element =>
                {
                    var identity = NormalizeSearch(GetAutomationId(element) + " " + ReadText(element));
                    return identity.Contains("date", StringComparison.Ordinal)
                        || identity.Contains("ngay", StringComparison.Ordinal);
                })
                ?? candidates.FirstOrDefault(element => IsControlType(element, ControlType.Edit));

            if (dateEdit == null)
            {
                log.AppendLine("DatePicker control not found.");
                return false;
            }

            if (IsExpectedDate(ReadText(dateEdit), date))
            {
                log.AppendLine("Attendance date already has expected value: " + dateText);
                return true;
            }

            if (SetValue(dateEdit, dateText))
            {
                try
                {
                    dateEdit.SetFocus();
                    SendKeys.SendWait("{TAB}");
                    Thread.Sleep(100);
                }
                catch
                {
                }

                if (IsExpectedDate(ReadText(dateEdit), date))
                {
                    log.AppendLine("Attendance date set by ValuePattern: " + dateText);
                    return true;
                }

                log.AppendLine("DatePicker did not accept the full ValuePattern value; retrying by DD/MM/YYYY segments.");
            }

            return SetDateBySegments(dateEdit, date, log);
        }

        private static bool SetDateBySegments(AutomationElement datePicker, DateTime date, StringBuilder log)
        {
            var day = date.ToString("dd", CultureInfo.InvariantCulture);
            var month = date.ToString("MM", CultureInfo.InvariantCulture);
            var year = date.ToString("yyyy", CultureInfo.InvariantCulture);

            try
            {
                var rect = TryGetBoundingRectangle(datePicker);
                if (!rect.IsEmpty && rect.Width >= 70)
                {
                    // DevExpress DateEdit selects each masked date part when that part is double-clicked.
                    // Leave room on the right for the calendar drop-down button.
                    var usableWidth = Math.Max(60, rect.Width - Math.Min(28, rect.Width * 0.20));
                    var unit = Math.Min(10, usableWidth / 10.0);
                    var centerY = (int)Math.Round(rect.Top + (rect.Height / 2));
                    var dayX = (int)Math.Round(rect.Left + (unit * 1.2));
                    var monthX = (int)Math.Round(rect.Left + (unit * 4.1));
                    var yearX = (int)Math.Round(rect.Left + (unit * 8.0));

                    DoubleClickAt(dayX, centerY);
                    SendKeys.SendWait(day);
                    Thread.Sleep(80);

                    DoubleClickAt(monthX, centerY);
                    SendKeys.SendWait(month);
                    Thread.Sleep(80);

                    DoubleClickAt(yearX, centerY);
                    SendKeys.SendWait(year);
                    Thread.Sleep(80);

                    SendKeys.SendWait("{TAB}");
                    Thread.Sleep(150);
                    log.AppendLine($"Attendance date entered by segments: {day}/{month}/{year}");
                    return VerifySegmentedDateWhenReadable(datePicker, date, log);
                }

                // Keyboard-only fallback for masked editors without a usable UIA rectangle.
                datePicker.SetFocus();
                Thread.Sleep(100);
                SendKeys.SendWait("{HOME}");
                SendKeys.SendWait(day);
                SendKeys.SendWait("{RIGHT}");
                SendKeys.SendWait(month);
                SendKeys.SendWait("{RIGHT}");
                SendKeys.SendWait(year);
                SendKeys.SendWait("{TAB}");
                Thread.Sleep(150);
                log.AppendLine($"Attendance date entered by keyboard segments: {day}/{month}/{year}");
                return VerifySegmentedDateWhenReadable(datePicker, date, log);
            }
            catch (Exception ex)
            {
                log.AppendLine("Segmented DatePicker input failed: " + ex.Message);
                return false;
            }
        }

        private static bool VerifySegmentedDateWhenReadable(AutomationElement datePicker, DateTime expected, StringBuilder log)
        {
            var current = ReadText(datePicker);
            if (!Regex.IsMatch(current ?? string.Empty, @"\b\d{1,2}/\d{1,2}/\d{4}\b"))
            {
                // Some DevExpress versions do not expose the edited text through UI Automation.
                return true;
            }

            var isExpected = IsExpectedDate(current, expected);
            log.AppendLine($"DatePicker value after segmented input: {current}; Matches={isExpected}");
            return isExpected;
        }

        private static bool IsExpectedDate(string value, DateTime expected)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return DateTime.TryParseExact(
                       value.Trim(),
                       new[] { "d/M/yyyy", "dd/MM/yyyy" },
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.None,
                       out var actual)
                   && actual.Date == expected.Date;
        }

        private static bool IsControlType(AutomationElement element, ControlType controlType)
        {
            try { return element.Current.ControlType == controlType; }
            catch { return false; }
        }

        private sealed class AttendanceRowData
        {
            public string PractitionerCode { get; init; } = string.Empty;
            public string TimeIn { get; init; } = string.Empty;
            public string TimeOut { get; init; } = string.Empty;
        }

        private static bool TryFindEligibleReplacement(
            AutomationElement root,
            string originalPractitionerCode,
            out string replacementCode,
            out string replacementTimeIn,
            out string replacementTimeOut)
        {
            replacementCode = string.Empty;
            replacementTimeIn = string.Empty;
            replacementTimeOut = string.Empty;
            var originalCodeKey = NormalizeCode(originalPractitionerCode);

            var replacement = ReadAttendanceRows(root)
                .FirstOrDefault(row =>
                    NormalizeCode(row.PractitionerCode) != originalCodeKey
                    && HasValidAttendance(row.TimeIn, row.TimeOut));
            if (replacement == null)
            {
                return false;
            }

            replacementCode = replacement.PractitionerCode;
            replacementTimeIn = NormalizeAttendanceValue(replacement.TimeIn);
            replacementTimeOut = NormalizeAttendanceValue(replacement.TimeOut);
            return true;
        }

        private static List<AttendanceRowData> ReadAttendanceRows(AutomationElement root)
        {
            var rows = new List<AttendanceRowData>();
            var elements = GetAllDescendants(root);
            elements.Insert(0, root);

            foreach (var element in elements)
            {
                try
                {
                    if (!element.TryGetCurrentPattern(GridPattern.Pattern, out var gridObject)
                        || gridObject is not GridPattern grid)
                    {
                        continue;
                    }

                    var rowCount = grid.Current.RowCount;
                    var columnCount = grid.Current.ColumnCount;
                    if (rowCount <= 0 || columnCount < 5 || rowCount > 5000 || columnCount > 100)
                    {
                        continue;
                    }

                    for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
                    {
                        var values = new List<string>(columnCount);
                        for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
                        {
                            values.Add(ReadText(grid.GetItem(rowIndex, columnIndex)) ?? string.Empty);
                        }

                        AddAttendanceRow(rows, values);
                    }
                }
                catch
                {
                }
            }

            foreach (var dataItem in elements.Where(element => IsControlType(element, ControlType.DataItem)))
            {
                var values = new List<string> { ReadText(dataItem) ?? string.Empty };
                values.AddRange(GetAllDescendants(dataItem).Select(ReadText).Where(text => !string.IsNullOrWhiteSpace(text)));
                AddAttendanceRow(rows, values);
            }

            var cells = elements
                .Select(element => new { Text = ReadText(element), Rect = TryGetBoundingRectangle(element) })
                .Where(cell => !string.IsNullOrWhiteSpace(cell.Text) && !cell.Rect.IsEmpty)
                .ToList();
            foreach (var codeCell in cells.Where(cell => !string.IsNullOrWhiteSpace(ExtractPractitionerCode(cell.Text))))
            {
                var centerY = codeCell.Rect.Top + (codeCell.Rect.Height / 2);
                var rowValues = cells
                    .Where(cell => Math.Abs((cell.Rect.Top + (cell.Rect.Height / 2)) - centerY) <= 8)
                    .OrderBy(cell => cell.Rect.Left)
                    .Select(cell => cell.Text)
                    .ToList();
                AddAttendanceRow(rows, rowValues);
            }

            return rows
                .Where(row => !string.IsNullOrWhiteSpace(row.PractitionerCode))
                .GroupBy(row => NormalizeCode(row.PractitionerCode))
                .Select(group => group.First())
                .ToList();
        }

        private static void AddAttendanceRow(List<AttendanceRowData> rows, IReadOnlyList<string> values)
        {
            if (TryParseAttendanceRow(values, out var row))
            {
                rows.Add(row);
            }
        }

        private static bool TryParseAttendanceRow(IReadOnlyList<string> values, out AttendanceRowData row)
        {
            row = new AttendanceRowData();
            if (values == null || values.Count < 3)
            {
                return false;
            }

            var timeIndexes = values
                .Select((value, index) => new { value, index })
                .Where(item => IsTimeOrMissingValue(item.value))
                .TakeLast(2)
                .ToList();
            if (timeIndexes.Count != 2)
            {
                return false;
            }

            var code = values
                .Take(timeIndexes[0].index)
                .Select(ExtractPractitionerCode)
                .LastOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            row = new AttendanceRowData
            {
                PractitionerCode = code,
                TimeIn = timeIndexes[0].value,
                TimeOut = timeIndexes[1].value
            };
            return true;
        }

        private static string ExtractPractitionerCode(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var match = Regex.Match(
                value,
                @"(?<![A-Z0-9._-])(?<code>[A-Z0-9._-]+/[A-Z0-9._-]+)(?![A-Z0-9._-])",
                RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["code"].Value.Trim() : string.Empty;
        }

        private static bool TryReadPractitionerRow(AutomationElement root, string practitionerCode, out string timeIn, out string timeOut)
        {
            timeIn = string.Empty;
            timeOut = string.Empty;
            var elements = GetAllDescendants(root);
            elements.Insert(0, root);

            foreach (var element in elements)
            {
                try
                {
                    if (!element.TryGetCurrentPattern(GridPattern.Pattern, out var gridObj) || gridObj is not GridPattern grid)
                    {
                        continue;
                    }

                    var rowCount = grid.Current.RowCount;
                    var columnCount = grid.Current.ColumnCount;
                    if (rowCount <= 0 || columnCount < 3 || rowCount > 5000 || columnCount > 100)
                    {
                        continue;
                    }

                    for (var row = 0; row < rowCount; row++)
                    {
                        var values = new List<string>();
                        for (var column = 0; column < columnCount; column++)
                        {
                            values.Add(ReadText(grid.GetItem(row, column)) ?? string.Empty);
                        }

                        if (TryExtractAttendanceFromValues(values, practitionerCode, out timeIn, out timeOut))
                        {
                            return true;
                        }
                    }
                }
                catch
                {
                }
            }

            if (TryReadRowByCoordinates(elements, practitionerCode, out timeIn, out timeOut))
            {
                return true;
            }

            foreach (var row in elements.Where(element => IsControlType(element, ControlType.DataItem)))
            {
                var values = new List<string> { ReadText(row) ?? string.Empty };
                values.AddRange(GetAllDescendants(row).Select(ReadText).Where(text => !string.IsNullOrWhiteSpace(text)));
                if (TryExtractAttendanceFromValues(values, practitionerCode, out timeIn, out timeOut))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadRowByCoordinates(
            IEnumerable<AutomationElement> elements,
            string practitionerCode,
            out string timeIn,
            out string timeOut)
        {
            timeIn = string.Empty;
            timeOut = string.Empty;
            var codeKey = NormalizeCode(practitionerCode);
            var cells = elements
                .Select(element => new { Text = ReadText(element), Rect = TryGetBoundingRectangle(element) })
                .Where(cell => !string.IsNullOrWhiteSpace(cell.Text) && !cell.Rect.IsEmpty)
                .ToList();

            var codeCell = cells.FirstOrDefault(cell => NormalizeCode(cell.Text).Contains(codeKey, StringComparison.OrdinalIgnoreCase));
            if (codeCell == null)
            {
                return false;
            }

            var centerY = codeCell.Rect.Top + (codeCell.Rect.Height / 2);
            var rowValues = cells
                .Where(cell => Math.Abs((cell.Rect.Top + (cell.Rect.Height / 2)) - centerY) <= 8)
                .OrderBy(cell => cell.Rect.Left)
                .Select(cell => cell.Text)
                .ToList();
            return TryExtractAttendanceFromValues(rowValues, practitionerCode, out timeIn, out timeOut);
        }

        private static bool TryFindAndCopyPractitionerRow(
            AutomationElement root,
            string practitionerCode,
            StringBuilder log,
            out string timeIn,
            out string timeOut)
        {
            timeIn = string.Empty;
            timeOut = string.Empty;
            var grid = FindByAutomationId(root, "gridControl1")
                ?? FindAllByControlType(root, ControlType.DataGrid)
                .Concat(FindAllByControlType(root, ControlType.Table))
                .OrderByDescending(element =>
                {
                    var rect = TryGetBoundingRectangle(element);
                    return rect.IsEmpty ? 0 : rect.Width * rect.Height;
                })
                .FirstOrDefault();

            if (grid == null)
            {
                grid = GetAllDescendants(root)
                    .Where(element =>
                    {
                        try { return element.Current.IsKeyboardFocusable; } catch { return false; }
                    })
                    .OrderByDescending(element =>
                    {
                        var rect = TryGetBoundingRectangle(element);
                        return rect.IsEmpty ? 0 : rect.Width * rect.Height;
                    })
                    .FirstOrDefault();
            }

            if (grid == null)
            {
                log.AppendLine("Attendance grid not found for clipboard fallback.");
                return false;
            }

            try
            {
                grid.SetFocus();
                Thread.Sleep(100);
                SendKeys.SendWait("^f");
                Thread.Sleep(150);
                Clipboard.SetText(practitionerCode);
                SendKeys.SendWait("^a");
                SendKeys.SendWait("^v");
                SendKeys.SendWait("{ENTER}");
                Thread.Sleep(500);

                if (TryReadPractitionerRow(grid, practitionerCode, out timeIn, out timeOut))
                {
                    return true;
                }

                var rect = TryGetBoundingRectangle(grid);
                if (!rect.IsEmpty)
                {
                    var x = (int)Math.Round(rect.Left + Math.Min(50, rect.Width / 4));
                    var y = (int)Math.Round(rect.Top + Math.Min(65, rect.Height / 3));
                    ClickAt(x, y);
                    Thread.Sleep(100);
                }

                const string marker = "__SUPPORT_EDIT_NO_ATTENDANCE_ROW__";
                Clipboard.SetText(marker);
                SendKeys.SendWait("^c");
                Thread.Sleep(200);
                var copied = Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
                if (string.IsNullOrWhiteSpace(copied) || copied == marker)
                {
                    return false;
                }

                log.AppendLine("Copied attendance row: " + copied.Replace("\r", " ").Replace("\n", " | "));
                foreach (var line in copied.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var values = line.Split('\t').Select(value => value.Trim()).ToList();
                    if (TryExtractAttendanceFromValues(values, practitionerCode, out timeIn, out timeOut))
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                log.AppendLine("Clipboard fallback failed: " + ex.Message);
            }

            return false;
        }

        private static bool TryExtractAttendanceFromValues(
            IReadOnlyList<string> values,
            string practitionerCode,
            out string timeIn,
            out string timeOut)
        {
            timeIn = string.Empty;
            timeOut = string.Empty;
            if (values == null || values.Count == 0)
            {
                return false;
            }

            var codeKey = NormalizeCode(practitionerCode);
            if (!values.Any(value => NormalizeCode(value).Contains(codeKey, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            var attendanceValues = values
                .Where(IsTimeOrMissingValue)
                .TakeLast(2)
                .ToList();
            if (attendanceValues.Count == 2)
            {
                timeIn = attendanceValues[0];
                timeOut = attendanceValues[1];
                return true;
            }

            if (values.Count >= 5)
            {
                timeIn = values[^2] ?? string.Empty;
                timeOut = values[^1] ?? string.Empty;
                return true;
            }

            return false;
        }

        private static AutomationElement FindButton(AutomationElement root, params string[] names)
        {
            if (root == null)
            {
                return null;
            }

            var wanted = names.Select(NormalizeSearch).ToList();
            return GetAllDescendants(root)
                .Where(element =>
                {
                    try
                    {
                        var controlType = element.Current.ControlType;
                        return controlType == ControlType.Button
                            || controlType == ControlType.MenuItem
                            || controlType == ControlType.Hyperlink
                            || controlType == ControlType.Custom;
                    }
                    catch
                    {
                        return false;
                    }
                })
                .OrderBy(element =>
                {
                    try { return element.Current.ControlType == ControlType.Button ? 0 : 1; }
                    catch { return 2; }
                })
                .FirstOrDefault(element =>
                {
                    var actual = NormalizeSearch(ReadText(element));
                    return wanted.Any(name => actual == name || actual.Contains(name, StringComparison.Ordinal));
                });
        }

        private static bool TryInvokeAccessibleAction(IntPtr windowHandle, string caption)
        {
            try
            {
                var iid = typeof(Accessibility.IAccessible).GUID;
                var result = NativeMethods.AccessibleObjectFromWindow(
                    windowHandle,
                    NativeMethods.OBJID_CLIENT,
                    ref iid,
                    out var accessibleObject);
                if (result < 0 || accessibleObject is not Accessibility.IAccessible root)
                {
                    return false;
                }

                var remainingNodes = 180;
                return TryInvokeAccessibleDescendant(root, NormalizeSearch(caption), 0, ref remainingNodes);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryInvokeAccessibleDescendant(
            Accessibility.IAccessible parent,
            string normalizedCaption,
            int depth,
            ref int remainingNodes)
        {
            if (parent == null || depth > 4 || remainingNodes <= 0)
            {
                return false;
            }

            int childCount;
            try { childCount = parent.accChildCount; }
            catch { return false; }
            if (childCount <= 0) return false;

            var requestedChildren = Math.Min(childCount, remainingNodes);
            var children = new object[requestedChildren];
            if (NativeMethods.AccessibleChildren(parent, 0, requestedChildren, children, out var obtained) < 0)
            {
                return false;
            }

            // Check all siblings first. Toolbar items are normally exposed directly at
            // a shallow level, so this finds btnCheckChamcong without entering the XML grids.
            for (var i = 0; i < obtained && remainingNodes > 0; i++, remainingNodes--)
            {
                var child = children[i];
                try
                {
                    var name = child is Accessibility.IAccessible accessible
                        ? accessible.get_accName(0)
                        : parent.get_accName(child);
                    if (NormalizeSearch(name) != normalizedCaption) continue;

                    if (child is Accessibility.IAccessible childAccessible)
                    {
                        childAccessible.accDoDefaultAction(0);
                    }
                    else
                    {
                        parent.accDoDefaultAction(child);
                    }

                    Thread.Sleep(30);
                    return true;
                }
                catch
                {
                }
            }

            for (var i = 0; i < obtained && remainingNodes > 0; i++)
            {
                if (children[i] is Accessibility.IAccessible childAccessible
                    && TryInvokeAccessibleDescendant(childAccessible, normalizedCaption, depth + 1, ref remainingNodes))
                {
                    return true;
                }
            }

            return false;
        }

        private static AutomationElement FindByAutomationId(AutomationElement root, string automationId)
        {
            if (root == null || string.IsNullOrWhiteSpace(automationId))
            {
                return null;
            }

            try
            {
                return root.FindFirst(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));
            }
            catch
            {
                return null;
            }
        }

        private static AutomationElement WaitForElementByAutomationId(AutomationElement root, string automationId, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var element = FindByAutomationId(root, automationId);
                if (element != null) return element;
                Thread.Sleep(50);
            }

            return null;
        }

        private static bool SetNativeDateTimePickerValue(AutomationElement datePicker, DateTime value)
        {
            try
            {
                var nativeHandle = datePicker.Current.NativeWindowHandle;
                if (nativeHandle == 0)
                {
                    return false;
                }

                return SetNativeDateTimePickerValue(new IntPtr(nativeHandle), value);
            }
            catch
            {
                return false;
            }
        }

        private static bool SetNativeDateTimePickerValue(IntPtr datePicker, DateTime value)
        {
            if (datePicker == IntPtr.Zero) return false;

            try
            {
                var systemTime = new NativeMethods.SYSTEMTIME
                {
                    Year = (ushort)value.Year,
                    Month = (ushort)value.Month,
                    Day = (ushort)value.Day,
                    DayOfWeek = (ushort)value.DayOfWeek,
                    Hour = (ushort)value.Hour,
                    Minute = (ushort)value.Minute,
                    Second = (ushort)value.Second,
                    Milliseconds = (ushort)value.Millisecond
                };

                NativeMethods.GetWindowThreadProcessId(datePicker, out var targetProcessId);
                if (targetProcessId == 0)
                {
                    return false;
                }

                // DTM_SETSYSTEMTIME is above WM_USER, so Windows does not marshal its
                // SYSTEMTIME pointer between processes. Passing a local ref directly
                // to the XML tool can dereference invalid memory and crash that tool.
                if (targetProcessId == (uint)Environment.ProcessId)
                {
                    var localResult = NativeMethods.SendMessage(
                        datePicker,
                        NativeMethods.DTM_SETSYSTEMTIME,
                        new IntPtr(NativeMethods.GDT_VALID),
                        ref systemTime);
                    Thread.Sleep(30);
                    return localResult != IntPtr.Zero;
                }

                return SetRemoteDateTimePickerValue(datePicker, targetProcessId, systemTime, value.Date);
            }
            catch
            {
                return false;
            }
        }

        private static bool SetRemoteDateTimePickerValue(
            IntPtr datePicker,
            uint targetProcessId,
            NativeMethods.SYSTEMTIME systemTime,
            DateTime expectedDate)
        {
            var process = NativeMethods.OpenProcess(
                NativeMethods.PROCESS_QUERY_INFORMATION
                | NativeMethods.PROCESS_VM_OPERATION
                | NativeMethods.PROCESS_VM_READ
                | NativeMethods.PROCESS_VM_WRITE,
                false,
                targetProcessId);
            if (process == IntPtr.Zero)
            {
                return false;
            }

            var size = (UIntPtr)(uint)Marshal.SizeOf<NativeMethods.SYSTEMTIME>();
            var remoteBuffer = IntPtr.Zero;
            try
            {
                remoteBuffer = NativeMethods.VirtualAllocEx(
                    process,
                    IntPtr.Zero,
                    size,
                    NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE,
                    NativeMethods.PAGE_READWRITE);
                if (remoteBuffer == IntPtr.Zero
                    || !NativeMethods.WriteProcessMemory(process, remoteBuffer, ref systemTime, size, out _))
                {
                    return false;
                }

                var setResult = NativeMethods.SendMessage(
                    datePicker,
                    NativeMethods.DTM_SETSYSTEMTIME,
                    new IntPtr(NativeMethods.GDT_VALID),
                    remoteBuffer);
                if (setResult == IntPtr.Zero)
                {
                    return false;
                }

                var getResult = NativeMethods.SendMessage(
                    datePicker,
                    NativeMethods.DTM_GETSYSTEMTIME,
                    IntPtr.Zero,
                    remoteBuffer);
                if (getResult.ToInt32() != NativeMethods.GDT_VALID)
                {
                    return false;
                }

                var actual = default(NativeMethods.SYSTEMTIME);
                if (!NativeMethods.ReadProcessMemory(process, remoteBuffer, ref actual, size, out _))
                {
                    return false;
                }

                Thread.Sleep(30);
                return actual.Year == expectedDate.Year
                    && actual.Month == expectedDate.Month
                    && actual.Day == expectedDate.Day;
            }
            finally
            {
                if (remoteBuffer != IntPtr.Zero)
                {
                    NativeMethods.VirtualFreeEx(process, remoteBuffer, UIntPtr.Zero, NativeMethods.MEM_RELEASE);
                }
                NativeMethods.CloseHandle(process);
            }
        }

        private static bool InvokeOrClick(AutomationElement element)
        {
            if (element == null)
            {
                return false;
            }

            try
            {
                if (element.TryGetCurrentPattern(InvokePattern.Pattern, out var invokeObj) && invokeObj is InvokePattern invoke)
                {
                    invoke.Invoke();
                    Thread.Sleep(200);
                    return true;
                }
            }
            catch
            {
            }

            var rect = TryGetBoundingRectangle(element);
            if (rect.IsEmpty)
            {
                return false;
            }

            ClickAt((int)Math.Round(rect.Left + rect.Width / 2), (int)Math.Round(rect.Top + rect.Height / 2));
            Thread.Sleep(200);
            return true;
        }

        private static bool SetValue(AutomationElement element, string value)
        {
            try
            {
                if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valueObj)
                    && valueObj is ValuePattern valuePattern
                    && !valuePattern.Current.IsReadOnly)
                {
                    valuePattern.SetValue(value);
                    Thread.Sleep(100);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool SetValueByKeyboard(AutomationElement element, string value, StringBuilder log, string fieldName)
        {
            try
            {
                element.SetFocus();
                Thread.Sleep(100);
                Clipboard.SetText(value);
                SendKeys.SendWait("^a");
                SendKeys.SendWait("^v");
                SendKeys.SendWait("{ENTER}");
                Thread.Sleep(150);
                log.AppendLine(fieldName + " set by keyboard: " + value);
                return true;
            }
            catch (Exception ex)
            {
                log.AppendLine(fieldName + " keyboard input failed: " + ex.Message);
                return false;
            }
        }

        private static List<AutomationElement> FindAllByControlType(AutomationElement root, ControlType controlType)
        {
            if (root == null)
            {
                return new List<AutomationElement>();
            }

            try
            {
                return root.FindAll(
                        TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.ControlTypeProperty, controlType))
                    .Cast<AutomationElement>()
                    .ToList();
            }
            catch
            {
                return new List<AutomationElement>();
            }
        }

        private static List<AutomationElement> GetAllDescendants(AutomationElement root)
        {
            if (root == null)
            {
                return new List<AutomationElement>();
            }

            try
            {
                return root.FindAll(TreeScope.Descendants, Condition.TrueCondition)
                    .Cast<AutomationElement>()
                    .ToList();
            }
            catch
            {
                return new List<AutomationElement>();
            }
        }

        private static string ReadText(AutomationElement element)
        {
            if (element == null)
            {
                return string.Empty;
            }

            try
            {
                if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valueObj) && valueObj is ValuePattern valuePattern)
                {
                    var value = valuePattern.Current.Value;
                    if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
                }
            }
            catch
            {
            }

            try
            {
                if (element.TryGetCurrentPattern(TextPattern.Pattern, out var textObj) && textObj is TextPattern textPattern)
                {
                    var value = textPattern.DocumentRange.GetText(-1);
                    if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
                }
            }
            catch
            {
            }

            try { return element.Current.Name?.Trim() ?? string.Empty; }
            catch { return string.Empty; }
        }

        private static string GetAutomationId(AutomationElement element)
        {
            try { return element.Current.AutomationId ?? string.Empty; }
            catch { return string.Empty; }
        }

        private static System.Windows.Rect TryGetBoundingRectangle(AutomationElement element)
        {
            try { return element.Current.BoundingRectangle; }
            catch { return System.Windows.Rect.Empty; }
        }

        private static string GetRuntimeIdKey(AutomationElement element)
        {
            try { return string.Join("-", element.GetRuntimeId()); }
            catch { return Guid.NewGuid().ToString("N"); }
        }

        private static string NormalizeDepartment(string value)
        {
            var normalized = NormalizeSearch(value);
            return normalized.StartsWith("khoa ", StringComparison.Ordinal)
                ? normalized.Substring(5).Trim()
                : normalized;
        }

        private static string NormalizeSearch(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (var c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(char.ToLowerInvariant(c == 'đ' || c == 'Đ' ? 'd' : c));
                }
            }

            return Regex.Replace(builder.ToString().Normalize(NormalizationForm.FormC), @"\s+", " ");
        }

        private static string NormalizeCode(string value)
        {
            return Regex.Replace(value ?? string.Empty, @"\s+", string.Empty).Trim().ToUpperInvariant();
        }

        private static string NormalizeAttendanceValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "N/A" : value.Trim();
        }

        private static bool IsMissingAttendanceValue(string value)
        {
            var normalized = NormalizeSearch(value).Replace(" ", string.Empty);
            return string.IsNullOrWhiteSpace(normalized) || normalized is "n/a" or "na" or "-";
        }

        private static bool HasValidAttendance(string timeIn, string timeOut)
        {
            return TryParseAttendanceTime(timeIn, out _)
                && TryParseAttendanceTime(timeOut, out var checkoutTime)
                && checkoutTime > MinimumCheckoutTime;
        }

        private static bool TryParseAttendanceTime(string value, out TimeSpan time)
        {
            time = default;
            if (IsMissingAttendanceValue(value)) return false;

            return TimeSpan.TryParseExact(
                value.Trim(),
                new[] { @"h\:mm", @"hh\:mm", @"h\:mm\:ss", @"hh\:mm\:ss" },
                CultureInfo.InvariantCulture,
                out time);
        }

        private static bool IsTimeOrMissingValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (IsMissingAttendanceValue(value)) return true;
            return Regex.IsMatch(value?.Trim() ?? string.Empty, @"^(?:[01]?\d|2[0-3]):[0-5]\d(?::[0-5]\d)?$");
        }

        private static IntPtr WaitForWindow(string titleKeyword, IntPtr excludedHandle, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var found = FindWindow(titleKeyword, excludedHandle);
                if (found != IntPtr.Zero)
                {
                    return found;
                }

                Thread.Sleep(150);
            }

            return IntPtr.Zero;
        }

        private static IntPtr FindWindow(string titleKeyword, IntPtr excludedHandle)
        {
            var wanted = NormalizeSearch(titleKeyword);
            var found = IntPtr.Zero;
            NativeMethods.EnumWindows((hWnd, _) =>
            {
                if (hWnd == excludedHandle || !NativeMethods.IsWindowVisible(hWnd)) return true;
                var title = GetWindowTitle(hWnd);
                if (NormalizeSearch(title).Contains(wanted, StringComparison.Ordinal))
                {
                    found = hWnd;
                    return false;
                }

                return true;
            }, IntPtr.Zero);
            return found;
        }

        private static string GetWindowTitle(IntPtr hWnd)
        {
            var length = NativeMethods.GetWindowTextLength(hWnd);
            if (length <= 0) return string.Empty;
            var title = new StringBuilder(length + 1);
            NativeMethods.GetWindowText(hWnd, title, title.Capacity);
            return title.ToString();
        }

        private static void ActivateWindow(IntPtr hWnd)
        {
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
            Thread.Sleep(80);
            NativeMethods.SetForegroundWindow(hWnd);
            Thread.Sleep(120);
        }

        private static void ClickAt(int x, int y)
        {
            NativeMethods.SetCursorPos(x, y);
            Thread.Sleep(40);
            NativeMethods.MouseEvent(NativeMethods.MOUSEEVENTF_LEFTDOWN, (uint)x, (uint)y, 0, IntPtr.Zero);
            Thread.Sleep(30);
            NativeMethods.MouseEvent(NativeMethods.MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, IntPtr.Zero);
        }

        private static void DoubleClickAt(int x, int y)
        {
            ClickAt(x, y);
            Thread.Sleep(50);
            ClickAt(x, y);
            Thread.Sleep(80);
        }

        private static class NativeMethods
        {
            public const int SW_SHOW = 5;
            public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
            public const uint MOUSEEVENTF_LEFTUP = 0x0004;
            public const int DTM_FIRST = 0x1000;
            public const int DTM_GETSYSTEMTIME = DTM_FIRST + 1;
            public const int DTM_SETSYSTEMTIME = DTM_FIRST + 2;
            public const int GDT_VALID = 0;
            public const uint PROCESS_VM_OPERATION = 0x0008;
            public const uint PROCESS_VM_READ = 0x0010;
            public const uint PROCESS_VM_WRITE = 0x0020;
            public const uint PROCESS_QUERY_INFORMATION = 0x0400;
            public const uint MEM_COMMIT = 0x1000;
            public const uint MEM_RESERVE = 0x2000;
            public const uint MEM_RELEASE = 0x8000;
            public const uint PAGE_READWRITE = 0x04;
            public const uint OBJID_CLIENT = 0xFFFFFFFC;
            public const int CB_GETCOUNT = 0x0146;
            public const int CB_GETLBTEXT = 0x0148;
            public const int CB_GETLBTEXTLEN = 0x0149;
            public const int CB_SETCURSEL = 0x014E;
            public const int CBN_SELCHANGE = 1;
            public const int WM_COMMAND = 0x0111;

            [StructLayout(LayoutKind.Sequential)]
            public struct POINT
            {
                public int X;
                public int Y;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int Left;
                public int Top;
                public int Right;
                public int Bottom;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct SYSTEMTIME
            {
                public ushort Year;
                public ushort Month;
                public ushort DayOfWeek;
                public ushort Day;
                public ushort Hour;
                public ushort Minute;
                public ushort Second;
                public ushort Milliseconds;
            }

            public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

            public static IntPtr MakeWParam(int lowWord, int highWord)
            {
                return new IntPtr((highWord << 16) | (lowWord & 0xFFFF));
            }

            [DllImport("user32.dll")]
            public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

            [DllImport("user32.dll")]
            public static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr lParam);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

            [DllImport("user32.dll")]
            public static extern int GetWindowTextLength(IntPtr hWnd);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);

            [DllImport("user32.dll")]
            public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);

            [DllImport("user32.dll")]
            public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

            [DllImport("user32.dll")]
            public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);

            [DllImport("user32.dll")]
            public static extern IntPtr GetParent(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern int GetDlgCtrlID(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool IsWindowVisible(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool ShowWindow(IntPtr hWnd, int command);

            [DllImport("user32.dll")]
            public static extern bool SetForegroundWindow(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool IsWindow(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

            [DllImport("user32.dll")]
            public static extern bool SetCursorPos(int x, int y);

            [DllImport("user32.dll", EntryPoint = "mouse_event")]
            public static extern void MouseEvent(uint flags, uint x, uint y, uint buttons, IntPtr extraInfo);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, StringBuilder lParam);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, ref SYSTEMTIME lParam);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr VirtualAllocEx(
                IntPtr process,
                IntPtr address,
                UIntPtr size,
                uint allocationType,
                uint protect);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool VirtualFreeEx(
                IntPtr process,
                IntPtr address,
                UIntPtr size,
                uint freeType);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool WriteProcessMemory(
                IntPtr process,
                IntPtr baseAddress,
                ref SYSTEMTIME buffer,
                UIntPtr size,
                out UIntPtr bytesWritten);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool ReadProcessMemory(
                IntPtr process,
                IntPtr baseAddress,
                ref SYSTEMTIME buffer,
                UIntPtr size,
                out UIntPtr bytesRead);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool CloseHandle(IntPtr handle);

            [DllImport("user32.dll")]
            public static extern bool IsWindowEnabled(IntPtr hWnd);

            [DllImport("oleacc.dll")]
            public static extern int AccessibleObjectFromWindow(
                IntPtr hWnd,
                uint objectId,
                ref Guid interfaceId,
                [MarshalAs(UnmanagedType.Interface)] out object accessibleObject);

            [DllImport("oleacc.dll")]
            public static extern int AccessibleObjectFromPoint(
                POINT point,
                [MarshalAs(UnmanagedType.Interface)] out Accessibility.IAccessible accessibleObject,
                [MarshalAs(UnmanagedType.Struct)] out object childId);

            [DllImport("oleacc.dll")]
            public static extern int AccessibleChildren(
                Accessibility.IAccessible accessibleContainer,
                int childStart,
                int childCount,
                [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] object[] children,
                out int childrenObtained);
        }
    }
}
