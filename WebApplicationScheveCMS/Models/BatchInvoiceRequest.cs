using System.ComponentModel.DataAnnotations;

namespace WebApplicationScheveCMS.Models
{
    public class BatchInvoiceRequest
    {
        [Required(ErrorMessage = "Student IDs are required")]
        [MinLength(1, ErrorMessage = "At least one student ID is required")]
        public List<string> StudentIds { get; set; } = new List<string>();

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Amount total is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero")]
        public decimal AmountTotal { get; set; }

        [Required(ErrorMessage = "VAT is required")]
        [Range(0, 100, ErrorMessage = "VAT must be between 0 and 100 percent")]
        public decimal VAT { get; set; }
    }

    public class BatchGenerationResult
    {
        public List<Invoice> SuccessfulInvoices { get; set; } = new List<Invoice>();
        public List<string> Errors { get; set; } = new List<string>();
        public int SuccessCount => SuccessfulInvoices.Count;
        public int ErrorCount => Errors.Count;
        public decimal TotalAmount => SuccessfulInvoices.Sum(i => i.AmountTotal);
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
    }

    public class ValidationResult
    {
        public bool IsValid { get; }
        public string ErrorMessage { get; }
        public List<string> Errors { get; }

        public ValidationResult(bool isValid, string errorMessage = "", List<string>? errors = null)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
            Errors = errors ?? new List<string>();
        }

        public static ValidationResult Success() => new ValidationResult(true);
        public static ValidationResult Failure(string message) => new ValidationResult(false, message);
        public static ValidationResult Failure(List<string> errors) => new ValidationResult(false, "Validation failed", errors);
    }

    public class TemplateUploadResult
    {
        public string Message { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string FileName { get; set; } = "";
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.Now;
    }

    public class TemplateInfo
    {
        public bool HasTemplate { get; set; }
        public string? FileName { get; set; }
        public long FileSize { get; set; }
        public DateTime? LastModified { get; set; }
        public string? Extension { get; set; }
        public string? Message { get; set; }
    }
}