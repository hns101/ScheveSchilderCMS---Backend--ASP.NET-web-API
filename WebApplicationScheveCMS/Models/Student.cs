using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic; // Required for List<Invoice>

namespace WebApplicationScheveCMS.Models
{
    public class Student
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        [BsonIgnoreIfDefault] 
        public string? Id { get; set; }

        public string Name { get; set; } = null!;
        public string StudentNumber { get; set; } = null!;
        public string Address { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public string EmergencyContact { get; set; } = null!;
        public string BankName { get; set; } = null!;
        public string AccountNumber { get; set; } = null!;
        public DateTime DateOfRegistration { get; set; }
        
        public string? RegistrationDocumentPath { get; set; }

        // Explicitly map the 'Invoices' field from the aggregation pipeline
        [BsonElement("Invoices")] 
        public List<Invoice>? Invoices { get; set; }
    }
}
