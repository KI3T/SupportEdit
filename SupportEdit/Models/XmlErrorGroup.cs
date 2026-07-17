using System.Collections.Generic;

namespace SupportEdit.Models
{
    // Nhóm lỗi theo XML + STT
    public class XmlErrorGroup
    {
        public int Xml { get; set; }
        public int Stt { get; set; }
        public List<XmlErrorItem> Items { get; set; } = new List<XmlErrorItem>();
    }
}
