namespace SupportEdit.Core.Models
{
    // Một người thực hiện trong danh sách
    public class PractitionerItem
    {
        public string CategoryName { get; set; }
        public string Code { get; set; }
        public string Description { get; set; }
        public bool CheckAttendance { get; set; } = true;
    }
}
