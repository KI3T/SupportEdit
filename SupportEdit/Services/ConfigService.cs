using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SupportEdit.Models;

namespace SupportEdit.Services
{
    // Quản lý cấu hình đơn giản như tiêu đề cửa sổ và danh sách practitioners
    public class ConfigService
    {
        private const string PractitionersFileName = "practitioners.json";
        private const string ConfigFileName = "appconfig.json";

        // Những cấu hình mở rộng theo yêu cầu
        public string XmlWindowTitle { get; set; } = "Siêu phần mềm";
        public string XmlAppWindowTitleKeyword { get; set; } = "Siêu phần mềm 3176";
        public int FindDelayMs { get; set; } = 300;
        public int ClipboardRetryCount { get; set; } = 10;
        public int ClipboardRetryDelayMs { get; set; } = 300;
        public string XmlAppProcessName { get; set; } = string.Empty;
        // Automation IDs
        public string ErrorControlAutomationId { get; set; } = "txtError";
        public string XmlControlAutomationIdPrefix { get; set; } = "xml";

        // Direct index scan edit settings
        public bool UseDirectIndexScanEdit { get; set; } = false;
        public int DirectScanMaxLines { get; set; } = 30000;
        public int DirectScanKeyDelayMs { get; set; } = 20;
        public int DirectScanClipboardRetryCount { get; set; } = 5;
        public int DirectScanClipboardRetryDelayMs { get; set; } = 50;

        // New: Direct find then local scan edit settings (default enabled, optimized)
        public bool UseDirectFindThenLocalScanEdit { get; set; } = true;
        public int DirectLocalScanMaxLinesWithinBlock { get; set; } = 80;
        public int DirectLocalScanKeyDelayMs { get; set; } = 5;
        public int DirectLocalScanClipboardRetryCount { get; set; } = 2;
        public int DirectLocalScanClipboardRetryDelayMs { get; set; } = 20;
        public bool VerboseLineScanLog { get; set; } = false;
        public int DirectFindThenLocalTotalTimeoutMs { get; set; } = 15000;

        // New: Fast Direct Edit mode (Ctrl+F navigation, no XML read, minimal logging)
        public bool UseFastDirectEdit { get; set; } = true;
        public int FastDirectMaxScanLines { get; set; } = 80;
        public int FastDirectKeyDelayMs { get; set; } = 5;
        public int FastDirectClipboardDelayMs { get; set; } = 20;
        public int FastDirectTotalTimeoutMs { get; set; } = 15000;

        // New: Fast mouse-only focus (pure click, no UI Automation - for performance)
        public bool UseFastMouseFocusOnly { get; set; } = true;
        public bool UseUiAutomationFocusForXml { get; set; } = false;
        public int FastFocusTimeoutMs { get; set; } = 800;

        // VAS / GDForm sync settings
        public string VasProcessName { get; set; } = "EISClient";
        public string VasWindowTitleKeyword { get; set; } = "Đồng bộ dữ liệu";
        public string VasMainWindowTitleKeyword { get; set; } = "PHẦN MỀM ĐỒNG BỘ DỮ LIỆU";
        public bool UseVasMouseFallback { get; set; } = true;
        public int VasSuccessTabOffsetX { get; set; } = 0;
        public int VasSuccessTabOffsetY { get; set; } = 0;
        public int VasLogClickOffsetX { get; set; } = 450;
        public int VasLogClickOffsetY { get; set; } = 260;
        public int VasAutomationTimeoutMs { get; set; } = 8000;
        public bool UseVasManualCopySequence { get; set; } = true;
        public bool VasCopyUseSendInput { get; set; } = true;
        public int VasCopyDelayAfterClickMs { get; set; } = 150;
        public int VasCopyDelayAfterCtrlHomeMs { get; set; } = 80;
        public int VasCopyDelayAfterSelectMs { get; set; } = 150;
        public int VasClipboardRetryCount { get; set; } = 5;
        public int VasClipboardRetryDelayMs { get; set; } = 200;
        public int MaGDButtonOffsetX { get; set; } = 1080;
        public int MaGDButtonOffsetY { get; set; } = 145;
        public string GdFormTitleKeyword { get; set; } = "Cập nhật mã giao dịch";
        public int GdTxtBodyOffsetX { get; set; } = 420;
        public int GdTxtBodyOffsetY { get; set; } = 230;
        public int GdConvertButtonOffsetX { get; set; } = 740;
        public int GdConvertButtonOffsetY { get; set; } = 255;
        public int GdFormOpenTimeoutMs { get; set; } = 5000;

        // Mouse click focus offsets for each XML control (per-XML or fallback to Xml3)
        public int Xml1ClickOffsetX { get; set; } = 600;
        public int Xml1ClickOffsetY { get; set; } = 500;
        public int Xml2ClickOffsetX { get; set; } = 600;
        public int Xml2ClickOffsetY { get; set; } = 500;
        public int Xml3ClickOffsetX { get; set; } = 600;
        public int Xml3ClickOffsetY { get; set; } = 500;
        public int Xml4ClickOffsetX { get; set; } = 600;
        public int Xml4ClickOffsetY { get; set; } = 500;

        // Find-specific focus behavior: click before Find only, not after
        public bool ClickBeforeFindOnly { get; set; } = true;
        public bool RefocusByMouseAfterFind { get; set; } = false;
        public bool AutoFixNguoiThucHien { get; set; } = false;

        // Legacy: Mouse click focus fallback for auto-focusing XML controls (deprecated, use per-XML above)
        public bool UseMouseClickFocusFallback { get; set; } = true;

        // Backwards-compat alias used previously
        public bool UseDirectUiEditFallback { get => UseDirectIndexScanEdit; set => UseDirectIndexScanEdit = value; }

        private static string AppDataDirectory => AppContext.BaseDirectory;
        private static string PractitionersFilePath => Path.Combine(AppDataDirectory, PractitionersFileName);
        private static string ConfigPath => Path.Combine(AppDataDirectory, ConfigFileName);

        public string ConfigFilePath => ConfigPath;

        public List<PractitionerItem> Practitioners { get; private set; } = new List<PractitionerItem>();

        public void Load()
        {
            // nếu chưa có practitioners.json thì tạo mẫu
            if (!File.Exists(PractitionersFilePath))
            {
                var sample = new List<PractitionerItem>
                {
                    new PractitionerItem{ CategoryName = "Huyết học", Code = "030390/HCM-CCHN", Description = "Người thực hiện mặc định cho xét nghiệm huyết học" },
                    new PractitionerItem{ CategoryName = "Vi sinh", Code = "025633/HCM-CCHN", Description = "Người thực hiện mặc định cho xét nghiệm vi sinh" },
                    new PractitionerItem{ CategoryName = "Sinh hóa", Code = "012345/HCM-CCHN", Description = "Người thực hiện mặc định cho xét nghiệm sinh hóa" }
                };
                try { File.WriteAllText(PractitionersFilePath, JsonSerializer.Serialize(sample, new JsonSerializerOptions{ WriteIndented = true })); }
                catch { }
            }

            if (File.Exists(PractitionersFilePath))
            {
                try
                {
                    var txt = File.ReadAllText(PractitionersFilePath);
                    Practitioners = JsonSerializer.Deserialize<List<PractitionerItem>>(txt) ?? new List<PractitionerItem>();
                }
                catch
                {
                    Practitioners = new List<PractitionerItem>();
                }
            }

            // Nếu chưa có config app thì tạo mẫu (with new optimized defaults)
            if (!File.Exists(ConfigPath))
            {
                var sample = new Dictionary<string, object>
                {
                    { "XmlAppWindowTitleKeyword", XmlAppWindowTitleKeyword },
                    { "XmlAppProcessName", XmlAppProcessName },
                    { "ErrorControlAutomationId", ErrorControlAutomationId },
                    { "XmlControlAutomationIdPrefix", XmlControlAutomationIdPrefix },
                    { "FindDelayMs", FindDelayMs },
                    { "ClipboardRetryCount", ClipboardRetryCount },
                    { "ClipboardRetryDelayMs", ClipboardRetryDelayMs },
                    { "UseDirectFindThenLocalScanEdit", true },
                    { "UseDirectIndexScanEdit", false },
                    { "UseDirectUiEditFallback", false },
                    { "DirectLocalScanMaxLinesWithinBlock", 80 },
                    { "DirectLocalScanKeyDelayMs", 5 },
                    { "DirectLocalScanClipboardRetryCount", 2 },
                    { "DirectLocalScanClipboardRetryDelayMs", 20 },
                    { "VerboseLineScanLog", false },
                    { "DirectFindThenLocalTotalTimeoutMs", 15000 },
                    { "UseFastDirectEdit", true },
                    { "FastDirectMaxScanLines", 80 },
                    { "FastDirectKeyDelayMs", 5 },
                    { "FastDirectClipboardDelayMs", 20 },
                    { "FastDirectTotalTimeoutMs", 15000 },
                    { "UseFastMouseFocusOnly", true },
                    { "UseUiAutomationFocusForXml", false },
                    { "FastFocusTimeoutMs", 800 },
                    { "VasProcessName", VasProcessName },
                    { "VasWindowTitleKeyword", VasWindowTitleKeyword },
                    { "VasMainWindowTitleKeyword", VasMainWindowTitleKeyword },
                    { "UseVasMouseFallback", UseVasMouseFallback },
                    { "VasSuccessTabOffsetX", VasSuccessTabOffsetX },
                    { "VasSuccessTabOffsetY", VasSuccessTabOffsetY },
                    { "VasLogClickOffsetX", VasLogClickOffsetX },
                    { "VasLogClickOffsetY", VasLogClickOffsetY },
                    { "VasAutomationTimeoutMs", VasAutomationTimeoutMs },
                    { "UseVasManualCopySequence", UseVasManualCopySequence },
                    { "VasCopyUseSendInput", VasCopyUseSendInput },
                    { "VasCopyDelayAfterClickMs", VasCopyDelayAfterClickMs },
                    { "VasCopyDelayAfterCtrlHomeMs", VasCopyDelayAfterCtrlHomeMs },
                    { "VasCopyDelayAfterSelectMs", VasCopyDelayAfterSelectMs },
                    { "VasClipboardRetryCount", VasClipboardRetryCount },
                    { "VasClipboardRetryDelayMs", VasClipboardRetryDelayMs },
                    { "MaGDButtonOffsetX", MaGDButtonOffsetX },
                    { "MaGDButtonOffsetY", MaGDButtonOffsetY },
                    { "GdFormTitleKeyword", GdFormTitleKeyword },
                    { "GdTxtBodyOffsetX", GdTxtBodyOffsetX },
                    { "GdTxtBodyOffsetY", GdTxtBodyOffsetY },
                    { "GdConvertButtonOffsetX", GdConvertButtonOffsetX },
                    { "GdConvertButtonOffsetY", GdConvertButtonOffsetY },
                    { "GdFormOpenTimeoutMs", GdFormOpenTimeoutMs },
                    { "Xml1ClickOffsetX", 600 },
                    { "Xml1ClickOffsetY", 500 },
                    { "Xml2ClickOffsetX", 600 },
                    { "Xml2ClickOffsetY", 500 },
                    { "Xml3ClickOffsetX", 600 },
                    { "Xml3ClickOffsetY", 500 },
                    { "Xml4ClickOffsetX", 600 },
                    { "Xml4ClickOffsetY", 500 },
                    { "UseMouseClickFocusFallback", true },
                    { "ClickBeforeFindOnly", true },
                    { "RefocusByMouseAfterFind", false }
                    ,{ "AutoFixNguoiThucHien", false }
                };
                try { File.WriteAllText(ConfigPath, JsonSerializer.Serialize(sample, new JsonSerializerOptions{ WriteIndented = true })); }
                catch { }
            }

            if (File.Exists(ConfigPath))
            {
                try
                {
                    var txt = File.ReadAllText(ConfigPath);
                    var doc = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(txt);
                    if (doc != null)
                    {
                        if (doc.ContainsKey("XmlAppWindowTitleKeyword")) XmlAppWindowTitleKeyword = doc["XmlAppWindowTitleKeyword"].GetString();
                        if (doc.ContainsKey("XmlAppProcessName")) XmlAppProcessName = doc["XmlAppProcessName"].GetString();
                        if (doc.ContainsKey("ErrorControlAutomationId")) ErrorControlAutomationId = doc["ErrorControlAutomationId"].GetString();
                        if (doc.ContainsKey("XmlControlAutomationIdPrefix")) XmlControlAutomationIdPrefix = doc["XmlControlAutomationIdPrefix"].GetString();
                        if (doc.ContainsKey("VasProcessName")) VasProcessName = doc["VasProcessName"].GetString();
                        if (doc.ContainsKey("VasWindowTitleKeyword")) VasWindowTitleKeyword = doc["VasWindowTitleKeyword"].GetString();
                        if (doc.ContainsKey("VasMainWindowTitleKeyword")) VasMainWindowTitleKeyword = doc["VasMainWindowTitleKeyword"].GetString();
                        if (doc.ContainsKey("UseVasMouseFallback")) UseVasMouseFallback = doc["UseVasMouseFallback"].GetBoolean();
                        if (doc.ContainsKey("VasSuccessTabOffsetX")) VasSuccessTabOffsetX = doc["VasSuccessTabOffsetX"].GetInt32();
                        if (doc.ContainsKey("VasSuccessTabOffsetY")) VasSuccessTabOffsetY = doc["VasSuccessTabOffsetY"].GetInt32();
                        if (doc.ContainsKey("VasLogClickOffsetX")) VasLogClickOffsetX = doc["VasLogClickOffsetX"].GetInt32();
                        if (doc.ContainsKey("VasLogClickOffsetY")) VasLogClickOffsetY = doc["VasLogClickOffsetY"].GetInt32();
                        if (doc.ContainsKey("VasAutomationTimeoutMs")) VasAutomationTimeoutMs = doc["VasAutomationTimeoutMs"].GetInt32();
                        if (doc.ContainsKey("UseVasManualCopySequence")) UseVasManualCopySequence = doc["UseVasManualCopySequence"].GetBoolean();
                        if (doc.ContainsKey("VasCopyUseSendInput")) VasCopyUseSendInput = doc["VasCopyUseSendInput"].GetBoolean();
                        if (doc.ContainsKey("VasCopyDelayAfterClickMs")) VasCopyDelayAfterClickMs = doc["VasCopyDelayAfterClickMs"].GetInt32();
                        if (doc.ContainsKey("VasCopyDelayAfterCtrlHomeMs")) VasCopyDelayAfterCtrlHomeMs = doc["VasCopyDelayAfterCtrlHomeMs"].GetInt32();
                        if (doc.ContainsKey("VasCopyDelayAfterSelectMs")) VasCopyDelayAfterSelectMs = doc["VasCopyDelayAfterSelectMs"].GetInt32();
                        if (doc.ContainsKey("VasClipboardRetryCount")) VasClipboardRetryCount = doc["VasClipboardRetryCount"].GetInt32();
                        if (doc.ContainsKey("VasClipboardRetryDelayMs")) VasClipboardRetryDelayMs = doc["VasClipboardRetryDelayMs"].GetInt32();
                        if (doc.ContainsKey("GdFormTitleKeyword")) GdFormTitleKeyword = doc["GdFormTitleKeyword"].GetString();
                        if (doc.ContainsKey("MaGDButtonOffsetX")) MaGDButtonOffsetX = doc["MaGDButtonOffsetX"].GetInt32();
                        if (doc.ContainsKey("MaGDButtonOffsetY")) MaGDButtonOffsetY = doc["MaGDButtonOffsetY"].GetInt32();
                        if (doc.ContainsKey("GdTxtBodyOffsetX")) GdTxtBodyOffsetX = doc["GdTxtBodyOffsetX"].GetInt32();
                        if (doc.ContainsKey("GdTxtBodyOffsetY")) GdTxtBodyOffsetY = doc["GdTxtBodyOffsetY"].GetInt32();
                        if (doc.ContainsKey("GdConvertButtonOffsetX")) GdConvertButtonOffsetX = doc["GdConvertButtonOffsetX"].GetInt32();
                        if (doc.ContainsKey("GdConvertButtonOffsetY")) GdConvertButtonOffsetY = doc["GdConvertButtonOffsetY"].GetInt32();
                        if (doc.ContainsKey("GdFormOpenTimeoutMs")) GdFormOpenTimeoutMs = doc["GdFormOpenTimeoutMs"].GetInt32();
                        if (doc.ContainsKey("FindDelayMs")) FindDelayMs = doc["FindDelayMs"].GetInt32();
                        if (doc.ContainsKey("ClipboardRetryCount")) ClipboardRetryCount = doc["ClipboardRetryCount"].GetInt32();
                        if (doc.ContainsKey("ClipboardRetryDelayMs")) ClipboardRetryDelayMs = doc["ClipboardRetryDelayMs"].GetInt32();
                        if (doc.ContainsKey("UseDirectIndexScanEdit")) UseDirectIndexScanEdit = doc["UseDirectIndexScanEdit"].GetBoolean();
                        if (doc.ContainsKey("DirectScanMaxLines")) DirectScanMaxLines = doc["DirectScanMaxLines"].GetInt32();
                        if (doc.ContainsKey("DirectScanKeyDelayMs")) DirectScanKeyDelayMs = doc["DirectScanKeyDelayMs"].GetInt32();
                        if (doc.ContainsKey("DirectScanClipboardRetryCount")) DirectScanClipboardRetryCount = doc["DirectScanClipboardRetryCount"].GetInt32();
                        if (doc.ContainsKey("DirectScanClipboardRetryDelayMs")) DirectScanClipboardRetryDelayMs = doc["DirectScanClipboardRetryDelayMs"].GetInt32();
                        // new keys
                        if (doc.ContainsKey("UseDirectFindThenLocalScanEdit")) UseDirectFindThenLocalScanEdit = doc["UseDirectFindThenLocalScanEdit"].GetBoolean();
                        if (doc.ContainsKey("DirectLocalScanMaxLinesWithinBlock")) DirectLocalScanMaxLinesWithinBlock = doc["DirectLocalScanMaxLinesWithinBlock"].GetInt32();
                        if (doc.ContainsKey("DirectLocalScanKeyDelayMs")) DirectLocalScanKeyDelayMs = doc["DirectLocalScanKeyDelayMs"].GetInt32();
                        if (doc.ContainsKey("DirectLocalScanClipboardRetryCount")) DirectLocalScanClipboardRetryCount = doc["DirectLocalScanClipboardRetryCount"].GetInt32();
                        if (doc.ContainsKey("DirectLocalScanClipboardRetryDelayMs")) DirectLocalScanClipboardRetryDelayMs = doc["DirectLocalScanClipboardRetryDelayMs"].GetInt32();
                        if (doc.ContainsKey("UseDirectUiEditFallback")) UseDirectUiEditFallback = doc["UseDirectUiEditFallback"].GetBoolean();
                        if (doc.ContainsKey("VerboseLineScanLog")) VerboseLineScanLog = doc["VerboseLineScanLog"].GetBoolean();
                        if (doc.ContainsKey("DirectFindThenLocalTotalTimeoutMs")) DirectFindThenLocalTotalTimeoutMs = doc["DirectFindThenLocalTotalTimeoutMs"].GetInt32();
                        // Fast Direct Edit keys
                        if (doc.ContainsKey("UseFastDirectEdit")) UseFastDirectEdit = doc["UseFastDirectEdit"].GetBoolean();
                        if (doc.ContainsKey("FastDirectMaxScanLines")) FastDirectMaxScanLines = doc["FastDirectMaxScanLines"].GetInt32();
                        if (doc.ContainsKey("FastDirectKeyDelayMs")) FastDirectKeyDelayMs = doc["FastDirectKeyDelayMs"].GetInt32();
                        if (doc.ContainsKey("FastDirectClipboardDelayMs")) FastDirectClipboardDelayMs = doc["FastDirectClipboardDelayMs"].GetInt32();
                        if (doc.ContainsKey("FastDirectTotalTimeoutMs")) FastDirectTotalTimeoutMs = doc["FastDirectTotalTimeoutMs"].GetInt32();
                        // Fast mouse-only focus keys
                        if (doc.ContainsKey("UseFastMouseFocusOnly")) UseFastMouseFocusOnly = doc["UseFastMouseFocusOnly"].GetBoolean();
                        if (doc.ContainsKey("UseUiAutomationFocusForXml")) UseUiAutomationFocusForXml = doc["UseUiAutomationFocusForXml"].GetBoolean();
                        if (doc.ContainsKey("FastFocusTimeoutMs")) FastFocusTimeoutMs = doc["FastFocusTimeoutMs"].GetInt32();
                        // Per-XML mouse click offsets
                        if (doc.ContainsKey("Xml1ClickOffsetX")) Xml1ClickOffsetX = doc["Xml1ClickOffsetX"].GetInt32();
                        if (doc.ContainsKey("Xml1ClickOffsetY")) Xml1ClickOffsetY = doc["Xml1ClickOffsetY"].GetInt32();
                        if (doc.ContainsKey("Xml2ClickOffsetX")) Xml2ClickOffsetX = doc["Xml2ClickOffsetX"].GetInt32();
                        if (doc.ContainsKey("Xml2ClickOffsetY")) Xml2ClickOffsetY = doc["Xml2ClickOffsetY"].GetInt32();
                        if (doc.ContainsKey("Xml3ClickOffsetX")) Xml3ClickOffsetX = doc["Xml3ClickOffsetX"].GetInt32();
                        if (doc.ContainsKey("Xml3ClickOffsetY")) Xml3ClickOffsetY = doc["Xml3ClickOffsetY"].GetInt32();
                        if (doc.ContainsKey("Xml4ClickOffsetX")) Xml4ClickOffsetX = doc["Xml4ClickOffsetX"].GetInt32();
                        if (doc.ContainsKey("Xml4ClickOffsetY")) Xml4ClickOffsetY = doc["Xml4ClickOffsetY"].GetInt32();
                        // Legacy fallback
                        if (doc.ContainsKey("UseMouseClickFocusFallback")) UseMouseClickFocusFallback = doc["UseMouseClickFocusFallback"].GetBoolean();
                        // Find-specific focus behavior
                        if (doc.ContainsKey("ClickBeforeFindOnly")) ClickBeforeFindOnly = doc["ClickBeforeFindOnly"].GetBoolean();
                        if (doc.ContainsKey("RefocusByMouseAfterFind")) RefocusByMouseAfterFind = doc["RefocusByMouseAfterFind"].GetBoolean();
                        if (doc.ContainsKey("AutoFixNguoiThucHien")) AutoFixNguoiThucHien = doc["AutoFixNguoiThucHien"].GetBoolean();

                        // Ensure missing keys are written back (add any missing new keys)
                        var normalized = new Dictionary<string, object>
                        {
                            { "XmlAppWindowTitleKeyword", XmlAppWindowTitleKeyword },
                            { "XmlAppProcessName", XmlAppProcessName },
                            { "ErrorControlAutomationId", ErrorControlAutomationId },
                            { "XmlControlAutomationIdPrefix", XmlControlAutomationIdPrefix },
                            { "FindDelayMs", FindDelayMs },
                            { "ClipboardRetryCount", ClipboardRetryCount },
                            { "ClipboardRetryDelayMs", ClipboardRetryDelayMs },
                            { "UseDirectIndexScanEdit", UseDirectIndexScanEdit },
                            { "DirectScanMaxLines", DirectScanMaxLines },
                            { "DirectScanKeyDelayMs", DirectScanKeyDelayMs },
                            { "DirectScanClipboardRetryCount", DirectScanClipboardRetryCount },
                            { "DirectScanClipboardRetryDelayMs", DirectScanClipboardRetryDelayMs },
                            { "UseDirectFindThenLocalScanEdit", UseDirectFindThenLocalScanEdit },
                            { "UseDirectUiEditFallback", UseDirectUiEditFallback },
                            { "DirectLocalScanMaxLinesWithinBlock", DirectLocalScanMaxLinesWithinBlock },
                            { "DirectLocalScanKeyDelayMs", DirectLocalScanKeyDelayMs },
                            { "DirectLocalScanClipboardRetryCount", DirectLocalScanClipboardRetryCount },
                            { "DirectLocalScanClipboardRetryDelayMs", DirectLocalScanClipboardRetryDelayMs },
                            { "VerboseLineScanLog", VerboseLineScanLog },
                            { "DirectFindThenLocalTotalTimeoutMs", DirectFindThenLocalTotalTimeoutMs },
                            { "UseFastDirectEdit", UseFastDirectEdit },
                            { "FastDirectMaxScanLines", FastDirectMaxScanLines },
                            { "FastDirectKeyDelayMs", FastDirectKeyDelayMs },
                            { "FastDirectClipboardDelayMs", FastDirectClipboardDelayMs },
                            { "FastDirectTotalTimeoutMs", FastDirectTotalTimeoutMs },
                            { "UseFastMouseFocusOnly", UseFastMouseFocusOnly },
                            { "UseUiAutomationFocusForXml", UseUiAutomationFocusForXml },
                            { "FastFocusTimeoutMs", FastFocusTimeoutMs },
                            { "VasProcessName", VasProcessName },
                            { "VasWindowTitleKeyword", VasWindowTitleKeyword },
                            { "VasMainWindowTitleKeyword", VasMainWindowTitleKeyword },
                            { "UseVasMouseFallback", UseVasMouseFallback },
                            { "VasSuccessTabOffsetX", VasSuccessTabOffsetX },
                            { "VasSuccessTabOffsetY", VasSuccessTabOffsetY },
                            { "VasLogClickOffsetX", VasLogClickOffsetX },
                            { "VasLogClickOffsetY", VasLogClickOffsetY },
                            { "VasAutomationTimeoutMs", VasAutomationTimeoutMs },
                            { "UseVasManualCopySequence", UseVasManualCopySequence },
                            { "VasCopyUseSendInput", VasCopyUseSendInput },
                            { "VasCopyDelayAfterClickMs", VasCopyDelayAfterClickMs },
                            { "VasCopyDelayAfterCtrlHomeMs", VasCopyDelayAfterCtrlHomeMs },
                            { "VasCopyDelayAfterSelectMs", VasCopyDelayAfterSelectMs },
                            { "VasClipboardRetryCount", VasClipboardRetryCount },
                            { "VasClipboardRetryDelayMs", VasClipboardRetryDelayMs },
                            { "MaGDButtonOffsetX", MaGDButtonOffsetX },
                            { "MaGDButtonOffsetY", MaGDButtonOffsetY },
                            { "GdFormTitleKeyword", GdFormTitleKeyword },
                            { "GdTxtBodyOffsetX", GdTxtBodyOffsetX },
                            { "GdTxtBodyOffsetY", GdTxtBodyOffsetY },
                            { "GdConvertButtonOffsetX", GdConvertButtonOffsetX },
                            { "GdConvertButtonOffsetY", GdConvertButtonOffsetY },
                            { "GdFormOpenTimeoutMs", GdFormOpenTimeoutMs },
                            { "Xml1ClickOffsetX", Xml1ClickOffsetX },
                            { "Xml1ClickOffsetY", Xml1ClickOffsetY },
                            { "Xml2ClickOffsetX", Xml2ClickOffsetX },
                            { "Xml2ClickOffsetY", Xml2ClickOffsetY },
                            { "Xml3ClickOffsetX", Xml3ClickOffsetX },
                            { "Xml3ClickOffsetY", Xml3ClickOffsetY },
                            { "Xml4ClickOffsetX", Xml4ClickOffsetX },
                            { "Xml4ClickOffsetY", Xml4ClickOffsetY },
                            { "UseMouseClickFocusFallback", UseMouseClickFocusFallback },
                            { "ClickBeforeFindOnly", ClickBeforeFindOnly },
                            { "RefocusByMouseAfterFind", RefocusByMouseAfterFind }
                            ,{ "AutoFixNguoiThucHien", AutoFixNguoiThucHien }
                        };

                        try
                        {
                            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(normalized, new JsonSerializerOptions { WriteIndented = true }));
                        }
                        catch { }
                    }
                }
                catch
                {
                }
            }
        }

        public void Save()
        {
            try
            {
                var txt = JsonSerializer.Serialize(Practitioners, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(PractitionersFilePath, txt);
            }
            catch { }

            try
            {
                var doc = new Dictionary<string, object>
                {
                    { "XmlAppWindowTitleKeyword", XmlAppWindowTitleKeyword },
                    { "XmlAppProcessName", XmlAppProcessName },
                    { "ErrorControlAutomationId", ErrorControlAutomationId },
                    { "XmlControlAutomationIdPrefix", XmlControlAutomationIdPrefix },
                    { "FindDelayMs", FindDelayMs },
                    { "ClipboardRetryCount", ClipboardRetryCount },
                    { "ClipboardRetryDelayMs", ClipboardRetryDelayMs },
                    { "UseDirectIndexScanEdit", UseDirectIndexScanEdit },
                    { "DirectScanMaxLines", DirectScanMaxLines },
                    { "DirectScanKeyDelayMs", DirectScanKeyDelayMs },
                    { "DirectScanClipboardRetryCount", DirectScanClipboardRetryCount },
                    { "DirectScanClipboardRetryDelayMs", DirectScanClipboardRetryDelayMs },
                    { "UseDirectFindThenLocalScanEdit", UseDirectFindThenLocalScanEdit },
                    { "UseDirectUiEditFallback", UseDirectUiEditFallback },
                    { "DirectLocalScanMaxLinesWithinBlock", DirectLocalScanMaxLinesWithinBlock },
                    { "DirectLocalScanKeyDelayMs", DirectLocalScanKeyDelayMs },
                    { "DirectLocalScanClipboardRetryCount", DirectLocalScanClipboardRetryCount },
                    { "DirectLocalScanClipboardRetryDelayMs", DirectLocalScanClipboardRetryDelayMs },
                    { "VerboseLineScanLog", VerboseLineScanLog },
                    { "DirectFindThenLocalTotalTimeoutMs", DirectFindThenLocalTotalTimeoutMs },
                    { "UseFastDirectEdit", UseFastDirectEdit },
                    { "FastDirectMaxScanLines", FastDirectMaxScanLines },
                    { "FastDirectKeyDelayMs", FastDirectKeyDelayMs },
                    { "FastDirectClipboardDelayMs", FastDirectClipboardDelayMs },
                    { "FastDirectTotalTimeoutMs", FastDirectTotalTimeoutMs },
                    { "UseFastMouseFocusOnly", UseFastMouseFocusOnly },
                    { "UseUiAutomationFocusForXml", UseUiAutomationFocusForXml },
                    { "FastFocusTimeoutMs", FastFocusTimeoutMs },
                    { "VasProcessName", VasProcessName },
                    { "VasWindowTitleKeyword", VasWindowTitleKeyword },
                    { "VasMainWindowTitleKeyword", VasMainWindowTitleKeyword },
                    { "UseVasMouseFallback", UseVasMouseFallback },
                    { "VasSuccessTabOffsetX", VasSuccessTabOffsetX },
                    { "VasSuccessTabOffsetY", VasSuccessTabOffsetY },
                    { "VasLogClickOffsetX", VasLogClickOffsetX },
                    { "VasLogClickOffsetY", VasLogClickOffsetY },
                    { "VasAutomationTimeoutMs", VasAutomationTimeoutMs },
                    { "UseVasManualCopySequence", UseVasManualCopySequence },
                    { "VasCopyUseSendInput", VasCopyUseSendInput },
                    { "VasCopyDelayAfterClickMs", VasCopyDelayAfterClickMs },
                    { "VasCopyDelayAfterCtrlHomeMs", VasCopyDelayAfterCtrlHomeMs },
                    { "VasCopyDelayAfterSelectMs", VasCopyDelayAfterSelectMs },
                    { "VasClipboardRetryCount", VasClipboardRetryCount },
                    { "VasClipboardRetryDelayMs", VasClipboardRetryDelayMs },
                    { "MaGDButtonOffsetX", MaGDButtonOffsetX },
                    { "MaGDButtonOffsetY", MaGDButtonOffsetY },
                    { "GdFormTitleKeyword", GdFormTitleKeyword },
                    { "GdTxtBodyOffsetX", GdTxtBodyOffsetX },
                    { "GdTxtBodyOffsetY", GdTxtBodyOffsetY },
                    { "GdConvertButtonOffsetX", GdConvertButtonOffsetX },
                    { "GdConvertButtonOffsetY", GdConvertButtonOffsetY },
                    { "GdFormOpenTimeoutMs", GdFormOpenTimeoutMs },
                    { "Xml1ClickOffsetX", Xml1ClickOffsetX },
                    { "Xml1ClickOffsetY", Xml1ClickOffsetY },
                    { "Xml2ClickOffsetX", Xml2ClickOffsetX },
                    { "Xml2ClickOffsetY", Xml2ClickOffsetY },
                    { "Xml3ClickOffsetX", Xml3ClickOffsetX },
                    { "Xml3ClickOffsetY", Xml3ClickOffsetY },
                    { "Xml4ClickOffsetX", Xml4ClickOffsetX },
                    { "Xml4ClickOffsetY", Xml4ClickOffsetY },
                    { "UseMouseClickFocusFallback", UseMouseClickFocusFallback },
                    { "ClickBeforeFindOnly", ClickBeforeFindOnly },
                    { "RefocusByMouseAfterFind", RefocusByMouseAfterFind }
                    ,{ "AutoFixNguoiThucHien", AutoFixNguoiThucHien }
                };
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(doc, new JsonSerializerOptions{ WriteIndented = true }));
            }
            catch { }
        }
    }
}


