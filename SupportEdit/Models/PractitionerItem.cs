namespace SupportEdit.Models
{
    using System.Collections.Generic;

    // Một người thực hiện trong danh sách
    public class PractitionerItem
    {
        public const string CodeSourceReplacement = "ReplacementCode";
        public const string CodeSourceMaBacSi = "MaBacSi";

        public string CategoryName { get; set; }
        public string Code { get; set; }
        public string CodeSource { get; set; } = CodeSourceReplacement;
        public string Description { get; set; }
        public List<string> ServiceNames { get; set; } = new List<string>();
        public bool CheckAttendance { get; set; } = true;

        public bool UsesMaBacSi()
        {
            return string.Equals(CodeSource, CodeSourceMaBacSi, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
