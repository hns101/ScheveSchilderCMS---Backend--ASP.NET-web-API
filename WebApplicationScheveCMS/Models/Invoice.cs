using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WebApplicationScheveCMS.Models
{
    [BsonIgnoreExtraElements]
    public class Invoice
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("StudentId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string StudentId { get; set; } = null!;

        [BsonElement("Date")]
        public DateTime Date { get; set; }
        [BsonElement("AmountTotal")]
        public decimal AmountTotal { get; set; }
        [BsonElement("VAT")]
        public decimal VAT { get; set; }
        [BsonElement("Description")]
        public string? Description { get; set; }
        
        [BsonElement("InvoicePdfPath")]
        public string? InvoicePdfPath { get; set; }
    }
}
