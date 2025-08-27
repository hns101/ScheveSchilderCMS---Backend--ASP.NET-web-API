using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WebApplicationScheveCMS.Models
{
    public class Invoice
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string StudentId { get; set; } = null!;

        public DateTime Date { get; set; }
        public decimal AmountTotal { get; set; }
        public decimal VAT { get; set; }
        public string? Description { get; set; }
        
        // This will store the path to the generated invoice PDF
        public string? InvoicePdfPath { get; set; }
    }
}