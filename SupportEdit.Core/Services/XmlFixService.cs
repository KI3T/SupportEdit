using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using SupportEdit.Core.Models;

namespace SupportEdit.Core.Services
{
    // Dịch vụ thực hiện sửa nội dung XML theo quy tắc - trả về XmlFixResult chi tiết
    public class XmlFixService
    {

        // Tìm tất cả block DVKT chứa STT (Singleline để . bao gồm xuống dòng)
        public static List<string> FindDvktBlocks(string xmlContent, int stt)
        {
            if (string.IsNullOrEmpty(xmlContent)) return new List<string>();
            var pattern = $@"<CHI_TIET_DVKT>[\s\S]*?<STT>{stt}</STT>[\s\S]*?</CHI_TIET_DVKT>";
            var matches = Regex.Matches(xmlContent, pattern, RegexOptions.Singleline);
            return matches.Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value).ToList();
        }

        // Thay giá trị tag trong 1 block (chỉ trong block)
        public static string ReplaceTagValue(string block, string tagName, string value)
        {
            if (block == null) return null;
            // Các dạng thẻ rỗng hoặc có nội dung - chỉ replace nội dung bên trong thẻ
            var patternContent = $"<%TAG%\\s*>\\s*.*?\\s*</%TAG%>".Replace("%TAG%", tagName);
            if (Regex.IsMatch(block, patternContent, RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                block = Regex.Replace(block, patternContent, $"<{tagName}>{value}</{tagName}>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                return block;
            }

            // thẻ tự đóng
            var patternEmpty = $"<%TAG%\\s*/>".Replace("%TAG%", tagName);
            if (Regex.IsMatch(block, patternEmpty, RegexOptions.IgnoreCase))
            {
                block = Regex.Replace(block, patternEmpty, $"<{tagName}>{value}</{tagName}>", RegexOptions.IgnoreCase);
                return block;
            }

            // thẻ mở đóng rỗng
            var patternEmpty2 = $"<%TAG%\\s*></%TAG%>".Replace("%TAG%", tagName);
            if (Regex.IsMatch(block, patternEmpty2, RegexOptions.IgnoreCase))
            {
                block = Regex.Replace(block, patternEmpty2, $"<{tagName}>{value}</{tagName}>", RegexOptions.IgnoreCase);
                return block;
            }

            // nếu không phù hợp, trả về block gốc
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

        // Sửa NGAY_TH_YL trong toàn bộ xml cho block có stt - trả về XmlFixResult
        public static XmlFixResult FixNgayThYl(string xmlContent, int xmlNumber, int stt, bool forceOverwrite = false)
        {
            var result = new XmlFixResult { Success = false, OriginalXml = xmlContent, XmlNumber = xmlNumber, Stt = stt };
            var blocks = FindDvktBlocks(xmlContent, stt);
            if (blocks.Count == 0)
            {
                result.Messages.Add("Không tìm thấy block DVKT chứa STT.");
                return result;
            }
            if (blocks.Count > 1)
            {
                result.Messages.Add("Tìm thấy nhiều block cùng STT, không an toàn để sửa.");
                return result;
            }

            var block = blocks[0];
            // tìm NGAY_YL gốc
            var ngayPattern = "<NGAY_YL>(.*?)</NGAY_YL>";
            var mNgay = Regex.Match(block, ngayPattern, RegexOptions.Singleline);
            if (!mNgay.Success)
            {
                result.Messages.Add("Không tìm thấy NGAY_YL trong block.");
                return result;
            }
            var oldNgay = mNgay.Groups[1].Value.Trim();
            // kiểm tra định dạng
            DateTime dt;
            if (!DateTime.TryParseExact(oldNgay, "yyyyMMddHHmm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
            {
                result.Messages.Add($"NGAY_YL sai định dạng: '{oldNgay}'");
                return result;
            }
            var newNgay = AddMinutesToNgayYl(oldNgay, 10);

            // kiểm tra NGAY_TH_YL hiện tại có giá trị không
            var mNgayTh = Regex.Match(block, "<NGAY_TH_YL>(.*?)</NGAY_TH_YL>", RegexOptions.Singleline);
            var ngayThCurrent = mNgayTh.Success ? mNgayTh.Groups[1].Value.Trim() : string.Empty;
            if (!string.IsNullOrEmpty(ngayThCurrent) && !forceOverwrite)
            {
                // báo yêu cầu xác nhận ghi đè thay vì lỗi nghiêm trọng
                result.RequireOverwriteConfirmation = true;
                result.FieldsNeedOverwriteConfirmation.Add("NGAY_TH_YL");
                result.ExistingValues["NGAY_TH_YL"] = ngayThCurrent;
                result.Messages.Add($"NGAY_TH_YL đã có giá trị: '{ngayThCurrent}'");
                // không sửa, chỉ báo cần xác nhận
                return result;
            }
            if (!string.IsNullOrEmpty(ngayThCurrent) && forceOverwrite)
            {
                // nếu có ghi đè, thêm message ghi đè vào messages
                result.Messages.Add($"NGAY_TH_YL đang bị ghi đè: '{ngayThCurrent}' -> '{newNgay}'");
            }

            var newBlock = ReplaceTagValue(block, "NGAY_TH_YL", newNgay);
            var newXml = xmlContent.Replace(block, newBlock);

            result.Success = true;
            result.FixedXml = newXml;
            result.FixedFields.Add("NGAY_TH_YL");
            result.Messages.Add($"NGAY_TH_YL: {ngayThCurrent} -> {newNgay}");
            return result;
        }

        // Sửa NGUOI_THUC_HIEN trong block có stt - trả về XmlFixResult
        public static XmlFixResult FixNguoiThucHien(string xmlContent, int xmlNumber, int stt, string practitionerCode, bool forceOverwrite = false)
        {
            var result = new XmlFixResult { Success = false, OriginalXml = xmlContent, XmlNumber = xmlNumber, Stt = stt };
            if (string.IsNullOrWhiteSpace(practitionerCode))
            {
                result.Messages.Add("Practitioner code trống.");
                return result;
            }

            var blocks = FindDvktBlocks(xmlContent, stt);
            if (blocks.Count == 0)
            {
                result.Messages.Add("Không tìm thấy block DVKT chứa STT.");
                return result;
            }
            if (blocks.Count > 1)
            {
                result.Messages.Add("Tìm thấy nhiều block cùng STT, không an toàn để sửa.");
                return result;
            }

            var block = blocks[0];
            var m = Regex.Match(block, "<NGUOI_THUC_HIEN>(.*?)</NGUOI_THUC_HIEN>", RegexOptions.Singleline);
            var oldVal = m.Success ? m.Groups[1].Value.Trim() : string.Empty;
            if (!string.IsNullOrEmpty(oldVal) && !forceOverwrite)
            {
                // yêu cầu xác nhận ghi đè thay vì lỗi nghiêm trọng
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

            var newBlock = ReplaceTagValue(block, "NGUOI_THUC_HIEN", practitionerCode);
            var newXml = xmlContent.Replace(block, newBlock);

            result.Success = true;
            result.FixedXml = newXml;
            result.FixedFields.Add("NGUOI_THUC_HIEN");
            result.Messages.Add($"NGUOI_THUC_HIEN: {oldVal} -> {practitionerCode}");
            return result;
        }
    }
}
