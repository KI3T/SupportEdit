using System;
using System.IO;

namespace SupportEdit.Core.Services
{
    public class BackupService
    {
        private readonly string _backupFolder;

        public BackupService(string baseFolder = null)
        {
            baseFolder ??= AppDomain.CurrentDomain.BaseDirectory;
            _backupFolder = Path.Combine(baseFolder, "Backups");
            Directory.CreateDirectory(_backupFolder);
        }

        public string SaveBackup(string xmlContent, int xml, int stt)
        {
            var name = $"XML{xml}_{stt}_{DateTime.Now:yyyyMMdd_HHmmss}.xml";
            var path = Path.Combine(_backupFolder, name);
            File.WriteAllText(path, xmlContent);
            return path;
        }
    }
}
