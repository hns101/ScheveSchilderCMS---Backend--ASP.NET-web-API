using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace WebApplicationScheveCMS.Models
{
    [BsonIgnoreExtraElements]
    public class PdfLayoutSettings
    {
        [BsonId]
        [BsonElement("_id")]
        [BsonRepresentation(BsonType.String)] // Explicitly set to String representation
        public string? Id { get; set; }

        // Student Information Positioning
        [BsonElement("StudentName")]
        public PdfElementPosition StudentName { get; set; } = new() { Top = 150, Left = 400, FontSize = 10 };
        
        [BsonElement("StudentAddress")]
        public PdfElementPosition StudentAddress { get; set; } = new() { Top = 165, Left = 400, FontSize = 10 };
        
        // Invoice Information Positioning
        [BsonElement("InvoiceId")]
        public PdfElementPosition InvoiceId { get; set; } = new() { Top = 195, Left = 400, FontSize = 10 };
        
        [BsonElement("InvoiceDate")]
        public PdfElementPosition InvoiceDate { get; set; } = new() { Top = 210, Left = 400, FontSize = 10 };
        
        [BsonElement("InvoiceDescription")]
        public PdfElementPosition InvoiceDescription { get; set; } = new() { Top = 315, Left = 100, FontSize = 10 };
        
        // Amount Information Positioning
        [BsonElement("BaseAmount")]
        public PdfElementPosition BaseAmount { get; set; } = new() { Top = 400, Left = 500, FontSize = 10 };
        
        [BsonElement("VatAmount")]
        public PdfElementPosition VatAmount { get; set; } = new() { Top = 415, Left = 500, FontSize = 10 };
        
        [BsonElement("TotalAmount")]
        public PdfElementPosition TotalAmount { get; set; } = new() { Top = 430, Left = 500, FontSize = 10 };
        
        // Additional Information Positioning
        [BsonElement("PaymentNote")]
        public PdfElementPosition PaymentNote { get; set; } = new() { Top = 500, Left = 100, FontSize = 10 };
        
        [BsonElement("ContactInfo")]
        public PdfElementPosition ContactInfo { get; set; } = new() { Top = 600, Left = 100, FontSize = 10 };
        
        [BsonElement("LastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        
        [BsonElement("UpdatedBy")]
        public string UpdatedBy { get; set; } = "System";
    }

    [BsonIgnoreExtraElements]
    public class PdfElementPosition
    {
        [BsonElement("Top")]
        [Range(0, 1000, ErrorMessage = "Top position must be between 0 and 1000")]
        public int Top { get; set; }
        
        [BsonElement("Left")]
        [Range(0, 1000, ErrorMessage = "Left position must be between 0 and 1000")]
        public int Left { get; set; }
        
        [BsonElement("FontSize")]
        [Range(6, 30, ErrorMessage = "Font size must be between 6 and 30")]
        public int FontSize { get; set; }
        
        [BsonElement("MaxHeight")]
        [Range(10, 100, ErrorMessage = "Max height must be between 10 and 100")]
        public int MaxHeight { get; set; } = 15;
        
        [BsonElement("IsBold")]
        public bool IsBold { get; set; } = false;
        
        [BsonElement("TextAlign")]
        [RegularExpression("^(Left|Center|Right)$", ErrorMessage = "Text align must be Left, Center, or Right")]
        public string TextAlign { get; set; } = "Left";
    }

    public class PdfLayoutUpdateRequest
    {
        [Required(ErrorMessage = "Element name is required")]
        public string ElementName { get; set; } = "";
        
        [Range(0, 1000, ErrorMessage = "Top position must be between 0 and 1000")]
        public int Top { get; set; }
        
        [Range(0, 1000, ErrorMessage = "Left position must be between 0 and 1000")]
        public int Left { get; set; }
        
        [Range(6, 30, ErrorMessage = "Font size must be between 6 and 30")]
        public int FontSize { get; set; }
        
        [Range(10, 100, ErrorMessage = "Max height must be between 10 and 100")]
        public int MaxHeight { get; set; } = 15;
        
        public bool IsBold { get; set; } = false;
        
        [RegularExpression("^(Left|Center|Right)$", ErrorMessage = "Text align must be Left, Center, or Right")]
        public string TextAlign { get; set; } = "Left";
    }

    public class PdfPreviewRequest
    {
        public PdfLayoutSettings? LayoutSettings { get; set; }
        public bool UseCurrentTemplate { get; set; } = true;
    }
}