using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SupportEdit.Services;
using SupportEdit.UI;
using SupportEdit.Models;
using MessageBox = SupportEdit.UI.SupportMessageBox;

namespace SupportEdit
{
    public partial class Form1 : Form
    {
        // Controls declared in Designer but assigned at runtime
        private TextBox txtErrorRaw;

        private Button btnReadError;
        private FlowLayoutPanel pnlErrors;
        private TextBox txtLog;

        private ConfigService _config;

        private AutomationService _automation;
        private AutomationUiService _automationUi;
        private BackupService _backup;
        private VasSyncService _vasSync;
        private GdFormAutomationService _gdFormAutomation;
        private AttendanceCheckService _attendanceCheck;
        private Win32InputService _win32Input;

        public Form1()
        {
            InitializeComponent();
            TopMost = false;

            // init services
            _config = new ConfigService();
            _config.Load();
            _automation = new AutomationService(_config);
            _automationUi = new AutomationUiService(_config);
            _backup = new BackupService();
            _win32Input = new Win32InputService(_config);
            _vasSync = new VasSyncService(_config, _win32Input);
            _gdFormAutomation = new GdFormAutomationService(_config, _automation);
            _attendanceCheck = new AttendanceCheckService(_config, _automation);

            // wire events (designer assigned fields)
            this.txtErrorRaw = (TextBox)Controls.Find("txtErrorRaw", true).FirstOrDefault();
            this.btnReadError = (Button)Controls.Find("btnReadError", true).FirstOrDefault();
            this.pnlErrors = (FlowLayoutPanel)Controls.Find("pnlErrors", true).FirstOrDefault();
            this.txtLog = (TextBox)Controls.Find("txtLog", true).FirstOrDefault();

            if (btnReadError != null) btnReadError.Click += BtnReadError_Click;
            btnPortalFix.Click += BtnPortalFix_Click;

            // wire test and debug menu
            WireMenuEvents();
            EnsureDongBoVasButton();

            // ensure DebugReads folder exists and log path
            try
            {
                var dbgFolder = SupportEdit.Services.DebugLogService.GetDebugReadsFolder();
                Log("DebugReads folder: " + dbgFolder);
            }
            catch { }

            // Log current mode flags and config paths
            try
            {
                Log($"UseFastDirectEdit = {_config.UseFastDirectEdit}");
                Log($"UseFastMouseFocusOnly = {_config.UseFastMouseFocusOnly}");
                Log($"UseUiAutomationFocusForXml = {_config.UseUiAutomationFocusForXml}");
                Log($"UseDirectFindThenLocalScanEdit = {_config.UseDirectFindThenLocalScanEdit}");
                Log($"UseDirectIndexScanEdit = {_config.UseDirectIndexScanEdit}");
                Log($"Xml3ClickOffsetX = {_config.Xml3ClickOffsetX}, Xml3ClickOffsetY = {_config.Xml3ClickOffsetY}");
                Log($"appconfig.json path: {_config.ConfigFilePath}");
                Log($"DebugReads folder: {SupportEdit.Services.DebugLogService.GetDebugReadsFolder()}");
                // warn if multiple modes enabled
                if (_config.UseFastDirectEdit && _config.UseDirectFindThenLocalScanEdit && _config.UseDirectIndexScanEdit)
                {
                    Log("WARNING: Multiple modes enabled; prioritizing UseFastDirectEdit -> UseDirectFindThenLocalScanEdit -> UseDirectIndexScanEdit.");
                }
            }
            catch { }

            // ensure we can try UI Automation at startup to find main window (non-blocking)
            try { _automationUi.GetMainWindow(); } catch { }
        }

        private void WireMenuEvents()
        {
            var menu = Controls.OfType<MenuStrip>().FirstOrDefault();
            if (menu == null) return;

            var menuConfig = menu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text == "Cấu hình");
            var searchMenu = menu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text == "Tìm kiếm hồ sơ");
            var debugMenu = menu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text == "Debug");

            var findDischargeDate = searchMenu?.DropDownItems
                .OfType<ToolStripMenuItem>()
                .FirstOrDefault(m => m.Text == "Tìm ngày ra viện");
            if (findDischargeDate != null)
            {
                findDischargeDate.Click -= FindDischargeDate_Click;
                findDischargeDate.Click += FindDischargeDate_Click;
            }

            var miPract = menuConfig?.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text == "Quản lý người thực hiện");
            if (miPract != null)
            {
                miPract.Click -= MiPract_Click;
                miPract.Click += MiPract_Click;
            }

            var miTitle = menuConfig?.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text == "Cấu hình tiêu đề cửa sổ phần mềm XML");
            if (miTitle != null)
            {
                miTitle.Click -= MiTitle_Click;
                miTitle.Click += MiTitle_Click;
            }

            if (debugMenu != null)
            {
                var testXml3 = debugMenu.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text == "Test đọc XML3");
                if (testXml3 != null) testXml3.Click += TestXml3_Click;

                var openDebug = debugMenu.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text == "Mở DebugReads");
                if (openDebug != null) openDebug.Click += OpenDebugReads_Click;

                var testReadVas = debugMenu.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text == "Test đọc VAS");
                if (testReadVas == null)
                {
                    testReadVas = new ToolStripMenuItem("Test đọc VAS");
                    debugMenu.DropDownItems.Add(testReadVas);
                }
                testReadVas.Click -= TestReadVas_Click;
                testReadVas.Click += TestReadVas_Click;

                var testOpenGd = debugMenu.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text == "Test mở GDForm");
                if (testOpenGd == null)
                {
                    testOpenGd = new ToolStripMenuItem("Test mở GDForm");
                    debugMenu.DropDownItems.Add(testOpenGd);
                }
                testOpenGd.Click -= TestOpenGdForm_Click;
                testOpenGd.Click += TestOpenGdForm_Click;

                var testPasteGd = debugMenu.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text == "Test paste GDForm");
                if (testPasteGd == null)
                {
                    testPasteGd = new ToolStripMenuItem("Test paste GDForm");
                    debugMenu.DropDownItems.Add(testPasteGd);
                }
                testPasteGd.Click -= TestPasteGdForm_Click;
                testPasteGd.Click += TestPasteGdForm_Click;

                var saveVasTabPos = menuSavePosition.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text == "Lưu vị trí tab Thành công VAS");
                if (saveVasTabPos == null)
                {
                    saveVasTabPos = new ToolStripMenuItem("Lưu vị trí tab Thành công VAS");
                    menuSavePosition.DropDownItems.Add(saveVasTabPos);
                }
                saveVasTabPos.Click -= SaveVasSuccessTabPosition_Click;
                saveVasTabPos.Click += SaveVasSuccessTabPosition_Click;

                var testClickVasLog = debugMenu.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text == "Test click VAS log");
                if (testClickVasLog == null)
                {
                    testClickVasLog = new ToolStripMenuItem("Test click VAS log");
                    debugMenu.DropDownItems.Add(testClickVasLog);
                }
                testClickVasLog.Click -= TestClickVasLog_Click;
                testClickVasLog.Click += TestClickVasLog_Click;

                var testManualVas = debugMenu.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text == "Test copy VAS manual sequence");
                if (testManualVas == null)
                {
                    testManualVas = new ToolStripMenuItem("Test copy VAS manual sequence");
                    debugMenu.DropDownItems.Add(testManualVas);
                }
                testManualVas.Click -= TestCopyVasManual_Click;
                testManualVas.Click += TestCopyVasManual_Click;

                var testCopyVasPasteGd = debugMenu.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text == "Test copy VAS paste GDForm");
                if (testCopyVasPasteGd == null)
                {
                    testCopyVasPasteGd = new ToolStripMenuItem("Test copy VAS paste GDForm");
                    debugMenu.DropDownItems.Add(testCopyVasPasteGd);
                }
                testCopyVasPasteGd.Click -= TestCopyVasPasteGdForm_Click;
                testCopyVasPasteGd.Click += TestCopyVasPasteGdForm_Click;

                var testVasOffsets = debugMenu.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text == "Test VAS offsets");
                if (testVasOffsets == null)
                {
                    testVasOffsets = new ToolStripMenuItem("Test VAS offsets");
                    debugMenu.DropDownItems.Add(testVasOffsets);
                }
                testVasOffsets.Click -= TestVasOffsets_Click;
                testVasOffsets.Click += TestVasOffsets_Click;

                var saveVasLogPos = menuSavePosition.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text == "Lưu vị trí khung log VAS");
                if (saveVasLogPos == null)
                {
                    saveVasLogPos = new ToolStripMenuItem("Lưu vị trí khung log VAS");
                    menuSavePosition.DropDownItems.Add(saveVasLogPos);
                }
                saveVasLogPos.Click -= SaveVasLogPosition_Click;
                saveVasLogPos.Click += SaveVasLogPosition_Click;

                var saveMaGDPos = menuSavePosition.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text == "Lưu vị trí nút Mã GD");
                if (saveMaGDPos == null)
                {
                    saveMaGDPos = new ToolStripMenuItem("Lưu vị trí nút Mã GD");
                    menuSavePosition.DropDownItems.Add(saveMaGDPos);
                }
                saveMaGDPos.Click -= SaveMaGDPosition_Click;
                saveMaGDPos.Click += SaveMaGDPosition_Click;

                var saveTxtBodyPos = menuSavePosition.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text == "Lưu vị trí txtbody GDForm");
                if (saveTxtBodyPos == null)
                {
                    saveTxtBodyPos = new ToolStripMenuItem("Lưu vị trí txtbody GDForm");
                    menuSavePosition.DropDownItems.Add(saveTxtBodyPos);
                }
                saveTxtBodyPos.Click -= SaveGdTxtBodyPosition_Click;
                saveTxtBodyPos.Click += SaveGdTxtBodyPosition_Click;

                var saveConvertPos = menuSavePosition.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text == "Lưu vị trí nút Chuyển dữ liệu");
                if (saveConvertPos == null)
                {
                    saveConvertPos = new ToolStripMenuItem("Lưu vị trí nút Chuyển dữ liệu");
                    menuSavePosition.DropDownItems.Add(saveConvertPos);
                }
                saveConvertPos.Click -= SaveGdConvertButtonPosition_Click;
                saveConvertPos.Click += SaveGdConvertButtonPosition_Click;

                var testMode = debugMenu.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text == "Test mode hiện tại");
                if (testMode == null)
                {
                    testMode = new ToolStripMenuItem("Test mode hiện tại");
                    debugMenu.DropDownItems.Add(testMode);
                }
                testMode.Click -= TestMode_Click;
                testMode.Click += TestMode_Click;

                var testTiming = debugMenu.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text == "Test timing sửa NGAY_TH_YL");
                if (testTiming == null)
                {
                    testTiming = new ToolStripMenuItem("Test timing sửa NGAY_TH_YL");
                    debugMenu.DropDownItems.Add(testTiming);
                }
                testTiming.Click -= TestTimingSuaNgayThYl_Click;
                testTiming.Click += TestTimingSuaNgayThYl_Click;

                var saveXmlPos = menuSavePosition.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text == "Lưu vị trí click XML hiện tại");
                if (saveXmlPos == null)
                {
                    saveXmlPos = new ToolStripMenuItem("Lưu vị trí click XML hiện tại");
                    menuSavePosition.DropDownItems.Add(saveXmlPos);
                }
                saveXmlPos.Click -= SaveXml3ClickPosition_Click;
                saveXmlPos.Click += SaveXml3ClickPosition_Click;

                var testGoTo = debugMenu.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text == "Test GoTo STT Fast");
                if (testGoTo == null)
                {
                    testGoTo = new ToolStripMenuItem("Test GoTo STT Fast");
                    debugMenu.DropDownItems.Add(testGoTo);
                }
                testGoTo.Click -= TestGoToSttFast_Click;
                testGoTo.Click += TestGoToSttFast_Click;
            }
        }

        private void MiPract_Click(object sender, EventArgs e)
        {
            var cfg = new ConfigService();
            cfg.Load();
            var f = new PractitionerManagerForm(cfg) { TopMost = true };
            f.ShowDialog(this);
            _config.Load();
        }

        private void FindDischargeDate_Click(object sender, EventArgs e)
        {
            using var form = new DischargeDateSearchForm();
            form.ShowDialog(this);
        }

        private void MiTitle_Click(object sender, EventArgs e)
        {
            var cfg = new ConfigService();
            cfg.Load();
            var f = new AppSettingsForm(cfg) { TopMost = true };
            f.ShowDialog(this);
            _config.Load();
        }

        private void Log(string message)
        {
            if (txtLog == null) return;
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action(() => { txtLog.AppendText(message + Environment.NewLine); }));
            }
            else
            {
                txtLog.AppendText(message + Environment.NewLine);
            }
        }

        private void BtnReadError_Click(object sender, EventArgs e)
        {
            var raw = txtErrorRaw?.Text ?? string.Empty;
            // Thử đọc lỗi trực tiếp từ control txtError của app XML bằng AutomationId trước khi dùng textbox manual
            try
            {
                var ext = _automationUi.ReadErrorText();
                if (!string.IsNullOrEmpty(ext))
                {
                    Log("Read error text from external app via AutomationId");
                    raw = ext;
                    txtErrorRaw.Text = raw;
                }
                else
                {
                    Log("Could not read error control via AutomationId; fallback to manual paste textbox");
                }
            }
            catch (Exception ex)
            {
                Log("Error reading external error control: " + ex.Message);
            }

            var items = ErrorParser.Parse(raw);
            var groups = items.GroupBy(i => (i.Xml, i.Stt))
                              .Select(g => new XmlErrorGroup { Xml = g.Key.Xml, Stt = g.Key.Stt, Items = g.ToList() })
                              .ToList();

            pnlErrors.Controls.Clear();
            foreach (var g in groups)
            {
                var card = new ErrorCard(g, _automation, _config);
                card.OnGoto += Card_OnGoto;
                card.OnFixNgay += group => RunCancellableFix(() => Card_OnFixNgay(group));
                card.OnFixNguoi += group => RunCancellableFix(() => Card_OnFixNguoi(group));
                card.OnFixTBncct += group => RunCancellableFix(() => Card_OnFixTBncct(group));
                card.OnFixMucHuong += group => RunCancellableFix(() => Card_OnFixMucHuong(group));
                card.OnFixNgayTToan += group => RunCancellableFix(() => Card_OnFixNgayTToan(group));
                card.OnFixSoCccd += group => RunCancellableFix(() => Card_OnFixSoCccd(group));
                card.OnFixAll += group => RunCancellableFix(() => Card_OnFixAll(group));
                pnlErrors.Controls.Add(card);
            }

            Log($"Parsed {items.Count} items into {groups.Count} groups");
        }

        private void RunCancellableFix(Action action)
        {
            AutomationCancellation.Begin();
            try
            {
                action();
                AutomationCancellation.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                Log("Đã ngừng sửa lỗi theo yêu cầu (Shift + Esc).");
                MessageBox.Show("Đã ngừng sửa lỗi.", "Đã hủy", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                AutomationCancellation.End();
            }
        }

        private void BtnPortalFix_Click(object sender, EventArgs e)
        {
            using var form = new PortalFixForm(
                () => RunCancellableFix(FixPortalMaBenhKt));
            form.ShowDialog(this);
        }

        private void FixPortalMaBenhKt()
        {
            const int xmlNumber = 1;
            try
            {
                Log("Clear MA_BENH_KT for XML1 from Portal Fix.");

                var h = _automation.FindWindowByTitleContains(_config.XmlAppWindowTitleKeyword);
                if (h == IntPtr.Zero)
                {
                    MessageBox.Show($"Không tìm thấy cửa sổ phần mềm XML chứa tiêu đề '{_config.XmlAppWindowTitleKeyword}'.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (!_automation.ActivateWindow(h))
                {
                    MessageBox.Show("Không thể kích hoạt cửa sổ phần mềm XML.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                _automationUi.SyncMainWindowHandle(h);

                if (_automationUi.FastDirectClearMaBenhKt(xmlNumber))
                {
                    Log("FastDirectClearMaBenhKt completed for XML1.");
                    return;
                }

                var logSb = new StringBuilder();
                logSb.AppendLine("ACTION: FastDirectClearMaBenhKt_Failed");
                logSb.AppendLine("XML number: 1");
                logSb.AppendLine($"Start time: {DateTime.Now:O}");
                var path = DebugLogService.SaveDebugLog("XML1_FastDirect_ClearMaBenhKt_Failed", logSb.ToString());
                MessageBox.Show($"Xóa MA_BENH_KT thất bại. Debug log saved: {path}", "Sửa lỗi trên Cổng", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log("FastDirectClearMaBenhKt failed; debug saved: " + path);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log("Error clearing MA_BENH_KT: " + ex.Message);
                MessageBox.Show("Không thể sửa MA_BENH_KT: " + ex.Message, "Sửa lỗi trên Cổng", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Card_OnGoto(XmlErrorGroup g)
        {
            try
            {
                Log($"Going to XML{g.Xml}" + (g.Stt > 0 ? $" STT {g.Stt}" : ""));
                var h = _automation.FindWindowByTitleContains(_config.XmlAppWindowTitleKeyword);
                if (h == IntPtr.Zero)
                {
                    MessageBox.Show($"Không tìm thấy cửa sổ phần mềm XML chứa tiêu đề '{_config.XmlAppWindowTitleKeyword}'");
                    return;
                }
                Log("Found external app window, activating...");
                if (!_automation.ActivateWindow(h))
                {
                    MessageBox.Show("Không thể kích hoạt cửa sổ phần mềm XML.");
                    return;
                }

                // Sync handle so AutomationUiService does not need to search again
                _automationUi.SyncMainWindowHandle(h);

                Log("Using GoToSttFast flow: focus XML, Ctrl+F to STT, then stop.");
                var ok = g.Stt > 0
                    ? _automationUi.GoToSttFast(g.Xml, g.Stt)
                    : _automationUi.GoToErrorFast(g.Xml, g.Stt, string.Empty);
                if (ok)
                {
                    Log($"Successfully navigated to XML{g.Xml}" + (g.Stt > 0 ? $" STT {g.Stt}" : "") + ".");
                }
                else
                {
                    Log($"ERROR: GoToSttFast failed for XML{g.Xml}" + (g.Stt > 0 ? $" STT {g.Stt}" : "") + ".");
                    MessageBox.Show($"Không thể đi đến lỗi trong XML{g.Xml}. Hãy kiểm tra DebugReads folder để xem chi tiết lỗi.", "Đi đến thất bại", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log("Error in goto: " + ex.Message);
            }
        }

        private void Card_OnFixNgay(XmlErrorGroup g)
        {
            try
            {
                Log($"Fix NGAY for XML{g.Xml} STT {g.Stt}");
                // find external app window
                var h2 = _automation.FindWindowByTitleContains(_config.XmlAppWindowTitleKeyword);
                if (h2 == IntPtr.Zero)
                {
                    MessageBox.Show($"Không tìm thấy cửa sổ phần mềm XML chứa tiêu đề '{_config.XmlAppWindowTitleKeyword}'.\n\nGợi ý: Hãy chắc chắn phần mềm XML đang chạy và sửa lỗi trước.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                Log("Found external app window, activating...");
                if (!_automation.ActivateWindow(h2))
                {
                    MessageBox.Show("Không thể kích hoạt cửa sổ phần mềm XML.\n\nGợi ý: Hãy chắc chắn SupportEdit không bị chặn bởi phần mềm khác.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Sync handle so AutomationUiService does not need to search again
                _automationUi.SyncMainWindowHandle(h2);

                // If configured to use FastDirectEdit, use it first (NEW FAST MODE); else DirectFindThenLocalScanEdit; else DirectIndexScanEdit; else whole-XML
                if (_config.UseFastDirectEdit)
                {
                    Log("Đang dùng UseFastDirectEdit (Fast Direct Edit Mode): Ctrl+F đến STT, scan tối đa 80 dòng, timeout 15s.");
                    var ok = _automationUi.FastDirectFixNgayThYl(g.Xml, g.Stt);
                    if (ok)
                    {
                        Log($"FastDirectFixNgayThYl completed for XML{g.Xml} STT{g.Stt}");
                        return;
                    }
                    else
                    {
                        var logSb = new System.Text.StringBuilder();
                        logSb.AppendLine("ACTION: FastDirectFixNgayThYl_Failed");
                        logSb.AppendLine($"XML number: {g.Xml}");
                        logSb.AppendLine($"Target STT: {g.Stt}");
                        logSb.AppendLine($"Start time: {DateTime.Now:O}");
                        var path = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{g.Xml}_STT{g.Stt}_FastDirect_FixNgayThYl_Failed", logSb.ToString());
                        MessageBox.Show($"FastDirectEditMode failed. Debug log saved: {path}", "Fast Direct Edit Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Log("FastDirectFixNgayThYl failed; debug saved: " + path);
                        return;
                    }
                }
                else if (_config.UseDirectFindThenLocalScanEdit)
                {
                    Log("Đang dùng DirectFindThenLocalScanEdit: Ctrl+F đến STT, đóng Find, scan cục bộ trong block.");
                    var ok = _automationUi.DirectFindThenLocalFixNgayThYl(g.Xml, g.Stt);
                    if (ok)
                    {
                        Log($"DirectFindThenLocalFixNgayThYl completed for XML{g.Xml} STT{g.Stt}");
                        return;
                    }
                    else
                    {
                        var logSb = new System.Text.StringBuilder();
                        logSb.AppendLine("ACTION: DirectFindThenLocalFixNgayThYl_Failed");
                        logSb.AppendLine($"XML number: {g.Xml}");
                        logSb.AppendLine($"Target STT: {g.Stt}");
                        logSb.AppendLine($"Start time: {DateTime.Now:O}");
                        var path = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{g.Xml}_STT{g.Stt}_DirectFind_LocalFixNgayThYl_Failed", logSb.ToString());
                        MessageBox.Show($"DirectFindThenLocalScanEdit failed. Debug log saved: {path}", "Direct Edit Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Log("DirectFindThenLocalFixNgayThYl failed; debug saved: " + path);
                        return;
                    }
                }
                else if (_config.UseDirectIndexScanEdit)
                {
                    Log("UseDirectIndexScanEdit = true, bỏ qua toàn bộ whole-XML flow.");
                    Log("Đang sửa trực tiếp trên giao diện bằng scan từng dòng.");
                    Log("Không sử dụng Find, không Ctrl+A toàn bộ XML.");
                    var ok = _automationUi.DirectIndexFixNgayThYl(g.Xml, g.Stt);
                    if (ok)
                    {
                        Log($"DirectIndexFixNgayThYl completed for XML{g.Xml} STT{g.Stt}");
                        return;
                    }
                    else
                    {
                        // Save debug log
                        var logSb = new System.Text.StringBuilder();
                        logSb.AppendLine("ACTION: DirectIndexFixNgayThYl_Failed");
                        logSb.AppendLine($"XML number: {g.Xml}");
                        logSb.AppendLine($"Target STT: {g.Stt}");
                        logSb.AppendLine($"Start time: {DateTime.Now:O}");
                        var path = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{g.Xml}_STT{g.Stt}_DirectIndexFixNgayThYl_Failed", logSb.ToString());
                        MessageBox.Show($"DirectIndexScanEdit failed. Debug log saved: {path}", "Direct Edit Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Log("DirectIndexFixNgayThYl failed; debug saved: " + path);
                        return;
                    }
                }

                // Copy with enhanced diagnostics
                Log("Copying XML content from external app...");
                var clip = _automation.CopyCurrentXmlFromExternalApp(h2);
                if (string.IsNullOrEmpty(clip))
                {
                    var msg = "Không copy được nội dung XML từ ứng dụng ngoài.\n\n" +
                              "Các nguyên nhân có thể:\n" +
                              "• Phần mềm XML không phải là cửa sổ hoạt động\n" +
                              "• Clipboard bị khóa hoặc chậm\n" +
                              "• SupportEdit cần chạy với quyền Administrator\n" +
                              "• Nội dung XML quá lớn (thử gửi nhiều hơn)\n\n" +
                              "Gợi ý:\n" +
                              "1. Click nút 'Debug → Test đọc XML3' để kiểm tra chi tiết\n" +
                              "2. Nếu dùng SupportEdit và phần mềm XML cùng trên một máy tính, hãy chạy cả hai với quyền Administrator";
                    try
                    {
                        var log = new System.Text.StringBuilder();
                        log.AppendLine("ACTION: Copy XML from external app");
                        log.AppendLine($"XML Number: {g.Xml}");
                        log.AppendLine($"STT: {g.Stt}");
                        log.AppendLine($"BaseDirectory: {AppContext.BaseDirectory}");
                        log.AppendLine($"DebugReadsFolder: {SupportEdit.Services.DebugLogService.GetDebugReadsFolder()}");
                        var focusInfo = _automationUi.GetFocusedElementInfo();
                        log.AppendLine("FocusedElement:");
                        foreach (var kvp in focusInfo) log.AppendLine($"{kvp.Key}: {kvp.Value}");
                        var path = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{g.Xml}_STT{g.Stt}_CopyFail", log.ToString());
                        Log("Saved debug log: " + path);
                    }
                    catch { }

                    MessageBox.Show(msg, "Lỗi Copy XML", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                Log($"Successfully read XML{g.Xml} ({clip.Length} bytes)");

                // Validate XML content before calling XmlFixService
                var v = ValidateXmlContentForFix(clip, g.Xml, g.Stt);
                if (!v.IsValid)
                {
                    // Ask user to try Direct UI Edit if configured
                    if (_config.UseDirectUiEditFallback)
                    {
                        var ask = MessageBox.Show("Không sửa được bằng XML text. Bạn có muốn thử sửa trực tiếp trên giao diện không?", "Direct UI Edit", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (ask == DialogResult.Yes)
                        {
                            // attempt direct edit for NGAY_TH_YL
                    var directOk = _automationUi.DirectFixNgayThYl(g.Xml, g.Stt);
                    if (directOk)
                    {
                        Log($"Direct UI edit attempted for XML{g.Xml} STT{g.Stt}");
                        return;
                    }
                            else
                            {
                                Log("Direct UI edit failed or was cancelled by user");
                            }
                        }
                    }

                    // Save debug files
                    try
                    {
                        var txtPath = SupportEdit.Services.DebugLogService.SaveDebugText($"XML{g.Xml}_STT{g.Stt}_ReadContent", clip);
                        var logSb = new System.Text.StringBuilder();
                        logSb.AppendLine($"Length: {v.Length}");
                        logSb.AppendLine($"HasDvktOpenTag: {v.HasDvktOpenTag}");
                        logSb.AppendLine($"HasDvktCloseTag: {v.HasDvktCloseTag}");
                        logSb.AppendLine($"HasTargetStt: {v.HasTargetStt}");
                        var first = clip == null ? string.Empty : clip.Substring(0, Math.Min(500, clip.Length));
                        var last = clip == null ? string.Empty : clip.Substring(Math.Max(0, clip.Length - 500), Math.Min(500, clip.Length));
                        logSb.AppendLine("--- First 500 chars ---");
                        logSb.AppendLine(first);
                        logSb.AppendLine("--- Last 500 chars ---");
                        logSb.AppendLine(last);
                        var focusInfo = _automationUi.GetFocusedElementInfo();
                        logSb.AppendLine("--- FocusedElementInfo ---");
                        foreach (var kvp in focusInfo) logSb.AppendLine($"{kvp.Key}: {kvp.Value}");
                        var logPath = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{g.Xml}_STT{g.Stt}_ValidateFail", logSb.ToString());
                        MessageBox.Show($"Nội dung XML đọc không hợp lệ cho việc sửa. Debug saved: {txtPath} and {logPath}", "Validation failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Log("Saved debug reads: " + txtPath + " and " + logPath);
                    }
                    catch (Exception ex)
                    {
                        Log("Failed to write debug files: " + ex.Message);
                    }
                    return;
                }

                var result = XmlFixService.FixNgayThYl(clip, g.Xml, g.Stt, false);
                // Nếu cần xác nhận ghi đè
                if (result.RequireOverwriteConfirmation && result.FieldsNeedOverwriteConfirmation.Contains("NGAY_TH_YL"))
                {
                    var oldVal = result.ExistingValues.ContainsKey("NGAY_TH_YL") ? result.ExistingValues["NGAY_TH_YL"] : "";
                    var ans = MessageBox.Show($"NGAY_TH_YL đã có giá trị: {oldVal}. Bạn có muốn ghi đè không?", "Xác nhận ghi đè", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (ans == DialogResult.Yes)
                    {
                        Log($"User chose to overwrite NGAY_TH_YL for XML{g.Xml} STT{g.Stt}");
                        result = XmlFixService.FixNgayThYl(clip, g.Xml, g.Stt, true);
                    }
                    else
                    {
                        Log($"User cancelled overwrite NGAY_TH_YL for XML{g.Xml} STT{g.Stt}");
                        return;
                    }
                }
                if (!result.Success)
                {
                    MessageBox.Show(string.Join("\n", result.Messages));
                    return;
                }

                var backupPath = _backup.SaveBackup(clip, g.Xml, g.Stt);

                // show preview form
                var preview = new PreviewFixForm(clip, result.FixedXml, result.FixedFields, result.Messages);
                if (preview.ShowDialog() != DialogResult.OK)
                {
                    Log("User cancelled preview paste");
                    return;
                }

                // paste back
                var pasted = _automation.PasteXmlToExternalApp(h2, result.FixedXml);
                Log($"Fixed NGAY_TH_YL XML{g.Xml} STT{g.Stt}. Backup: {backupPath}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                try
                {
                    var log = new System.Text.StringBuilder();
                    log.AppendLine("ACTION: Fix NGAY_TH_YL");
                    log.AppendLine($"XML Number: {g.Xml}");
                    log.AppendLine($"STT: {g.Stt}");
                    log.AppendLine($"BaseDirectory: {AppContext.BaseDirectory}");
                    log.AppendLine($"DebugReadsFolder: {SupportEdit.Services.DebugLogService.GetDebugReadsFolder()}");
                    log.AppendLine("FocusedElement:");
                    try
                    {
                        var fi = _automationUi.GetFocusedElementInfo();
                        foreach (var kvp in fi) log.AppendLine($"{kvp.Key}: {kvp.Value}");
                    }
                    catch { }
                    log.AppendLine("Exception:");
                    log.AppendLine(ex.ToString());

                    var logPath = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{g.Xml}_STT{g.Stt}_FixNgayThYl_Exception", log.ToString());
                    MessageBox.Show($"Có lỗi khi sửa NGAY_TH_YL.\nLog đã lưu tại:\n{logPath}");
                }
                catch { }

                Log("Error in fix ngay: " + ex.Message);
            }
        }

        private void Card_OnFixNguoi(XmlErrorGroup g)
        {
            try
            {
                Log($"Fix NGUOI for XML{g.Xml} STT {g.Stt}");
                // Find + activate external app window before any automation (same as Card_OnFixNgay)
                var hNguoi = _automation.FindWindowByTitleContains(_config.XmlAppWindowTitleKeyword);
                if (hNguoi == IntPtr.Zero)
                {
                    MessageBox.Show($"Không tìm thấy cửa sổ phần mềm XML chứa tiêu đề '{_config.XmlAppWindowTitleKeyword}'.\n\nGợi ý: Hãy chắc chắn phần mềm XML đang chạy.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (!_automation.ActivateWindow(hNguoi))
                {
                    MessageBox.Show("Không thể kích hoạt cửa sổ phần mềm XML.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                // Sync handle so AutomationUiService does not need to search again
                _automationUi.SyncMainWindowHandle(hNguoi);

                string code;
                PractitionerItem practitioner;
                string ngayThYlForAttendance = null;
                _config.Load();
                Log($"AutoFixNguoiThucHien = {_config.AutoFixNguoiThucHien}");
                Log($"CheckAttendanceBeforePractitionerFix = {_config.CheckAttendanceBeforePractitionerFix}");
                Log($"AutoReplaceAbsentPractitioner = {_config.AutoReplaceAbsentPractitioner}");

                if (_config.CheckAttendanceBeforePractitionerFix)
                {
                    ngayThYlForAttendance = _automationUi.FastDirectReadTagValue(g.Xml, g.Stt, "NGAY_TH_YL");
                    Log($"Read NGAY_TH_YL for attendance: {ngayThYlForAttendance}");
                }

                if (_config.AutoFixNguoiThucHien || _config.CheckAttendanceBeforePractitionerFix)
                {
                    practitioner = ResolvePractitionerByServiceName(g);
                    if (practitioner == null) return;
                    code = ResolvePractitionerCode(g, practitioner);
                    if (string.IsNullOrWhiteSpace(code)) return;
                    Log($"Auto selected practitioner group '{practitioner.CategoryName}' ({GetPractitionerCodeDisplay(practitioner)}) -> {code}");
                }
                else
                {
                    var dlg = new SelectPractitionerForm(_config.Practitioners);
                    dlg.TopMost = true;
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                    {
                        MessageBox.Show("Người dùng chưa chọn người thực hiện.");
                        return;
                    }

                    practitioner = dlg.Selected;
                    if (practitioner == null)
                    {
                        MessageBox.Show("Người dùng chưa chọn người thực hiện.");
                        return;
                    }

                    code = ResolvePractitionerCode(g, practitioner);
                    if (string.IsNullOrWhiteSpace(code))
                    {
                        return;
                    }
                }

                var shouldCheckAttendance = _config.CheckAttendanceBeforePractitionerFix && practitioner.CheckAttendance;
                if (shouldCheckAttendance)
                {
                    if (!TryParseNgayThYlDate(ngayThYlForAttendance, out var attendanceDate))
                    {
                        MessageBox.Show(
                            "Không đọc được NGAY_TH_YL hợp lệ để kiểm tra chấm công.",
                            "Kiểm tra chấm công",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return;
                    }

                    Log($"Attendance enabled for '{practitioner.CategoryName}': {attendanceDate:dd/MM/yyyy}");
                    var attendance = _attendanceCheck.Check(
                        practitioner.CategoryName,
                        attendanceDate,
                        code,
                        _config.AutoReplaceAbsentPractitioner);
                    if (!attendance.Success)
                    {
                        MessageBox.Show(
                            attendance.Message,
                            "Kiểm tra chấm công",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return;
                    }

                    if (!attendance.IsWorking)
                    {
                        if (_config.AutoReplaceAbsentPractitioner
                            && !string.IsNullOrWhiteSpace(attendance.ReplacementPractitionerCode))
                        {
                            var originalCode = code;
                            code = attendance.ReplacementPractitionerCode;
                            Log(
                                $"Attendance replacement selected: {originalCode} -> {code}; " +
                                $"Giờ vào={attendance.ReplacementTimeIn}, Giờ ra={attendance.ReplacementTimeOut}");
                        }
                        else
                        {
                            MessageBox.Show(
                                _config.AutoReplaceAbsentPractitioner
                                    ? "NGUOI_THUC_HIEN này off và không tìm thấy người thực hiện khác đủ điều kiện để thay thế"
                                    : "NGUOI_THUC_HIEN này off nên không thể thay thế",
                                "Kiểm tra chấm công",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                            return;
                        }
                    }

                    if (attendance.IsWorking)
                    {
                        Log($"Attendance passed for {code}: Giờ vào={attendance.TimeIn}, Giờ ra={attendance.TimeOut}");
                    }
                }
                else if (_config.CheckAttendanceBeforePractitionerFix)
                {
                    Log($"Skipped attendance check for '{practitioner.CategoryName}' ({code}) by practitioner setting.");
                }

                // Order: UseFastDirectEdit -> UseDirectFindThenLocalScanEdit -> UseDirectIndexScanEdit -> whole-XML
                if (_config.UseFastDirectEdit)
                {
                    Log("Đang dùng UseFastDirectEdit (Fast Direct Edit Mode): Ctrl+F đến STT, scan tối đa 80 dòng, timeout 15s.");
                    var ok = _automationUi.FastDirectFixNguoiThucHien(g.Xml, g.Stt, code);
                    if (ok)
                    {
                        Log($"FastDirectFixNguoiThucHien completed for XML{g.Xml} STT{g.Stt}");
                        return;
                    }
                    else
                    {
                        var logSb = new System.Text.StringBuilder();
                        logSb.AppendLine("ACTION: FastDirectFixNguoiThucHien_Failed");
                        logSb.AppendLine($"XML number: {g.Xml}");
                        logSb.AppendLine($"Target STT: {g.Stt}");
                        logSb.AppendLine($"PractitionerCode: {code}");
                        logSb.AppendLine($"Start time: {DateTime.Now:O}");
                        var path = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{g.Xml}_STT{g.Stt}_FastDirect_FixNguoi_Failed", logSb.ToString());
                        MessageBox.Show($"FastDirectEditMode failed. Debug log saved: {path}", "Fast Direct Edit Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Log("FastDirectFixNguoiThucHien failed; debug saved: " + path);
                        return;
                    }
                }
                else if (_config.UseDirectFindThenLocalScanEdit)
                {
                    Log("Đang dùng DirectFindThenLocalScanEdit: Ctrl+F đến STT, đóng Find, scan cục bộ trong block.");

                    var ok = _automationUi.DirectFindThenLocalFixNguoiThucHien(g.Xml, g.Stt, code);
                    if (ok)
                    {
                        Log($"DirectFindThenLocalFixNguoiThucHien completed for XML{g.Xml} STT{g.Stt}");
                        return;
                    }
                    else
                    {
                        var logSb = new System.Text.StringBuilder();
                        logSb.AppendLine("ACTION: DirectFindThenLocalFixNguoiThucHien_Failed");
                        logSb.AppendLine($"XML number: {g.Xml}");
                        logSb.AppendLine($"Target STT: {g.Stt}");
                        logSb.AppendLine($"PractitionerCode: {code}");
                        logSb.AppendLine($"Start time: {DateTime.Now:O}");
                        var path = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{g.Xml}_STT{g.Stt}_DirectFind_LocalFixNguoi_Failed", logSb.ToString());
                        MessageBox.Show($"DirectFindThenLocalScanEdit failed. Debug log saved: {path}", "Direct Edit Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Log("DirectFindThenLocalFixNguoiThucHien failed; debug saved: " + path);
                        return;
                    }
                }
                else if (_config.UseDirectIndexScanEdit)
                {
                    Log("UseDirectIndexScanEdit = true, bỏ qua toàn bộ whole-XML flow.");
                    Log("Đang sửa trực tiếp trên giao diện bằng scan từng dòng.");
                    Log("Không sử dụng Find, không Ctrl+A toàn bộ XML.");

                    var ok = _automationUi.DirectIndexFixNguoiThucHien(g.Xml, g.Stt, code);
                    if (ok)
                    {
                        Log($"DirectIndexFixNguoiThucHien completed for XML{g.Xml} STT{g.Stt}");
                        return;
                    }
                    else
                    {
                        var logSb = new System.Text.StringBuilder();
                        logSb.AppendLine("ACTION: DirectIndexFixNguoiThucHien_Failed");
                        logSb.AppendLine($"XML number: {g.Xml}");
                        logSb.AppendLine($"Target STT: {g.Stt}");
                        logSb.AppendLine($"PractitionerCode: {code}");
                        logSb.AppendLine($"Start time: {DateTime.Now:O}");
                        var path = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{g.Xml}_STT{g.Stt}_DirectIndexFixNguoiThucHien_Failed", logSb.ToString());
                        MessageBox.Show($"DirectIndexScanEdit failed. Debug log saved: {path}", "Direct Edit Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Log("DirectIndexFixNguoiThucHien failed; debug saved: " + path);
                        return;
                    }
                }

                // fallback: find external app window - use FindWindowByTitleContains
                var h2 = _automation.FindWindowByTitleContains(_config.XmlAppWindowTitleKeyword);
                if (h2 == IntPtr.Zero)
                {
                    MessageBox.Show($"Không tìm thấy cửa sổ phần mềm XML chứa tiêu đề '{_config.XmlAppWindowTitleKeyword}'");
                    return;
                }
                if (!_automation.ActivateWindow(h2))
                {
                    MessageBox.Show("Không thể kích hoạt cửa sổ phần mềm XML.");
                    return;
                }

                var clip = _automation.CopyCurrentXmlFromExternalApp(h2);
                if (string.IsNullOrEmpty(clip))
                {
                    MessageBox.Show("Không copy được nội dung XML từ ứng dụng ngoài.");
                    return;
                }

                // Validate xml before fix
                var v2 = ValidateXmlContentForFix(clip, g.Xml, g.Stt);
                if (!v2.IsValid)
                {
                    try
                    {
                        var dbgDir = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "DebugReads");
                        System.IO.Directory.CreateDirectory(dbgDir);
                        var ts = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        var fileBase = $"XML{g.Xml}*STT{g.Stt}*{ts}";
                        var txtPath = System.IO.Path.Combine(dbgDir, fileBase + ".txt");
                        var logPath = System.IO.Path.Combine(dbgDir, fileBase + ".log");
                        System.IO.File.WriteAllText(txtPath, clip ?? string.Empty);
                        var logSb = new System.Text.StringBuilder();
                        logSb.AppendLine($"Length: {v2.Length}");
                        logSb.AppendLine($"HasDvktOpenTag: {v2.HasDvktOpenTag}");
                        logSb.AppendLine($"HasDvktCloseTag: {v2.HasDvktCloseTag}");
                        logSb.AppendLine($"HasTargetStt: {v2.HasTargetStt}");
                        var first = clip == null ? string.Empty : clip.Substring(0, Math.Min(500, clip.Length));
                        var last = clip == null ? string.Empty : clip.Substring(Math.Max(0, clip.Length - 500), Math.Min(500, clip.Length));
                        logSb.AppendLine("--- First 500 chars ---");
                        logSb.AppendLine(first);
                        logSb.AppendLine("--- Last 500 chars ---");
                        logSb.AppendLine(last);
                        var focusInfo = _automationUi.GetFocusedElementInfo();
                        logSb.AppendLine("--- FocusedElementInfo ---");
                        foreach (var kvp in focusInfo) logSb.AppendLine($"{kvp.Key}: {kvp.Value}");
                        System.IO.File.WriteAllText(logPath, logSb.ToString());
                        MessageBox.Show($"Nội dung XML đọc không hợp lệ cho việc sửa. Debug saved: {txtPath} and {logPath}", "Validation failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Log("Saved debug reads: " + txtPath + " and " + logPath);
                    }
                    catch (Exception ex)
                    {
                        Log("Failed to write debug files: " + ex.Message);
                    }
                    return;
                }

                var result = XmlFixService.FixNguoiThucHien(clip, g.Xml, g.Stt, code, false);
                if (result.RequireOverwriteConfirmation && result.FieldsNeedOverwriteConfirmation.Contains("NGUOI_THUC_HIEN"))
                {
                    var oldVal = result.ExistingValues.ContainsKey("NGUOI_THUC_HIEN") ? result.ExistingValues["NGUOI_THUC_HIEN"] : "";
                    var ans = MessageBox.Show($"NGUOI_THUC_HIEN đã có giá trị: {oldVal}. Bạn có muốn ghi đè không?", "Xác nhận ghi đè", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (ans == DialogResult.Yes)
                    {
                        Log($"User chose to overwrite NGUOI_THUC_HIEN for XML{g.Xml} STT{g.Stt}");
                        result = XmlFixService.FixNguoiThucHien(clip, g.Xml, g.Stt, code, true);
                    }
                    else
                    {
                        Log($"User cancelled overwrite NGUOI_THUC_HIEN for XML{g.Xml} STT{g.Stt}");
                        return;
                    }
                }
                if (!result.Success)
                {
                    MessageBox.Show(string.Join("\\n", result.Messages));
                    return;
                }

                var backupPath = _backup.SaveBackup(clip, g.Xml, g.Stt);
                var preview = new PreviewFixForm(clip, result.FixedXml, result.FixedFields, result.Messages);
                if (preview.ShowDialog() != DialogResult.OK)
                {
                    Log("User cancelled preview paste");
                    return;
                }

                var pasted = _automation.PasteXmlToExternalApp(h2, result.FixedXml);
                Log($"Fixed NGUOI_THUC_HIEN XML{g.Xml} STT{g.Stt}. Backup: {backupPath}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log("Error in fix nguoi: " + ex.Message);
            }
        }

        private string ResolvePractitionerCode(XmlErrorGroup g, PractitionerItem item)
        {
            if (item == null)
            {
                return null;
            }

            if (!item.UsesMaBacSi())
            {
                return item.Code;
            }

            var code = _automationUi.FastDirectReadTagValue(g.Xml, g.Stt, "MA_BAC_SI");
            if (string.IsNullOrWhiteSpace(code))
            {
                MessageBox.Show(
                    $"Không đọc được MA_BAC_SI trong block STT {g.Stt}.",
                    "Sửa NGUOI_THUC_HIEN",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return null;
            }

            Log($"Read MA_BAC_SI for XML{g.Xml} STT{g.Stt}: {code}");
            return code.Trim();
        }

        private static string GetPractitionerCodeDisplay(PractitionerItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            return item.UsesMaBacSi() ? "Lấy MA_BAC_SI" : item.Code;
        }

        private PractitionerItem ResolvePractitionerByServiceName(XmlErrorGroup g)
        {
            var serviceName = _automationUi.FastDirectReadTagValue(g.Xml, g.Stt, "TEN_DICH_VU");
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                MessageBox.Show("Không đọc được TEN_DICH_VU trong block đang sửa.", "Tự động sửa NGUOI_THUC_HIEN", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }

            var normalizedService = NormalizeServiceNameForMatch(serviceName);
            var matches = _config.Practitioners
                .Where(p => p.ServiceNames != null && p.ServiceNames.Any(s => NormalizeServiceNameForMatch(s) == normalizedService))
                .ToList();

            if (matches.Count == 1)
            {
                return matches[0];
            }

            if (matches.Count == 0)
            {
                MessageBox.Show(
                    $"Không tìm thấy nhóm nào khớp TEN_DICH_VU:\n{serviceName}",
                    "Tự động sửa NGUOI_THUC_HIEN",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return null;
            }

            var groups = string.Join("\n", matches.Select(m => $"- {m.CategoryName} ({m.Code})"));
            MessageBox.Show(
                $"TEN_DICH_VU khớp nhiều nhóm, không tự động sửa để tránh sai:\n{serviceName}\n\nCác nhóm trùng:\n{groups}",
                "Tự động sửa NGUOI_THUC_HIEN",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return null;
        }

        private static string NormalizeServiceNameForMatch(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return System.Text.RegularExpressions.Regex.Replace(value.Trim(), "\\s+", " ").ToUpperInvariant();
        }

        private static bool TryParseNgayThYlDate(string value, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(value)) return false;

            var digits = new string(value.Where(char.IsDigit).ToArray());
            if (digits.Length < 8) return false;

            return DateTime.TryParseExact(
                digits.Substring(0, 8),
                "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out date);
        }

        private void Card_OnFixTBncct(XmlErrorGroup g)
        {
            try
            {
                Log($"Fix T_BNCCT for XML{g.Xml}" + (g.Stt > 0 ? $" STT {g.Stt}" : ""));

                var h = _automation.FindWindowByTitleContains(_config.XmlAppWindowTitleKeyword);
                if (h == IntPtr.Zero)
                {
                    MessageBox.Show($"Không tìm thấy cửa sổ phần mềm XML chứa tiêu đề '{_config.XmlAppWindowTitleKeyword}'.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (!_automation.ActivateWindow(h))
                {
                    MessageBox.Show("Không thể kích hoạt cửa sổ phần mềm XML.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                _automationUi.SyncMainWindowHandle(h);

                var ok = _automationUi.FastDirectFixTBncct(g.Xml, g.Stt);
                if (ok)
                {
                    Log($"FastDirectFixTBncct completed for XML{g.Xml}" + (g.Stt > 0 ? $" STT{g.Stt}" : ""));
                    return;
                }

                var logSb = new System.Text.StringBuilder();
                logSb.AppendLine("ACTION: FastDirectFixTBncct_Failed");
                logSb.AppendLine($"XML number: {g.Xml}");
                logSb.AppendLine($"Target STT: {g.Stt}");
                logSb.AppendLine($"Start time: {DateTime.Now:O}");
                var path = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{g.Xml}_STT{g.Stt}_FastDirect_FixTBncct_Failed", logSb.ToString());
                MessageBox.Show($"Sửa T_BNCCT thất bại. Debug log saved: {path}", "Fast Direct Edit Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log("FastDirectFixTBncct failed; debug saved: " + path);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log("Error in fix T_BNCCT: " + ex.Message);
            }
        }

        private void Card_OnFixMucHuong(XmlErrorGroup g)
        {
            try
            {
                Log($"Fix MUC_HUONG for XML{g.Xml}" + (g.Stt > 0 ? $" STT {g.Stt}" : ""));

                var h = _automation.FindWindowByTitleContains(_config.XmlAppWindowTitleKeyword);
                if (h == IntPtr.Zero)
                {
                    MessageBox.Show($"Không tìm thấy cửa sổ phần mềm XML chứa tiêu đề '{_config.XmlAppWindowTitleKeyword}'.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (!_automation.ActivateWindow(h))
                {
                    MessageBox.Show("Không thể kích hoạt cửa sổ phần mềm XML.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                _automationUi.SyncMainWindowHandle(h);

                var ok = _automationUi.FastDirectFixMucHuong(g.Xml, g.Stt);
                if (ok)
                {
                    Log($"FastDirectFixMucHuong completed for XML{g.Xml}" + (g.Stt > 0 ? $" STT{g.Stt}" : ""));
                    return;
                }

                var logSb = new System.Text.StringBuilder();
                logSb.AppendLine("ACTION: FastDirectFixMucHuong_Failed");
                logSb.AppendLine($"XML number: {g.Xml}");
                logSb.AppendLine($"Target STT: {g.Stt}");
                logSb.AppendLine($"Start time: {DateTime.Now:O}");
                var path = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{g.Xml}_STT{g.Stt}_FastDirect_FixMucHuong_Failed", logSb.ToString());
                MessageBox.Show($"Sửa MUC_HUONG thất bại. Debug log saved: {path}", "Fast Direct Edit Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log("FastDirectFixMucHuong failed; debug saved: " + path);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log("Error in fix MUC_HUONG: " + ex.Message);
            }
        }

        private void Card_OnFixNgayTToan(XmlErrorGroup g)
        {
            try
            {
                Log($"Fix NGAY_TTOAN for XML{g.Xml}" + (g.Stt > 0 ? $" STT {g.Stt}" : ""));

                var h = _automation.FindWindowByTitleContains(_config.XmlAppWindowTitleKeyword);
                if (h == IntPtr.Zero)
                {
                    MessageBox.Show($"Không tìm thấy cửa sổ phần mềm XML chứa tiêu đề '{_config.XmlAppWindowTitleKeyword}'.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (!_automation.ActivateWindow(h))
                {
                    MessageBox.Show("Không thể kích hoạt cửa sổ phần mềm XML.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                _automationUi.SyncMainWindowHandle(h);

                var ok = _automationUi.FastDirectFixNgayTToan(g.Xml, g.Stt);
                if (ok)
                {
                    Log($"FastDirectFixNgayTToan completed for XML{g.Xml}" + (g.Stt > 0 ? $" STT{g.Stt}" : ""));
                    return;
                }

                var logSb = new System.Text.StringBuilder();
                logSb.AppendLine("ACTION: FastDirectFixNgayTToan_Failed");
                logSb.AppendLine($"XML number: {g.Xml}");
                logSb.AppendLine($"Target STT: {g.Stt}");
                logSb.AppendLine($"Start time: {DateTime.Now:O}");
                var path = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{g.Xml}_STT{g.Stt}_FastDirect_FixNgayTToan_Failed", logSb.ToString());
                MessageBox.Show($"Sửa NGAY_TTOAN thất bại. Debug log saved: {path}", "Fast Direct Edit Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log("FastDirectFixNgayTToan failed; debug saved: " + path);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log("Error in fix NGAY_TTOAN: " + ex.Message);
            }
        }

        private void Card_OnFixSoCccd(XmlErrorGroup g)
        {
            try
            {
                Log($"Clear SO_CCCD for XML{g.Xml}" + (g.Stt > 0 ? $" STT {g.Stt}" : ""));

                var h = _automation.FindWindowByTitleContains(_config.XmlAppWindowTitleKeyword);
                if (h == IntPtr.Zero)
                {
                    MessageBox.Show($"Không tìm thấy cửa sổ phần mềm XML chứa tiêu đề '{_config.XmlAppWindowTitleKeyword}'.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (!_automation.ActivateWindow(h))
                {
                    MessageBox.Show("Không thể kích hoạt cửa sổ phần mềm XML.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                _automationUi.SyncMainWindowHandle(h);

                if (_automationUi.FastDirectClearSoCccd(g.Xml, g.Stt))
                {
                    Log($"FastDirectClearSoCccd completed for XML{g.Xml}" + (g.Stt > 0 ? $" STT{g.Stt}" : ""));
                    return;
                }

                var logSb = new System.Text.StringBuilder();
                logSb.AppendLine("ACTION: FastDirectClearSoCccd_Failed");
                logSb.AppendLine($"XML number: {g.Xml}");
                logSb.AppendLine($"Target STT: {g.Stt}");
                logSb.AppendLine($"Start time: {DateTime.Now:O}");
                var path = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{g.Xml}_STT{g.Stt}_FastDirect_ClearSoCccd_Failed", logSb.ToString());
                MessageBox.Show($"Xóa SO_CCCD thất bại. Debug log saved: {path}", "Fast Direct Edit Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log("FastDirectClearSoCccd failed; debug saved: " + path);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log("Error clearing SO_CCCD: " + ex.Message);
            }
        }

        private void Card_OnFixAll(XmlErrorGroup g)
        {
            if (g.Items.Any(i => i.Field == "NGAY_TH_YL")) Card_OnFixNgay(g);
            if (g.Items.Any(i => i.Field == "NGUOI_THUC_HIEN")) Card_OnFixNguoi(g);
            if (g.Items.Any(i => i.Field == "T_BNCCT")) Card_OnFixTBncct(g);
            if (g.Items.Any(i => i.Field == "MUC_HUONG")) Card_OnFixMucHuong(g);
            if (g.Items.Any(i => i.Field == "NGAY_TTOAN")) Card_OnFixNgayTToan(g);
            if (g.Items.Any(i => i.Field == "SO_CCCD")) Card_OnFixSoCccd(g);
        }

        // Debug: Test reading XML3
        private void TestXml3_Click(object sender, EventArgs e)
        {
            Log("=== Test XML3 Reading ===");
            var sb = new System.Text.StringBuilder();

            try
            {
                // Get main window info
                var mainHwnd = _automationUi.GetMainWindow();
                if (mainHwnd == IntPtr.Zero)
                {
                    sb.AppendLine("✗ Main window not found");
                    MessageBox.Show(sb.ToString(), "Test XML3 - FAILED", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Log(sb.ToString());
                    return;
                }
                sb.AppendLine($"✓ Main window found: Handle={mainHwnd}");

                // Try reading XML3
                Log("Attempting to read XML3 via UI Automation...");
                var xml3Content = _automationUi.ReadXmlText(3);

                if (string.IsNullOrEmpty(xml3Content))
                {
                    sb.AppendLine("✗ Could not read XML3 content");

                    // Get diagnostics
                    var focusInfo = _automationUi.GetFocusedElementInfo();
                    sb.AppendLine("\nFocused Element Info:");
                    foreach (var kvp in focusInfo)
                    {
                        sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
                    }

                    sb.AppendLine("\nPossible reasons:");
                    sb.AppendLine("1. Control xml3 not found by AutomationId");
                    sb.AppendLine("2. Could not focus xml3 control");
                    sb.AppendLine("3. Clipboard did not receive content after Ctrl+C");
                    sb.AppendLine("4. SupportEdit may need Administrator privileges");
                    sb.AppendLine("5. XML app may need to be in foreground");

                    MessageBox.Show(sb.ToString(), "Test XML3 - FAILED", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Log(sb.ToString());
                }
                else
                {
                    sb.AppendLine($"✓ Successfully read XML3");
                    sb.AppendLine($"  Content length: {xml3Content.Length} bytes");
                    sb.AppendLine($"  First 100 chars: {xml3Content.Substring(0, Math.Min(100, xml3Content.Length))}...");
                    if (xml3Content.Contains("<?xml")) sb.AppendLine("  Contains XML declaration ✓");
                    if (xml3Content.Contains("<CHI_TIET_DVKT") || xml3Content.Contains("<CHITIEU")) sb.AppendLine("  Contains DVKT/CHITIEU blocks ✓");

                    MessageBox.Show(sb.ToString(), "Test XML3 - SUCCESS", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            Log(sb.ToString());
                            }
                        }
                        catch (Exception ex)
                        {
                            sb.AppendLine($"✗ Exception: {ex.Message}");
                            sb.AppendLine($"\nStack Trace: {ex.StackTrace}");
                            MessageBox.Show(sb.ToString(), "Test XML3 - ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            Log(sb.ToString());
                        }
                    }

                    // Debug: show current test mode
                    private void TestMode_Click(object sender, EventArgs e)
                    {
                        try
                        {
                            var mode = _config.UseDirectFindThenLocalScanEdit ? "DirectFindThenLocalScanEdit" : _config.UseDirectIndexScanEdit ? "DirectIndexScanEdit" : "WholeXmlFlow";
                            var msg = $"Mode: {mode}\n" +
                                      $"UseDirectFindThenLocalScanEdit: {_config.UseDirectFindThenLocalScanEdit}\n" +
                                      $"UseDirectIndexScanEdit: {_config.UseDirectIndexScanEdit}\n" +
                                      $"UseDirectUiEditFallback: {_config.UseDirectUiEditFallback}\n" +
                                      $"appconfig path: {_config.ConfigFilePath}";
                            MessageBox.Show(msg, "Current Test Mode", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Failed to show test mode: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }

                    // Debug: test timing for NGAY_TH_YL (Find + Verify only, no edit)
                    private void TestTimingSuaNgayThYl_Click(object sender, EventArgs e)
                    {
                        var form = new Form();
                        form.Text = "Test timing sửa NGAY_TH_YL";
                        form.Width = 400;
                        form.Height = 200;
                        form.StartPosition = FormStartPosition.CenterScreen;

                        var lblXml = new Label { Text = "XML số:", Left = 10, Top = 10, Width = 60 };
                        var txtXml = new TextBox { Text = "3", Left = 70, Top = 10, Width = 100 };
                        form.Controls.Add(lblXml);
                        form.Controls.Add(txtXml);

                        var lblStt = new Label { Text = "STT:", Left = 10, Top = 40, Width = 60 };
                        var txtStt = new TextBox { Text = "1", Left = 70, Top = 40, Width = 100 };
                        form.Controls.Add(lblStt);
                        form.Controls.Add(txtStt);

                        var btnRun = new Button { Text = "Chạy test", Left = 70, Top = 70, Width = 100 };
                        form.Controls.Add(btnRun);

                        btnRun.Click += (sender2, e2) =>
                        {
                            try
                            {
                                if (!int.TryParse(txtXml.Text, out int xml) || !int.TryParse(txtStt.Text, out int stt))
                                {
                                    MessageBox.Show("Invalid XML or STT number", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    return;
                                }

                                form.Hide();

                                // Find and activate external app
                                var h = _automation.FindWindowByTitleContains(_config.XmlAppWindowTitleKeyword);
                                if (h == IntPtr.Zero)
                                {
                                    MessageBox.Show($"Cannot find window with '{_config.XmlAppWindowTitleKeyword}'", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    form.Show();
                                    return;
                                }

                                if (!_automation.ActivateWindow(h))
                                {
                                    MessageBox.Show("Cannot activate window", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    form.Show();
                                    return;
                                }

                                // Sync handle so AutomationUiService does not need to search again
                                _automationUi.SyncMainWindowHandle(h);

                                Log($"Running TestTimingSuaNgayThYl for XML{xml} STT {stt}...");

                                // Run timing test
                                var (success, timings, logPath) = _automationUi.TestTimingSuaNgayThYl(xml, stt);

                                var sb = new System.Text.StringBuilder();
                                sb.AppendLine($"Test Timing Result (Find + Verify only, no edit)");
                                sb.AppendLine($"XML: {xml}, STT: {stt}");
                                sb.AppendLine($"Success: {success}");
                                sb.AppendLine();
                                sb.AppendLine("Timing breakdown (ms):");
                                foreach (var kvp in timings.OrderBy(k => k.Key))
                                {
                                    sb.AppendLine($"  {kvp.Key}: {kvp.Value}ms");
                                }
                                sb.AppendLine();
                                sb.AppendLine($"Log saved to: {logPath}");

                                MessageBox.Show(sb.ToString(), "Test Timing Result", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                Log(sb.ToString());

                                form.Show();
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Error during test: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                Log($"TestTimingSuaNgayThYl error: {ex}");
                                form.Show();
                            }
                        };

                        form.ShowDialog();
                    }

                    // Debug: Test GoTo STT Fast — runs GoToSttFastAndReport (no edit) and shows result
        private void TestGoToSttFast_Click(object sender, EventArgs e)
        {
            var form = new Form();
            form.Text = "Test GoTo STT Fast";
            form.Width = 480;
            form.Height = 310;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.MaximizeBox = false;

            var lblXml = new Label { Text = "XML số:", Left = 10, Top = 15, Width = 65 };
            var txtXml = new TextBox { Text = "3", Left = 75, Top = 12, Width = 70 };
            form.Controls.Add(lblXml);
            form.Controls.Add(txtXml);

            var lblStt = new Label { Text = "STT:", Left = 160, Top = 15, Width = 35 };
            var txtStt = new TextBox { Text = "1", Left = 195, Top = 12, Width = 80 };
            form.Controls.Add(lblStt);
            form.Controls.Add(txtStt);

            var btnRun = new Button { Text = "Chạy Test GoTo", Left = 290, Top = 10, Width = 140, Height = 26 };
            form.Controls.Add(btnRun);

            var txtResult = new TextBox
            {
                Left = 10, Top = 50, Width = 450, Height = 215,
                Multiline = true, ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new System.Drawing.Font("Consolas", 8.5f),
                BackColor = System.Drawing.Color.Black,
                ForeColor = System.Drawing.Color.LimeGreen
            };
            form.Controls.Add(txtResult);

            btnRun.Click += (s2, e2) =>
            {
                if (!int.TryParse(txtXml.Text.Trim(), out int xml) || xml < 1 || xml > 14)
                {
                    MessageBox.Show("XML số phải là số nguyên 1-14", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (!int.TryParse(txtStt.Text.Trim(), out int stt) || stt < 1)
                {
                    MessageBox.Show("STT phải là số nguyên dương", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                txtResult.Text = $"Đang tìm XML{xml} STT {stt}...\r\nHãy KHÔNG động vào chuột/bàn phím!";
                form.Refresh();

                var h = _automation.FindWindowByTitleContains(_config.XmlAppWindowTitleKeyword);
                if (h == IntPtr.Zero)
                {
                    txtResult.Text = $"THẤT BẠI: Không tìm thấy cửa sổ '{_config.XmlAppWindowTitleKeyword}'";
                    return;
                }
                _automation.ActivateWindow(h);
                Thread.Sleep(150);

                // Sync handle so AutomationUiService does not need to search again
                _automationUi.SyncMainWindowHandle(h);

                var report = _automationUi.GoToSttFastAndReport(xml, stt);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"XML{xml}  STT{stt}  →  {(report.Success ? "✓ SUCCESS" : "✗ FAILED")}");
                sb.AppendLine($"Elapsed : {report.ElapsedMs} ms");
                sb.AppendLine($"Search  : [{report.SearchText}]");
                sb.AppendLine($"LineFnd : [{report.LineAfterFind}]");
                sb.AppendLine($"LineDown: [{report.LineAfterDown}]");
                sb.AppendLine($"LineUp  : [{report.LineAfterUp}]");
                if (!report.Success)
                {
                    sb.AppendLine($"Reason  : {report.FailReason}");
                }
                sb.AppendLine($"Log     : {report.LogPath}");
                sb.AppendLine();
                sb.AppendLine(report.Success
                    ? "Con trỏ đang ở dòng STT. Nếu muốn sửa, bấm nút sửa từ card lỗi."
                    : "Hãy kiểm tra log để biết nguyên nhân chi tiết.");

                txtResult.Text = sb.ToString();
                Log($"TestGoToSttFast: XML{xml} STT{stt} = {(report.Success ? "SUCCESS" : "FAILED")} ({report.ElapsedMs}ms)");
                if (!string.IsNullOrEmpty(report.LogPath))
                    Log($"Log: {report.LogPath}");
            };

            form.ShowDialog();
        }

        // Validate XML content prior to fix
        private void SaveXml3ClickPosition_Click(object sender, EventArgs e)
        {
            try
            {
                // Ask user for XML number
                var inputForm = new Form();
                inputForm.Text = "Lưu vị trí click XML";
                inputForm.Width = 300;
                inputForm.Height = 150;
                inputForm.StartPosition = FormStartPosition.CenterScreen;

                var lblXml = new Label { Text = "XML number (1-4):", Left = 10, Top = 20, Width = 120 };
                var txtXmlNum = new TextBox { Text = "3", Left = 140, Top = 20, Width = 100 };
                inputForm.Controls.Add(lblXml);
                inputForm.Controls.Add(txtXmlNum);

                var btnOk = new Button { Text = "OK", Left = 140, Top = 60, Width = 80, DialogResult = DialogResult.OK };
                var btnCancel = new Button { Text = "Cancel", Left = 230, Top = 60, Width = 60, DialogResult = DialogResult.Cancel };
                inputForm.Controls.Add(btnOk);
                inputForm.Controls.Add(btnCancel);
                inputForm.AcceptButton = btnOk;
                inputForm.CancelButton = btnCancel;

                if (inputForm.ShowDialog() != DialogResult.OK)
                    return;

                if (!int.TryParse(txtXmlNum.Text, out int xmlNumber) || xmlNumber < 1 || xmlNumber > 4)
                {
                    MessageBox.Show("Invalid XML number. Please enter 1-4.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                Log($"SaveXmlClickPosition: Getting mouse position for XML{xmlNumber}...");
                var mousePos = System.Windows.Forms.Cursor.Position;
                Log($"Current mouse position: X={mousePos.X}, Y={mousePos.Y}");

                // Get the XML main window info
                if (!_automationUi.GetMainWindowRect(out int winX, out int winY, out int winWidth, out int winHeight))
                {
                    MessageBox.Show("Cannot get external app window position. Make sure it's open.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Calculate offset relative to window
                int offsetX = mousePos.X - winX;
                int offsetY = mousePos.Y - winY;

                Log($"Window position: X={winX}, Y={winY}, Width={winWidth}, Height={winHeight}");
                Log($"Calculated offset for XML{xmlNumber}: X={offsetX}, Y={offsetY}");

                // Update config based on XML number
                switch (xmlNumber)
                {
                    case 1:
                        _config.Xml1ClickOffsetX = offsetX;
                        _config.Xml1ClickOffsetY = offsetY;
                        break;
                    case 2:
                        _config.Xml2ClickOffsetX = offsetX;
                        _config.Xml2ClickOffsetY = offsetY;
                        break;
                    case 3:
                        _config.Xml3ClickOffsetX = offsetX;
                        _config.Xml3ClickOffsetY = offsetY;
                        break;
                    case 4:
                        _config.Xml4ClickOffsetX = offsetX;
                        _config.Xml4ClickOffsetY = offsetY;
                        break;
                }

                _config.Save();

                MessageBox.Show($"Đã lưu vị trí click XML{xmlNumber}:\n\nX offset: {offsetX}\nY offset: {offsetY}\n\nThese offsets will be used for auto-focusing XML{xmlNumber}.", "Position Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Log($"SaveXmlClickPosition: Saved offsets for XML{xmlNumber} X={offsetX}, Y={offsetY}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving position: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log($"SaveXmlClickPosition error: {ex}");
            }
        }

        // Validate XML content prior to fix
        private SupportEdit.Services.XmlValidationResult ValidateXmlContentForFix(string xmlContent, int xmlNumber, int stt)
        {
            var res = new SupportEdit.Services.XmlValidationResult();
            res.Length = xmlContent == null ? 0 : xmlContent.Length;
            res.HasDvktOpenTag = !string.IsNullOrEmpty(xmlContent) && xmlContent.Contains("<CHI_TIET_DVKT");
            res.HasDvktCloseTag = !string.IsNullOrEmpty(xmlContent) && xmlContent.Contains("</CHI_TIET_DVKT>");
            try
            {
                var rx = new System.Text.RegularExpressions.Regex($"<STT>\\s*{stt}\\s*</STT>", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
                res.HasTargetStt = !string.IsNullOrEmpty(xmlContent) && rx.IsMatch(xmlContent);
            }
            catch { res.HasTargetStt = false; }

            res.IsValid = res.Length > 0 && res.HasDvktOpenTag && res.HasDvktCloseTag && res.HasTargetStt;
            res.Message = res.IsValid ? "OK" : "Invalid XML content for fix";
            return res;
        }

        private void OpenDebugReads_Click(object sender, EventArgs e)
        {
            try
            {
                var folder = SupportEdit.Services.DebugLogService.GetDebugReadsFolder();
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cannot open DebugReads folder: " + ex.Message);
            }
        }

        private void EnsureDongBoVasButton()
        {
            if (btnDongBoVAS != null)
            {
                btnDongBoVAS.Click -= BtnDongBoVAS_Click;
                btnDongBoVAS.Click += BtnDongBoVAS_Click;
                return;
            }

            btnDongBoVAS = new Button
            {
                Name = "btnDongBoVAS",
                Text = "Đồng bộ VAS",
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new System.Drawing.Point(btnReadError.Left - 130, btnReadError.Top)
            };
            btnDongBoVAS.Click += BtnDongBoVAS_Click;
            Controls.Add(btnDongBoVAS);
            btnDongBoVAS.BringToFront();
        }

        private async void BtnDongBoVAS_Click(object sender, EventArgs e)
        {
            if (btnDongBoVAS != null)
            {
                btnDongBoVAS.Enabled = false;
            }

            try
            {
                await RunVasSyncAsync();
            }
            finally
            {
                if (btnDongBoVAS != null)
                {
                    btnDongBoVAS.Enabled = true;
                }
            }
        }

        private Task RunVasSyncAsync()
        {
            var sb = new StringBuilder();
            sb.AppendLine("ACTION: DongBoVAS");
            sb.AppendLine($"Start: {DateTime.Now:O}");
            sb.AppendLine($"DebugReadsFolder: {SupportEdit.Services.DebugLogService.GetDebugReadsFolder()}");
            try
            {
                var process = _vasSync.FindVasProcess();
                Log($"VAS process {(process == null ? "not found" : "found")}");
                sb.AppendLine(process == null ? "VAS process not found" : $"VAS process found: {process.ProcessName} (PID {process.Id})");
                if (process == null)
                {
                    throw new InvalidOperationException("Không tìm thấy EISClient.exe. Vui lòng mở phần mềm VAS trước.");
                }

                var vasWindow = _vasSync.FindVasWindow();
                sb.AppendLine($"VAS window handle: 0x{vasWindow.ToInt64():X}");
                if (vasWindow == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Không tìm thấy cửa sổ VAS phù hợp.");
                }

                var vasCopy = _vasSync.RunVasManualCopySequence(true);
                var vasText = vasCopy.Text;
                sb.AppendLine($"VAS manual copy debug log: {vasCopy.DebugLogPath}");
                sb.AppendLine($"VAS log length: {(vasText?.Length ?? 0)}");
                sb.AppendLine($"VAS validation: {vasCopy.IsValid}");
                sb.AppendLine($"VAS validation flags: {vasCopy.ValidationFlags}");
                sb.AppendLine("VAS log preview:");
                sb.AppendLine(vasText == null ? string.Empty : vasText.Substring(0, Math.Min(500, vasText.Length)));
                if (string.IsNullOrWhiteSpace(vasText))
                {
                    throw new InvalidOperationException("Không copy được log VAS. Có thể tọa độ khung log chưa đúng hoặc VAS chưa nhận focus. Hãy chạy Debug → Test click VAS log và gửi file log.");
                }

                var xmlWindow = _gdFormAutomation.FindXmlAppWindow();
                sb.AppendLine($"XML window handle: 0x{xmlWindow.ToInt64():X}");
                if (xmlWindow == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Không tìm thấy cửa sổ phần mềm XML.");
                }

                if (!_gdFormAutomation.OpenGdFormFromXmlApp())
                {
                    throw new InvalidOperationException("Không mở được GDForm/Cập nhật mã giao dịch.");
                }
                sb.AppendLine("GDForm opened");

                var gdWindow = WaitForGdFormWindow();
                if (gdWindow == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Không mở được GDForm/Cập nhật mã giao dịch.");
                }

                var pasted = _gdFormAutomation.PasteTextToGdForm(vasText);
                sb.AppendLine($"Paste success: {pasted}");
                if (!pasted)
                {
                    throw new InvalidOperationException("Không paste được nội dung VAS vào GDForm.");
                }

                var clicked = _gdFormAutomation.ClickGdConvertButton();
                sb.AppendLine($"Convert button clicked: {clicked}");
                if (!clicked)
                {
                    throw new InvalidOperationException("Không bấm được nút Chuyển dữ liệu.");
                }

                var resultMessage = _gdFormAutomation.WaitForResultMessageBox();
                sb.AppendLine("MessageBox result:");
                sb.AppendLine(resultMessage ?? string.Empty);

                var logPath = SupportEdit.Services.DebugLogService.SaveDebugLog("DongBoVAS", sb.ToString());
                Log("Saved DebugReads log: " + logPath);
            }
            catch (Exception ex)
            {
                sb.AppendLine("Exception:");
                sb.AppendLine(ex.ToString());
                var logPath = SupportEdit.Services.DebugLogService.SaveDebugLog("DongBoVAS_Error", sb.ToString());
                Log("Saved DebugReads log: " + logPath);
                ShowError($"Đồng bộ VAS thất bại.\n\n{ex.Message}\n\nLog: {logPath}");
            }

            return Task.CompletedTask;
        }

        private IntPtr WaitForGdFormWindow()
        {
            var timeout = Math.Max(1000, _config.GdFormOpenTimeoutMs);
            var deadline = DateTime.UtcNow.AddMilliseconds(timeout);
            while (DateTime.UtcNow < deadline)
            {
                var hWnd = _gdFormAutomation.FindGdFormWindow();
                if (hWnd != IntPtr.Zero)
                {
                    return hWnd;
                }
                Thread.Sleep(150);
            }
            return IntPtr.Zero;
        }

        private void ShowInfo(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => MessageBox.Show(this, message, "SupportEdit", MessageBoxButtons.OK, MessageBoxIcon.Information)));
            }
            else
            {
                MessageBox.Show(this, message, "SupportEdit", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ShowError(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => MessageBox.Show(this, message, "SupportEdit", MessageBoxButtons.OK, MessageBoxIcon.Error)));
            }
            else
            {
                MessageBox.Show(this, message, "SupportEdit", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveVasLogPosition_Click(object sender, EventArgs e)
        {
            SaveOffsetFromMouse("Lưu vị trí khung log VAS", (x, y) => { _config.VasLogClickOffsetX = x; _config.VasLogClickOffsetY = y; }, "VasLogClickOffsetX", "VasLogClickOffsetY");
        }

        private void SaveVasSuccessTabPosition_Click(object sender, EventArgs e)
        {
            SaveOffsetFromMouse("Lưu vị trí tab Thành công VAS", (x, y) => { _config.VasSuccessTabOffsetX = x; _config.VasSuccessTabOffsetY = y; }, "VasSuccessTabOffsetX", "VasSuccessTabOffsetY");
        }

        private void SaveMaGDPosition_Click(object sender, EventArgs e)
        {
            SaveOffsetFromMouse("Lưu vị trí nút Mã GD", (x, y) => { _config.MaGDButtonOffsetX = x; _config.MaGDButtonOffsetY = y; }, "MaGDButtonOffsetX", "MaGDButtonOffsetY");
        }

        private void SaveGdTxtBodyPosition_Click(object sender, EventArgs e)
        {
            SaveOffsetFromMouse("Lưu vị trí txtbody GDForm", (x, y) => { _config.GdTxtBodyOffsetX = x; _config.GdTxtBodyOffsetY = y; }, "GdTxtBodyOffsetX", "GdTxtBodyOffsetY");
        }

        private void SaveGdConvertButtonPosition_Click(object sender, EventArgs e)
        {
            SaveOffsetFromMouse("Lưu vị trí nút Chuyển dữ liệu", (x, y) => { _config.GdConvertButtonOffsetX = x; _config.GdConvertButtonOffsetY = y; }, "GdConvertButtonOffsetX", "GdConvertButtonOffsetY");
        }

        private void SaveOffsetFromMouse(string title, Action<int, int> assignOffsets, string keyX, string keyY)
        {
            try
            {
                var inputForm = new Form();
                inputForm.Text = title;
                inputForm.Width = 320;
                inputForm.Height = 140;
                inputForm.StartPosition = FormStartPosition.CenterScreen;

                var lbl = new Label { Text = "Đặt chuột lên vị trí cần lưu rồi bấm OK.", Left = 10, Top = 15, Width = 280 };
                inputForm.Controls.Add(lbl);

                var btnOk = new Button { Text = "OK", Left = 150, Top = 55, Width = 80, DialogResult = DialogResult.OK };
                var btnCancel = new Button { Text = "Hủy", Left = 235, Top = 55, Width = 60, DialogResult = DialogResult.Cancel };
                inputForm.Controls.Add(btnOk);
                inputForm.Controls.Add(btnCancel);
                inputForm.AcceptButton = btnOk;
                inputForm.CancelButton = btnCancel;

                if (inputForm.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                var mousePos = Cursor.Position;
                var targetWindow = title.Contains("VAS") ? _vasSync.FindVasWindow() : _gdFormAutomation.FindGdFormWindow();
                if (targetWindow == IntPtr.Zero)
                {
                    targetWindow = _gdFormAutomation.FindXmlAppWindow();
                }

                if (targetWindow == IntPtr.Zero)
                {
                    MessageBox.Show("Không tìm thấy cửa sổ để tính offset.");
                    return;
                }

                bool gotRect;
                int winX;
                int winY;
                int winWidth;
                int winHeight;
                if (title.Contains("VAS"))
                {
                    gotRect = _vasSync.GetVasWindowRect(out winX, out winY, out winWidth, out winHeight);
                }
                else
                {
                    gotRect = _automationUi.GetMainWindowRect(out winX, out winY, out winWidth, out winHeight);
                }

                if (!gotRect)
                {
                    MessageBox.Show("Không lấy được vị trí cửa sổ.");
                    return;
                }

                var offsetX = mousePos.X - winX;
                var offsetY = mousePos.Y - winY;
                assignOffsets(offsetX, offsetY);
                _config.Save();
                Log($"Saved {keyX}/{keyY} = {offsetX}/{offsetY}");
                MessageBox.Show($"Đã lưu {title}: X={offsetX}, Y={offsetY}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void TestReadVas_Click(object sender, EventArgs e)
        {
            var text = _vasSync.ReadVasSuccessLog();
            Log("Test đọc VAS length = " + (text?.Length ?? 0));
            MessageBox.Show(text ?? "Không đọc được VAS.");
        }

        private void TestOpenGdForm_Click(object sender, EventArgs e)
        {
            var ok = _gdFormAutomation.OpenGdFormFromXmlApp();
            Log("Test mở GDForm = " + ok);
            MessageBox.Show(ok ? "Đã mở GDForm." : "Không mở được GDForm.");
        }

        private void TestPasteGdForm_Click(object sender, EventArgs e)
        {
            var text = txtErrorRaw?.Text ?? string.Empty;
            var ok = _gdFormAutomation.PasteTextToGdForm(text);
            Log("Test paste GDForm = " + ok);
            MessageBox.Show(ok ? "Đã paste vào GDForm." : "Không paste được vào GDForm.");
        }

        private void TestCopyVasManual_Click(object sender, EventArgs e)
        {
            try
            {
                var result = _vasSync.RunVasManualCopySequence(true);
                var first500 = result.Text == null ? string.Empty : result.Text.Substring(0, Math.Min(500, result.Text.Length));
                var hasText = !string.IsNullOrWhiteSpace(result.Text);
                MessageBox.Show(
                    $"Test copy VAS manual sequence: {(hasText ? "SUCCESS" : "FAIL")}\nText length: {(result.Text?.Length ?? 0)}\nValidation: {result.IsValid}\nFlags: {result.ValidationFlags}\nFirst 500 chars:\n{first500}\n\nLog: {result.DebugLogPath}",
                    "Test copy VAS manual sequence",
                    MessageBoxButtons.OK,
                    hasText ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                Log($"Test copy VAS manual sequence = {(hasText ? "SUCCESS" : "FAIL")}, length={(result.Text?.Length ?? 0)}, validation={result.IsValid}, log: {result.DebugLogPath}");
            }
            catch (Exception ex)
            {
                var logPath = SupportEdit.Services.DebugLogService.SaveDebugLog("TestCopyVasManual_Error", ex.ToString());
                MessageBox.Show($"Test copy VAS manual sequence thất bại.\nLog: {logPath}", "Test copy VAS manual sequence", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log("Test copy VAS manual sequence error: " + ex.Message);
            }
        }

        private void TestCopyVasPasteGdForm_Click(object sender, EventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("ACTION: TestCopyVasPasteGdForm");
            sb.AppendLine($"Start: {DateTime.Now:O}");
            sb.AppendLine($"DebugReadsFolder: {SupportEdit.Services.DebugLogService.GetDebugReadsFolder()}");

            try
            {
                var vasCopy = _vasSync.RunVasManualCopySequence(true);
                var vasText = vasCopy.Text ?? string.Empty;
                sb.AppendLine($"VAS copy debug log: {vasCopy.DebugLogPath}");
                sb.AppendLine($"VAS text length: {vasText.Length}");
                sb.AppendLine($"VAS validation: {vasCopy.IsValid}");
                sb.AppendLine($"VAS validation flags: {vasCopy.ValidationFlags}");
                sb.AppendLine("VAS first 500 chars:");
                sb.AppendLine(vasText.Substring(0, Math.Min(500, vasText.Length)));

                if (string.IsNullOrWhiteSpace(vasText))
                {
                    throw new InvalidOperationException("Không đọc được nội dung VAS để paste vào GDForm.");
                }

                var xmlWindow = _gdFormAutomation.FindXmlAppWindow();
                sb.AppendLine($"XML window handle: 0x{xmlWindow.ToInt64():X}");
                if (xmlWindow == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Không tìm thấy cửa sổ phần mềm XML.");
                }

                var opened = _gdFormAutomation.OpenGdFormFromXmlApp();
                sb.AppendLine($"Open GDForm requested: {opened}");
                if (!opened)
                {
                    throw new InvalidOperationException("Không mở được GDForm/Cập nhật mã giao dịch.");
                }

                var gdWindow = WaitForGdFormWindow();
                sb.AppendLine($"GDForm window handle: 0x{gdWindow.ToInt64():X}");
                if (gdWindow == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Không tìm thấy GDForm sau khi mở.");
                }

                var pasted = _gdFormAutomation.PasteTextToGdForm(vasText);
                sb.AppendLine($"Paste to GDForm success: {pasted}");
                if (!pasted)
                {
                    throw new InvalidOperationException("Không paste được nội dung VAS vào txtbody GDForm.");
                }

                sb.AppendLine("NOTE: Test này chỉ paste vào GDForm, không bấm Chuyển dữ liệu.");
                var logPath = SupportEdit.Services.DebugLogService.SaveDebugLog("TestCopyVasPasteGdForm", sb.ToString());
                Log($"Test copy VAS paste GDForm = SUCCESS, length={vasText.Length}, log: {logPath}");
                MessageBox.Show(
                    $"Đã copy VAS và paste vào GDForm.\nText length: {vasText.Length}\nKhông bấm Chuyển dữ liệu.\n\nLog: {logPath}",
                    "Test copy VAS paste GDForm",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                sb.AppendLine("Exception:");
                sb.AppendLine(ex.ToString());
                var logPath = SupportEdit.Services.DebugLogService.SaveDebugLog("TestCopyVasPasteGdForm_Error", sb.ToString());
                Log($"Test copy VAS paste GDForm error: {ex.Message}, log: {logPath}");
                MessageBox.Show(
                    $"Test copy VAS paste GDForm thất bại.\n\n{ex.Message}\n\nLog: {logPath}",
                    "Test copy VAS paste GDForm",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void TestClickVasLog_Click(object sender, EventArgs e)
        {
            try
            {
                var info = _vasSync.TestClickVasLog();
                var logPath = _vasSync.SaveTestClickVasLogDebug(info);
                if (info == null)
                {
                    MessageBox.Show($"Không tìm thấy/click được VAS log.\nLog: {logPath}", "Test click VAS log", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                MessageBox.Show(
                    $"Handle: 0x{info.Handle.ToInt64():X}\nClassName: {info.ClassName}\nWindowText: {info.WindowText}\nProcessId: {info.ProcessId}\nParent: 0x{info.ParentHandle.ToInt64():X} / {info.ParentClassName}\nLog: {logPath}",
                    "Test click VAS log",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                var logPath = SupportEdit.Services.DebugLogService.SaveDebugLog("TestClickVasLog_Error", ex.ToString());
                MessageBox.Show($"Test click VAS log thất bại.\nLog: {logPath}", "Test click VAS log", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void TestVasOffsets_Click(object sender, EventArgs e)
        {
            var hasRect = _vasSync.GetVasWindowRect(out int x, out int y, out int width, out int height);
            MessageBox.Show(
                $"VasSuccessTabOffsetX/Y: {_config.VasSuccessTabOffsetX}/{_config.VasSuccessTabOffsetY}\nVasLogClickOffsetX/Y: {_config.VasLogClickOffsetX}/{_config.VasLogClickOffsetY}\nVAS window rect: {(hasRect ? $"X={x}, Y={y}, W={width}, H={height}" : "<not found>")}",
                "Test VAS offsets",
                MessageBoxButtons.OK,
                hasRect ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
    }
}
