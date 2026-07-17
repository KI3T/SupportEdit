using System;
using System.IO;
using System.Text;

namespace SupportEdit.Services
{
    public static class DebugLogService
    {
        public static string GetDebugReadsFolder()
        {
            var folder = Path.Combine(AppContext.BaseDirectory, "DebugReads");
            try
            {
                Directory.CreateDirectory(folder);
            }
            catch { }
            return folder;
        }

        public static string SaveDebugLog(string filePrefix, string content)
        {
            var folder = GetDebugReadsFolder();
            var fileName = $"{filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.log";
            var path = Path.Combine(folder, fileName);
            try { File.WriteAllText(path, content ?? string.Empty, Encoding.UTF8); } catch { }
            return path;
        }

        public static string SaveDebugText(string filePrefix, string content)
        {
            var folder = GetDebugReadsFolder();
            var fileName = $"{filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            var path = Path.Combine(folder, fileName);
            try { File.WriteAllText(path, content ?? string.Empty, Encoding.UTF8); } catch { }
            return path;
        }
    }
}
