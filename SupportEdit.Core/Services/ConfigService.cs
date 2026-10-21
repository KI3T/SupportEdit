using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using SupportEdit.Core.Models;

namespace SupportEdit.Core.Services
{
    // Quản lý cấu hình đơn giản như tiêu đề cửa sổ và danh sách practitioners
    public class ConfigService
    {
        private const string PractitionersFile = "practitioners.json";
        private const string ConfigFile = "appconfig.json";

        // Những cấu hình mở rộng theo yêu cầu
        public string XmlWindowTitle { get; set; } = "tool_4750_TBT"; // Mặc định cũ
        public string XmlAppWindowTitleKeyword { get; set; } = "tool_4750_TBT";
        public int FindDelayMs { get; set; } = 300;
        public int ClipboardRetryCount { get; set; } = 5;
        public int ClipboardRetryDelayMs { get; set; } = 200;
        public string XmlAppProcessName { get; set; } = string.Empty;

        public List<PractitionerItem> Practitioners { get; private set; } = new List<PractitionerItem>();

        public void Load()
        {
            if (File.Exists(PractitionersFile))
            {
                try
                {
                    var txt = File.ReadAllText(PractitionersFile);
                    Practitioners = JsonSerializer.Deserialize<List<PractitionerItem>>(txt) ?? new List<PractitionerItem>();
                }
                catch
                {
                    Practitioners = new List<PractitionerItem>();
                }
            }
            else
            {
                Practitioners = new List<PractitionerItem>();
            }

            // Nếu chưa có config app thì tạo mẫu
            if (!File.Exists(ConfigFile))
            {
                var sample = new Dictionary<string, object>
                {
                    { "XmlAppWindowTitleKeyword", "Siêu phần mềm" },
                    { "XmlAppProcessName", "" },
                    { "FindDelayMs", 300 },
                    { "ClipboardRetryCount", 5 },
                    { "ClipboardRetryDelayMs", 200 }
                };
                try { File.WriteAllText(ConfigFile, JsonSerializer.Serialize(sample, new JsonSerializerOptions{ WriteIndented = true })); }
                catch { }
            }

            if (File.Exists(ConfigFile))
            {
                try
                {
                    var txt = File.ReadAllText(ConfigFile);
                    var doc = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(txt);
                    if (doc != null)
                    {
                        if (doc.ContainsKey("XmlAppWindowTitleKeyword")) XmlAppWindowTitleKeyword = doc["XmlAppWindowTitleKeyword"].GetString();
                        if (doc.ContainsKey("XmlAppProcessName")) XmlAppProcessName = doc["XmlAppProcessName"].GetString();
                        if (doc.ContainsKey("FindDelayMs")) FindDelayMs = doc["FindDelayMs"].GetInt32();
                        if (doc.ContainsKey("ClipboardRetryCount")) ClipboardRetryCount = doc["ClipboardRetryCount"].GetInt32();
                        if (doc.ContainsKey("ClipboardRetryDelayMs")) ClipboardRetryDelayMs = doc["ClipboardRetryDelayMs"].GetInt32();
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
                File.WriteAllText(PractitionersFile, txt);
            }
            catch { }

            try
            {
                var doc = new Dictionary<string, object>
                {
                    { "XmlAppWindowTitleKeyword", XmlAppWindowTitleKeyword },
                    { "XmlAppProcessName", XmlAppProcessName },
                    { "FindDelayMs", FindDelayMs },
                    { "ClipboardRetryCount", ClipboardRetryCount },
                    { "ClipboardRetryDelayMs", ClipboardRetryDelayMs }
                };
                File.WriteAllText(ConfigFile, JsonSerializer.Serialize(doc, new JsonSerializerOptions{ WriteIndented = true }));
            }
            catch { }
        }
    }
}
