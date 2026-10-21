using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Linq;
using SupportEdit.Models;

namespace SupportEdit.Services
{
    public sealed class Xml1ReadProgress
    {
        public int Processed { get; init; }
        public string FileName { get; init; } = string.Empty;
    }

    public static class Xml1Reader
    {
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);
        private static readonly string[] SupportedDateFormats =
        {
            "yyyyMMddHHmm",
            "yyyyMMddHHmmss"
        };

        public static HoSoXml1 ReadFile(string filePath)
        {
            var result = new HoSoXml1 { FileName = Path.GetFileName(filePath) };

            try
            {
                var outerDocument = XDocument.Load(filePath, LoadOptions.None);
                var xml1File = outerDocument
                    .Descendants()
                    .Where(element => HasLocalName(element, "FILEHOSO"))
                    .FirstOrDefault(element => string.Equals(
                        ReadDirectChildValue(element, "LOAIHOSO"),
                        "XML1",
                        StringComparison.OrdinalIgnoreCase));

                if (xml1File == null)
                {
                    result.Loi = "Không tìm thấy XML1";
                    return result;
                }

                var encodedContent = xml1File.Elements()
                    .FirstOrDefault(element => HasLocalName(element, "NOIDUNGFILE"))
                    ?.Value.Trim();
                if (string.IsNullOrWhiteSpace(encodedContent))
                {
                    result.Loi = "XML1 không có NOIDUNGFILE";
                    return result;
                }

                byte[] decodedBytes;
                try
                {
                    decodedBytes = Convert.FromBase64String(encodedContent);
                }
                catch (FormatException)
                {
                    result.Loi = "NOIDUNGFILE của XML1 không phải Base64 hợp lệ";
                    return result;
                }

                string decodedXml;
                try
                {
                    decodedXml = StrictUtf8.GetString(decodedBytes);
                }
                catch (DecoderFallbackException)
                {
                    result.Loi = "NOIDUNGFILE của XML1 không phải UTF-8 hợp lệ";
                    return result;
                }

                XDocument xml1Document;
                try
                {
                    xml1Document = XDocument.Parse(decodedXml, LoadOptions.None);
                }
                catch (Exception ex) when (ex is System.Xml.XmlException or ArgumentException)
                {
                    result.Loi = "XML1 sau giải mã không hợp lệ";
                    return result;
                }

                result.MA_LK = ReadDescendantValue(xml1Document, "MA_LK");
                result.MA_BN = ReadDescendantValue(xml1Document, "MA_BN");
                var rawDischargeDate = ReadDescendantValue(xml1Document, "NGAY_RA");
                var errors = new List<string>();

                if (string.IsNullOrWhiteSpace(result.MA_LK)) errors.Add("XML1 không có MA_LK");
                if (string.IsNullOrWhiteSpace(result.MA_BN)) errors.Add("XML1 không có MA_BN");
                if (string.IsNullOrWhiteSpace(rawDischargeDate))
                {
                    errors.Add("XML1 không có NGAY_RA");
                }
                else if (TryParseDischargeDate(rawDischargeDate, out var dischargeDate))
                {
                    result.NGAY_RA = dischargeDate;
                }
                else
                {
                    errors.Add($"NGAY_RA không đúng định dạng: {rawDischargeDate}");
                }

                result.Loi = string.Join("; ", errors);
                return result;
            }
            catch (Exception ex)
            {
                result.Loi = "Không đọc được file XML: " + ex.Message;
                return result;
            }
        }

        public static List<HoSoXml1> ReadDirectory(
            string folderPath,
            IProgress<Xml1ReadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                throw new ArgumentException("Chưa chọn thư mục XML.", nameof(folderPath));
            }
            if (!Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException("Không tìm thấy thư mục XML đã chọn.");
            }

            var results = new List<HoSoXml1>();
            var processed = 0;
            foreach (var filePath in Directory.EnumerateFiles(folderPath, "*.xml", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = ReadFile(filePath);
                results.Add(item);
                processed++;
                progress?.Report(new Xml1ReadProgress
                {
                    Processed = processed,
                    FileName = item.FileName
                });
            }

            return results;
        }

        private static bool HasLocalName(XElement element, string name)
        {
            return string.Equals(element.Name.LocalName, name, StringComparison.OrdinalIgnoreCase);
        }

        private static string ReadDirectChildValue(XElement parent, string name)
        {
            return parent.Elements()
                .FirstOrDefault(element => HasLocalName(element, name))
                ?.Value.Trim() ?? string.Empty;
        }

        private static string ReadDescendantValue(XDocument document, string name)
        {
            return document.Descendants()
                .FirstOrDefault(element => HasLocalName(element, name))
                ?.Value.Trim() ?? string.Empty;
        }

        private static bool TryParseDischargeDate(string value, out DateTime dischargeDate)
        {
            return DateTime.TryParseExact(
                value.Trim(),
                SupportedDateFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out dischargeDate);
        }
    }
}
