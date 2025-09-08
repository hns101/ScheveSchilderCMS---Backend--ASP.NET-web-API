using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WebApplicationScheveCMS.Models
{
    public class PdfLayoutSettings
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        // Student Information Positioning
        public PdfElementPosition StudentName { get; set; } = new() { Top = 150, Left = 400, FontSize = 10 };
        public PdfElementPosition StudentAddress { get; set; } = new() { Top = 165, Left = 400, FontSize = 10 };
        
        // Invoice Information Positioning
        public PdfElementPosition InvoiceId { get; set; } = new() { Top = 195, Left = 400, FontSize = 10 };
        public PdfElementPosition InvoiceDate { get; set; } = new() { Top = 210, Left = 400, FontSize = 10 };
        public PdfElementPosition InvoiceDescription { get; set; } = new() { Top = 315, Left = 100, FontSize = 10 };
        
        // Amount Information Positioning
        public PdfElementPosition BaseAmount { get; set; } = new() { Top = 400, Left = 500, FontSize = 10 };
        public PdfElementPosition VatAmount { get; set; } = new() { Top = 415, Left = 500, FontSize = 10 };
        public PdfElementPosition TotalAmount { get; set; } = new() { Top = 430, Left = 500, FontSize = 10 };
        
        // Additional Information Positioning
        public PdfElementPosition PaymentNote { get; set; } = new() { Top = 500, Left = 100, FontSize = 10 };
        public PdfElementPosition ContactInfo { get; set; } = new() { Top = 600, Left = 100, FontSize = 10 };
        
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public string UpdatedBy { get; set; } = "System";
    }

    public class PdfElementPosition
    {
        public int Top { get; set; }
        public int Left { get; set; }
        public int FontSize { get; set; }
        public int MaxHeight { get; set; } = 15;
        public bool IsBold { get; set; } = false;
        public string TextAlign { get; set; } = "Left"; // Left, Center, Right
    }

    public class PdfLayoutUpdateRequest
    {
        public string ElementName { get; set; } = "";
        public int Top { get; set; }
        public int Left { get; set; }
        public int FontSize { get; set; }
        public int MaxHeight { get; set; } = 15;
        public bool IsBold { get; set; } = false;
        public string TextAlign { get; set; } = "Left";
    }

    public class PdfPreviewRequest
    {
        public PdfLayoutSettings? LayoutSettings { get; set; }
        public bool UseCurrentTemplate { get; set; } = true;
    }
}