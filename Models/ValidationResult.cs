namespace WarpBootstrap.Models
{
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string SafeFileName { get; set; } = string.Empty;
    }
}
