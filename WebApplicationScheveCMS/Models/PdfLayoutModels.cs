using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WebApplicationScheveCMS.Models
{
    [BsonIgnoreExtraElements]
    public class PdfLayoutSettings
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("StudentName")]
        public PdfElementPosition StudentName { get; set; } = new();

        [BsonElement("StudentAddress")]
        public PdfElementPosition StudentAddress { get; set; } = new();

        [BsonElement("InvoiceId")]
        public PdfElementPosition InvoiceId { get; set; } = new();

        [BsonElement("InvoiceDate")]
        public PdfElementPosition InvoiceDate { get; set; } = new();

        [BsonElement("InvoiceDescription")]
        public PdfElementPosition InvoiceDescription { get; set; } = new();

        [BsonElement("BaseAmount")]
        public PdfElementPosition BaseAmount { get; set; } = new();

        [BsonElement("VatAmount")]
        public PdfElementPosition VatAmount { get; set; } = new();

        [BsonElement("TotalAmount")]
        public PdfElementPosition TotalAmount { get; set; } = new();

        [BsonElement("PaymentNote")]
        public PdfElementPosition PaymentNote { get; set; } = new();

        [BsonElement("ContactInfo")]
        public PdfElementPosition ContactInfo { get; set; } = new();

        [BsonElement("LastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        [BsonElement("UpdatedBy")]
        public string UpdatedBy { get; set; } = "System";
    }

    [BsonIgnoreExtraElements]
    public class PdfElementPosition
    {
        [BsonElement("Top")]
        public int Top { get; set; } = 0;

        [BsonElement("Left")]
        public int Left { get; set; } = 0;

        [BsonElement("FontSize")]
        public int FontSize { get; set; } = 10;

        [BsonElement("TextAlign")]
        public string TextAlign { get; set; } = "left";

        [BsonElement("IsBold")]
        public bool IsBold { get; set; } = false;

        [BsonElement("MaxHeight")]
        public int MaxHeight { get; set; } = 15;
    }

    public class PdfPreviewRequest
    {
        public PdfLayoutSettings? LayoutSettings { get; set; }
    }
}