namespace SupportEdit.Services
{
    public class XmlValidationResult
    {
        public bool IsValid { get; set; }
        public int Length { get; set; }
        public bool HasDvktOpenTag { get; set; }
        public bool HasDvktCloseTag { get; set; }
        public bool HasTargetStt { get; set; }
        public string Message { get; set; }
    }
}