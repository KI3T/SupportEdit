using System;

namespace SupportEdit.Core.Models
{
    // Mô tả 1 dòng lỗi
    public class XmlErrorItem
    {
        public int Xml { get; set; }
        public int Stt { get; set; }
        public string Field { get; set; }
        public string Message { get; set; }
        public string RawLine { get; set; }
    }
}
