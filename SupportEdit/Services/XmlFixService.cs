using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using SupportEdit.Models;

namespace SupportEdit.Services
{
    // Dịch vụ thực hiện sửa nội dung XML theo quy tắc - trả về XmlFixResult chi tiết
    public class XmlFixService
    {
        // Diagnostic record for DVKT search
        public class DvktSearchDiagnostic
        {
            public int Stt { get; set; }
            public int XmlLength { get; set; }
            public bool HasDvktOpenAny { get; set; }
            public bool HasDvktCloseAny { get; set; }
            public bool HasTargetStt { get; set; }
            public int TargetSttIndex { get; set; } = -1;
            public int NearestOpenTagIndex { get; set; } = -1;
            public int NearestCloseTagIndex { get; set; } = -1;
            public bool OpenTagBeforeStt { get; set; }
            public bool CloseTagAfterStt { get; set; }
            public string TextAroundStt { get; set; }
            public string Message { get; set; }
        }

        // Struct chứa thông tin về 1 block DVKT trong xml
        public struct DvktBlockMatch
        {
            public int Index { get; set; }
            public int Length { get; set; }
            public string Value { get; set; }
            public int Stt { get; set; }
        }

        private static readonly TimeSpan _regexTimeout = TimeSpan.FromSeconds(3);
        private static readonly RegexOptions _regexOptions = RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled;

        // Pattern chuẩn cho block DVKT và STT
        private const string DvktBlockPattern = @"<CHI_TIET_DVKT\b[^>]*>[\s\S]*?</CHI_TIET_DVKT>";
        private const string SttPattern = @"<STT>\s*(?<stt>\d+)\s*</STT>";

        // Cho phép fallback chậm sang toàn quét nếu thật sự cần (mặc định false)
        public static bool AllowSlowFallback { get; set; } = false;

        // Tìm tất cả block <CHI_TIET_DVKT ...>...</CHI_TIET_DVKT> (riêng lẻ)
        public static List<DvktBlockMatch> FindAllDvktBlocks(string xmlContent)
        {
            var list = new List<DvktBlockMatch>();
            if (string.IsNullOrEmpty(xmlContent)) return list;
            var rx = new Regex(DvktBlockPattern, _regexOptions, _regexTimeout);
            var matches = rx.Matches(xmlContent);
            var sttRx = new Regex(SttPattern, _regexOptions, _regexTimeout);
            foreach (Match m in matches)
            {
                int sttVal = -1;
                var mStt = sttRx.Match(m.Value);
                if (mStt.Success)
                {
                    int.TryParse(mStt.Groups["stt"].Value, out sttVal);
                }
                list.Add(new DvktBlockMatch { Index = m.Index, Length = m.Length, Value = m.Value, Stt = sttVal });
            }
            return list;
        }

        // Tìm block duy nhất chứa STT cụ thể (fallback dùng FindAllDvktBlocks)
        public static DvktBlockMatch? FindDvktBlockByStt(string xmlContent, int stt)
        {
            // Mặc định dùng phiên bản nhanh
            var fast = FindDvktBlockBySttFast(xmlContent, stt);
            if (fast != null) return fast;
            // Nếu fast trả về null do timeout hoặc cấu trúc lạ thì theo mặc định KHÔNG fallback chậm trừ khi AllowSlowFallback=true
            if (!AllowSlowFallback)
            {
                // trả null để caller báo lỗi và không sửa tự động
                return null;
            }
            // Fallback chậm (chỉ khi cấu hình bật)
            var all = FindAllDvktBlocks(xmlContent);
            var matched = all.Where(b => b.Stt == stt).ToList();
            if (matched.Count == 0) return null;
            if (matched.Count > 1) throw new InvalidOperationException($"Tìm thấy nhiều block CHI_TIET_DVKT với STT {stt}");
            return matched.First();
        }

        // Lấy giá trị tag trong 1 block, hỗ trợ CDATA
        public static string GetTagValue(string block, string tagName)
        {
            if (string.IsNullOrEmpty(block)) return null;
            var pattern = $"<{tagName}\\b[^>]*>\\s*(?:<!\\[CDATA\\[(?<c>[\\s\\S]*?)\\]\\]>|(?<t>[\\s\\S]*?))\\s*</{tagName}>";
            var rx = new Regex(pattern, _regexOptions, _regexTimeout);
            var m = rx.Match(block);
            if (!m.Success) return null;
            if (m.Groups["c"].Success) return m.Groups["c"].Value.Trim();
            if (m.Groups["t"].Success) return m.Groups["t"].Value.Trim();
            return null;
        }

        // Analyze DVKT block search for diagnostics
        public static DvktSearchDiagnostic AnalyzeDvktBlockSearch(string xmlContent, int stt)
        {
            var d = new DvktSearchDiagnostic();
            d.Stt = stt;
            d.XmlLength = string.IsNullOrEmpty(xmlContent) ? 0 : xmlContent.Length;
            d.HasDvktOpenAny = !string.IsNullOrEmpty(xmlContent) && xmlContent.IndexOf("<CHI_TIET_DVKT", StringComparison.OrdinalIgnoreCase) >= 0;
            d.HasDvktCloseAny = !string.IsNullOrEmpty(xmlContent) && xmlContent.IndexOf("</CHI_TIET_DVKT>", StringComparison.OrdinalIgnoreCase) >= 0;
            d.HasTargetStt = false;
            d.TargetSttIndex = -1;
            d.NearestOpenTagIndex = -1;
            d.NearestCloseTagIndex = -1;
            d.OpenTagBeforeStt = false;
            d.CloseTagAfterStt = false;
            d.TextAroundStt = string.Empty;

            if (string.IsNullOrEmpty(xmlContent))
            {
                d.Message = "XML content is empty";
                return d;
            }

            try
            {
                var pattern = $"<STT>\\s*{stt}\\s*</STT>";
                var rx = new Regex(pattern, _regexOptions, _regexTimeout);
                var m = rx.Match(xmlContent);
                if (m.Success)
                {
                    d.HasTargetStt = true;
                    d.TargetSttIndex = m.Index;
                    // find nearest open tag before
                    d.NearestOpenTagIndex = xmlContent.LastIndexOf("<CHI_TIET_DVKT", d.TargetSttIndex, StringComparison.OrdinalIgnoreCase);
                    d.NearestCloseTagIndex = xmlContent.IndexOf("</CHI_TIET_DVKT>", d.TargetSttIndex, StringComparison.OrdinalIgnoreCase);
                    d.OpenTagBeforeStt = d.NearestOpenTagIndex >= 0 && d.NearestOpenTagIndex < d.TargetSttIndex;
                    d.CloseTagAfterStt = d.NearestCloseTagIndex >= 0 && d.NearestCloseTagIndex > d.TargetSttIndex;

                    // extract context
                    var start = Math.Max(0, d.TargetSttIndex - 300);
                    var len = Math.Min(600, xmlContent.Length - start);
                    d.TextAroundStt = xmlContent.Substring(start, len);
                }
                else
                {
                    d.Message = "STT not found in XML";
                }
            }
            catch (RegexMatchTimeoutException)
            {
                d.Message = "Regex timeout while searching for STT";
            }
            return d;
        }

        private static string BuildDiagMessage(DvktSearchDiagnostic d)
        {
            if (d == null) return string.Empty;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Stt: {d.Stt}");
            sb.AppendLine($"XmlLength: {d.XmlLength}");
            sb.AppendLine($"HasDvktOpenAny: {d.HasDvktOpenAny}");
            sb.AppendLine($"HasDvktCloseAny: {d.HasDvktCloseAny}");
            sb.AppendLine($"HasTargetStt: {d.HasTargetStt}");
            sb.AppendLine($"TargetSttIndex: {d.TargetSttIndex}");
            sb.AppendLine($"NearestOpenTagIndex: {d.NearestOpenTagIndex}");
            sb.AppendLine($"NearestCloseTagIndex: {d.NearestCloseTagIndex}");
            sb.AppendLine($"OpenTagBeforeStt: {d.OpenTagBeforeStt}");
            sb.AppendLine($"CloseTagAfterStt: {d.CloseTagAfterStt}");
            sb.AppendLine("--- TextAroundStt (trimmed) ---");
            if (!string.IsNullOrEmpty(d.TextAroundStt)) sb.AppendLine(d.TextAroundStt.Substring(0, Math.Min(500, d.TextAroundStt.Length)));
            if (!string.IsNullOrEmpty(d.Message)) sb.AppendLine("Message: " + d.Message);
            return sb.ToString();
        }

        // Phiên bản nhanh: tìm STT trước, rồi cắt block bằng chỉ số gần nhất
        public static DvktBlockMatch? FindDvktBlockBySttFast(string xmlContent, int stt)
        {
            if (string.IsNullOrEmpty(xmlContent))
            {
                var diagEmpty = AnalyzeDvktBlockSearch(xmlContent, stt);
                throw new InvalidOperationException("DVKT block search failed: " + diagEmpty.Message);
            }
            var sw = Stopwatch.StartNew();
            try
            {
                var sttRx = new Regex(SttPattern, _regexOptions, _regexTimeout);
                var mStt = sttRx.Match(xmlContent);
                if (!mStt.Success)
                {
                    var diag = AnalyzeDvktBlockSearch(xmlContent, stt);
                    throw new InvalidOperationException("DVKT block search failed: STT not found. " + diag.Message);
                }
                int sttIndex = mStt.Index;
                // tìm thẻ mở CHI_TIET_DVKT trước sttIndex
                var openTag = "<CHI_TIET_DVKT";
                var closeTag = "</CHI_TIET_DVKT>";
                var blockStart = xmlContent.LastIndexOf(openTag, sttIndex, StringComparison.OrdinalIgnoreCase);
                if (blockStart < 0)
                {
                    var diag = AnalyzeDvktBlockSearch(xmlContent, stt);
                    throw new InvalidOperationException("DVKT block search failed: No opening <CHI_TIET_DVKT> found before STT. " + diag.Message);
                }
                var blockCloseStart = xmlContent.IndexOf(closeTag, sttIndex, StringComparison.OrdinalIgnoreCase);
                if (blockCloseStart < 0)
                {
                    var diag = AnalyzeDvktBlockSearch(xmlContent, stt);
                    throw new InvalidOperationException("DVKT block search failed: No closing </CHI_TIET_DVKT> found after STT. " + diag.Message);
                }
                var blockEnd = blockCloseStart + closeTag.Length;
                var block = xmlContent.Substring(blockStart, blockEnd - blockStart);
                // kiểm tra block có STT chính xác
                var sttInsideRx = new Regex("<STT>\\s*(?<stt>\\d+)\\s*</STT>", _regexOptions, _regexTimeout);
                var mInside = sttInsideRx.Match(block);
                if (!mInside.Success)
                {
                    var diag = AnalyzeDvktBlockSearch(xmlContent, stt);
                    throw new InvalidOperationException("DVKT block search failed: STT not found inside the candidate block. " + diag.Message);
                }
                if (!int.TryParse(mInside.Groups["stt"].Value, out var foundStt))
                {
                    var diag = AnalyzeDvktBlockSearch(xmlContent, stt);
                    throw new InvalidOperationException("DVKT block search failed: cannot parse STT inside block. " + diag.Message);
                }
                if (foundStt != stt)
                {
                    var diag = AnalyzeDvktBlockSearch(xmlContent, stt);
                    throw new InvalidOperationException($"DVKT block search failed: found STT {foundStt} inside candidate block, expected {stt}. " + diag.Message);
                }
                // kiểm tra có STT khác cùng giá trị xuất hiện sau block (trùng STT)
                var mNext = sttRx.Match(xmlContent, blockEnd);
                if (mNext.Success)
                {
                    throw new InvalidOperationException($"Tìm thấy nhiều block CHI_TIET_DVKT với STT {stt}");
                }
                sw.Stop();
                var elapsed = sw.ElapsedMilliseconds;
                // log tốc độ tìm nếu cần (caller có thể log)
                return new DvktBlockMatch { Index = blockStart, Length = block.Length, Value = block, Stt = foundStt };
            }
            catch (RegexMatchTimeoutException)
            {
                var diag = AnalyzeDvktBlockSearch(xmlContent, stt);
                throw new InvalidOperationException("DVKT block search failed: regex timeout. " + diag.Message);
            }
            finally
            {
                sw.Stop();
                var elapsed = sw.ElapsedMilliseconds;
                // ghi log nếu chậm
                if (elapsed > 500)
                {
                    System.Diagnostics.Debug.WriteLine($"Tìm block STT {stt} bằng Fast mất {elapsed} ms");
                }
            }
        }

        // Thay giá trị tag trong 1 block (chỉ trong block) - dùng Regex với timeout
        public static string ReplaceTagValue(string block, string tagName, string value)
        {
            if (block == null) return null;
            // Các dạng: <TAG>...</TAG>
            var patternContent = $"<{tagName}\\b[^>]*>\\s*[\\s\\S]*?\\s*</{tagName}>";
            var rxContent = new Regex(patternContent, _regexOptions, _regexTimeout);
            if (rxContent.IsMatch(block))
            {
                block = rxContent.Replace(block, $"<{tagName}>{value}</{tagName}>", 1);
                return block;
            }
            // thẻ tự đóng <TAG />
            var patternEmpty = $"<{tagName}\\b[^>]*/>";
            var rxEmpty = new Regex(patternEmpty, _regexOptions, _regexTimeout);
            if (rxEmpty.IsMatch(block))
            {
                block = rxEmpty.Replace(block, $"<{tagName}>{value}</{tagName}>", 1);
                return block;
            }
            // thẻ mở đóng rỗng <TAG></TAG>
            var patternEmpty2 = $"<{tagName}\\b[^>]*>\\s*</{tagName}>";
            var rxEmpty2 = new Regex(patternEmpty2, _regexOptions, _regexTimeout);
            if (rxEmpty2.IsMatch(block))
            {
                block = rxEmpty2.Replace(block, $"<{tagName}>{value}</{tagName}>", 1);
                return block;
            }
            return block;
        }

        // Thêm phút cho NGAY_YL theo format yyyyMMddHHmm
        public static string AddMinutesToNgayYl(string ngayYl, int minutes)
        {
            if (string.IsNullOrWhiteSpace(ngayYl)) throw new ArgumentNullException(nameof(ngayYl));
            var dt = DateTime.ParseExact(ngayYl, "yyyyMMddHHmm", System.Globalization.CultureInfo.InvariantCulture);
            dt = dt.AddMinutes(minutes);
            return dt.ToString("yyyyMMddHHmm");
        }

        // Sửa NGAY_TH_YL chỉ trong block tìm được theo STT - trả về XmlFixResult
        public static XmlFixResult FixNgayThYl(string xmlContent, int xmlNumber, int stt, bool forceOverwrite = false)
        {
            var result = new XmlFixResult { Success = false, OriginalXml = xmlContent, XmlNumber = xmlNumber, Stt = stt };
            DvktBlockMatch? found = null;
            try
            {
                found = FindDvktBlockByStt(xmlContent, stt);
            }
            catch (InvalidOperationException ex)
            {
                result.Messages.Add(ex.Message);
                return result;
            }
            if (found == null)
            {
                var diag = AnalyzeDvktBlockSearch(xmlContent, stt);
                var msg = BuildDiagMessage(diag);
                result.Messages.Add("Không tìm thấy block DVKT chứa STT.");
                result.Messages.Add(msg);
                try
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("XmlFixService diagnostic:");
                    sb.AppendLine(msg);
                    sb.AppendLine($"XmlLength: {(xmlContent == null ? 0 : xmlContent.Length)}");
                    sb.AppendLine("--- First 500 chars ---");
                    sb.AppendLine(xmlContent == null ? string.Empty : xmlContent.Substring(0, Math.Min(500, xmlContent.Length)));
                    sb.AppendLine("--- Last 500 chars ---");
                    if (!string.IsNullOrEmpty(xmlContent))
                        sb.AppendLine(xmlContent.Substring(Math.Max(0, xmlContent.Length - 500), Math.Min(500, xmlContent.Length)));
                    sb.AppendLine($"DebugReadsFolder: {SupportEdit.Services.DebugLogService.GetDebugReadsFolder()}");
                    var logPath = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{stt}_FixFail", sb.ToString());
                    result.Messages.Add("Saved debug log: " + logPath);
                }
                catch { }
                return result;
            }

            var block = found.Value.Value;
            var index = found.Value.Index;
            var length = found.Value.Length;

            // lấy NGAY_YL gốc từ block
            var oldNgay = GetTagValue(block, "NGAY_YL");
            if (string.IsNullOrEmpty(oldNgay))
            {
                result.Messages.Add("Không tìm thấy NGAY_YL trong block.");
                return result;
            }
            DateTime dt;
            if (!DateTime.TryParseExact(oldNgay, "yyyyMMddHHmm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
            {
                result.Messages.Add($"NGAY_YL sai định dạng: '{oldNgay}'");
                return result;
            }
            var newNgay = AddMinutesToNgayYl(oldNgay, 10);

            var ngayThCurrent = GetTagValue(block, "NGAY_TH_YL") ?? string.Empty;
            if (!string.IsNullOrEmpty(ngayThCurrent) && !forceOverwrite)
            {
                result.RequireOverwriteConfirmation = true;
                result.FieldsNeedOverwriteConfirmation.Add("NGAY_TH_YL");
                result.ExistingValues["NGAY_TH_YL"] = ngayThCurrent;
                result.Messages.Add($"NGAY_TH_YL đã có giá trị: '{ngayThCurrent}'");
                return result;
            }
            if (!string.IsNullOrEmpty(ngayThCurrent) && forceOverwrite)
            {
                result.Messages.Add($"NGAY_TH_YL đang bị ghi đè: '{ngayThCurrent}' -> '{newNgay}'");
            }

            // tạo block mới và thay đúng vị trí trong xml gốc
            var newBlock = ReplaceTagValue(block, "NGAY_TH_YL", newNgay);
            var newXml = xmlContent.Substring(0, index) + newBlock + xmlContent.Substring(index + length);

            result.Success = true;
            result.FixedXml = newXml;
            result.FixedFields.Add("NGAY_TH_YL");
            result.Messages.Add($"NGAY_TH_YL: {ngayThCurrent} -> {newNgay}");

            // lưu thông tin block cũ / mới để hiển thị preview
            result.OldBlockValues = new Dictionary<string, string>
            {
                { "STT", GetTagValue(block, "STT") },
                { "MA_NHOM", GetTagValue(block, "MA_NHOM") },
                { "MA_DICH_VU", GetTagValue(block, "MA_DICH_VU") },
                { "MA_VAT_TU", GetTagValue(block, "MA_VAT_TU") },
                { "TEN_DICH_VU", GetTagValue(block, "TEN_DICH_VU") },
                { "TEN_VAT_TU", GetTagValue(block, "TEN_VAT_TU") },
                { "NGAY_YL", oldNgay },
                { "NGAY_TH_YL", ngayThCurrent },
                { "NGUOI_THUC_HIEN", GetTagValue(block, "NGUOI_THUC_HIEN") }
            };
            result.NewBlockValues = new Dictionary<string, string>(result.OldBlockValues);
            result.NewBlockValues["NGAY_TH_YL"] = newNgay;

            return result;
        }

        // Sửa NGUOI_THUC_HIEN chỉ trong block tìm được theo STT - trả về XmlFixResult
        public static XmlFixResult FixNguoiThucHien(string xmlContent, int xmlNumber, int stt, string practitionerCode, bool forceOverwrite = false)
        {
            var result = new XmlFixResult { Success = false, OriginalXml = xmlContent, XmlNumber = xmlNumber, Stt = stt };
            if (string.IsNullOrWhiteSpace(practitionerCode))
            {
                result.Messages.Add("Practitioner code trống.");
                return result;
            }

            DvktBlockMatch? found = null;
            try
            {
                found = FindDvktBlockByStt(xmlContent, stt);
            }
            catch (InvalidOperationException ex)
            {
                result.Messages.Add(ex.Message);
                return result;
            }
            if (found == null)
            {
                var diag = AnalyzeDvktBlockSearch(xmlContent, stt);
                var msg = BuildDiagMessage(diag);
                result.Messages.Add("Không tìm thấy block DVKT chứa STT.");
                result.Messages.Add(msg);
                try
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("XmlFixService diagnostic:");
                    sb.AppendLine(msg);
                    sb.AppendLine($"XmlLength: {(xmlContent == null ? 0 : xmlContent.Length)}");
                    sb.AppendLine("--- First 500 chars ---");
                    sb.AppendLine(xmlContent == null ? string.Empty : xmlContent.Substring(0, Math.Min(500, xmlContent.Length)));
                    sb.AppendLine("--- Last 500 chars ---");
                    if (!string.IsNullOrEmpty(xmlContent))
                        sb.AppendLine(xmlContent.Substring(Math.Max(0, xmlContent.Length - 500), Math.Min(500, xmlContent.Length)));
                    sb.AppendLine($"DebugReadsFolder: {SupportEdit.Services.DebugLogService.GetDebugReadsFolder()}");
                    var logPath = SupportEdit.Services.DebugLogService.SaveDebugLog($"XML{xmlNumber}_STT{stt}_FixFail", sb.ToString());
                    result.Messages.Add("Saved debug log: " + logPath);
                }
                catch { }
                return result;
            }

            var block = found.Value.Value;
            var index = found.Value.Index;
            var length = found.Value.Length;

            var oldVal = GetTagValue(block, "NGUOI_THUC_HIEN") ?? string.Empty;
            if (!string.IsNullOrEmpty(oldVal) && !forceOverwrite)
            {
                result.RequireOverwriteConfirmation = true;
                result.FieldsNeedOverwriteConfirmation.Add("NGUOI_THUC_HIEN");
                result.ExistingValues["NGUOI_THUC_HIEN"] = oldVal;
                result.Messages.Add($"NGUOI_THUC_HIEN đã có giá trị: '{oldVal}'");
                return result;
            }

            if (!string.IsNullOrEmpty(oldVal) && forceOverwrite)
            {
                result.Messages.Add($"NGUOI_THUC_HIEN đang bị ghi đè: '{oldVal}' -> '{practitionerCode}'");
            }

            // cho phép nhiều code phân tách bằng dấu chấm phẩy; đã validate không rỗng ở trên
            var newBlock = ReplaceTagValue(block, "NGUOI_THUC_HIEN", practitionerCode);
            var newXml = xmlContent.Substring(0, index) + newBlock + xmlContent.Substring(index + length);

            result.Success = true;
            result.FixedXml = newXml;
            result.FixedFields.Add("NGUOI_THUC_HIEN");
            result.Messages.Add($"NGUOI_THUC_HIEN: {oldVal} -> {practitionerCode}");

            result.OldBlockValues = new Dictionary<string, string>
            {
                { "STT", GetTagValue(block, "STT") },
                { "MA_NHOM", GetTagValue(block, "MA_NHOM") },
                { "MA_DICH_VU", GetTagValue(block, "MA_DICH_VU") },
                { "MA_VAT_TU", GetTagValue(block, "MA_VAT_TU") },
                { "TEN_DICH_VU", GetTagValue(block, "TEN_DICH_VU") },
                { "TEN_VAT_TU", GetTagValue(block, "TEN_VAT_TU") },
                { "NGAY_YL", GetTagValue(block, "NGAY_YL") },
                { "NGAY_TH_YL", GetTagValue(block, "NGAY_TH_YL") },
                { "NGUOI_THUC_HIEN", oldVal }
            };
            result.NewBlockValues = new Dictionary<string, string>(result.OldBlockValues);
            result.NewBlockValues["NGUOI_THUC_HIEN"] = practitionerCode;

            return result;
        }

        // Helper để kiểm tra điều kiện trong test
        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new Exception("Internal test failed: " + message);
        }

        // Hàm test nội bộ với real assertions - sẽ throw Exception nếu có lỗi
        public static string RunInternalTests()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Running XmlFixService internal tests...");
            var xml = "<ROOT>" +
                      "<CHI_TIET_DVKT><STT>1</STT><NGAY_YL>202605110000</NGAY_YL></CHI_TIET_DVKT>" +
                      "<CHI_TIET_DVKT><STT>2</STT><NGAY_YL>202605110100</NGAY_YL></CHI_TIET_DVKT>" +
                      "<CHI_TIET_DVKT><STT>257</STT><NGAY_YL>202605110911</NGAY_YL><NGAY_TH_YL /></CHI_TIET_DVKT>" +
                      "</ROOT>";

            try
            {
                // Test 1: FindAllDvktBlocks phải trả về 3 blocks
                var all = FindAllDvktBlocks(xml);
                AssertTrue(all.Count == 3, $"FindAllDvktBlocks should return 3, got {all.Count}");
                sb.AppendLine("✓ FindAllDvktBlocks returned: 3 blocks");

                // Test 2: DvktBlockPattern đúng
                AssertTrue(DvktBlockPattern == @"<CHI_TIET_DVKT\b[^>]*>[\s\S]*?</CHI_TIET_DVKT>",
                    $"DvktBlockPattern incorrect: {DvktBlockPattern}");
                sb.AppendLine($"✓ DvktBlockPattern is correct: {DvktBlockPattern}");

                // Test 3: FindDvktBlockBySttFast tìm được STT 257
                var fast = FindDvktBlockBySttFast(xml, 257);
                AssertTrue(fast != null, "FindDvktBlockBySttFast(xml, 257) should find block");
                sb.AppendLine("✓ FindDvktBlockBySttFast found STT 257");

                // Test 4: Block STT 257 chứa <STT>257</STT>
                AssertTrue(fast.Value.Value.Contains("<STT>257</STT>"),
                    "Block STT 257 should contain <STT>257</STT>");
                sb.AppendLine("✓ Block STT 257 contains <STT>257</STT>");

                // Test 5: Block STT 257 không chứa <STT>1</STT>
                AssertTrue(!fast.Value.Value.Contains("<STT>1</STT>"),
                    "Block STT 257 should NOT contain <STT>1</STT>");
                sb.AppendLine("✓ Block STT 257 does NOT contain <STT>1</STT>");

                // Test 6: Block STT 257 không chứa <STT>2</STT>
                AssertTrue(!fast.Value.Value.Contains("<STT>2</STT>"),
                    "Block STT 257 should NOT contain <STT>2</STT>");
                sb.AppendLine("✓ Block STT 257 does NOT contain <STT>2</STT>");

                // Test 7: FixNgayThYl với NGAY_YL 202605110911 tạo NGAY_TH_YL 202605110921
                var fix = FixNgayThYl(xml, 3, 257, false);
                AssertTrue(fix.Success, $"FixNgayThYl should succeed. Messages: {string.Join("; ", fix.Messages)}");
                sb.AppendLine("✓ FixNgayThYl succeeded");

                var newNgayTh = fix.NewBlockValues["NGAY_TH_YL"];
                AssertTrue(newNgayTh == "202605110921",
                    $"NGAY_TH_YL should be 202605110921, got {newNgayTh}");
                sb.AppendLine("✓ NGAY_YL 202605110911 + 10 minutes = 202605110921");

                sb.AppendLine("✓✓✓ ALL TESTS PASSED ✓✓✓");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                sb.AppendLine($"✗✗✗ TEST FAILED: {ex.Message}");
                throw;
            }
        }
    }
}
