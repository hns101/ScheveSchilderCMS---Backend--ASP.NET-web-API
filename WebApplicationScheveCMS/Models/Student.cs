using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic; // Required for List<Invoice>

namespace WebApplicationScheveCMS.Models
{
    public class Student
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        // BsonIgnoreIfDefault ensures Id is not sent if null, letting MongoDB generate it
        [BsonIgnoreIfDefault] 
        public string? Id { get; set; }

        public string Name { get; set; } = null!; // Non-nullable, enforce data
        public string StudentNumber { get; set; } = null!; // Non-nullable, enforce data
        public string Address { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public string EmergencyContact { get; set; } = null!;
        public string BankName { get; set; } = null!;
        public string AccountNumber { get; set; } = null!;
        public DateTime DateOfRegistration { get; set; }
        
        public string? RegistrationDocumentPath { get; set; }

        // Property to hold the student's invoices after the lookup
        // This will not be stored in the Students collection itself, but populated by queries
        [BsonIgnore] // Ignore this property when serializing/deserializing to/from MongoDB
        public List<Invoice>? Invoices { get; set; }
    }
}
