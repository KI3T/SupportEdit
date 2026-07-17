using System.Collections.Generic;

namespace SupportEdit.Core.Models
{
    // Kết quả của 1 lần sửa XML
    public class XmlFixResult
    {
        public bool Success { get; set; }
        public string OriginalXml { get; set; }
        public string FixedXml { get; set; }
        public int XmlNumber { get; set; }
        public int Stt { get; set; }
        public List<string> FixedFields { get; set; } = new List<string>();

        // Nếu cần hỏi xác nhận ghi đè thì true
        public bool RequireOverwriteConfirmation { get; set; } = false;
        public List<string> FieldsNeedOverwriteConfirmation { get; set; } = new List<string>();
        public Dictionary<string, string> ExistingValues { get; set; } = new Dictionary<string, string>();

        public List<string> Messages { get; set; } = new List<string>();
    }
}
