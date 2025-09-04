using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace WebApplicationScheveCMS.Models
{
    [BsonIgnoreExtraElements]
    public class Student
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("Name")]
        public string? Name { get; set; }
        [BsonElement("StudentNumber")]
        public string? StudentNumber { get; set; }
        [BsonElement("Address")]
        public string? Address { get; set; }
        [BsonElement("Email")]
        public string? Email { get; set; }
        [BsonElement("PhoneNumber")]
        public string? PhoneNumber { get; set; }
        [BsonElement("EmergencyContact")]
        public string? EmergencyContact { get; set; }
        [BsonElement("BankName")]
        public string? BankName { get; set; }
        [BsonElement("AccountNumber")]
        public string? AccountNumber { get; set; }
        [BsonElement("DateOfRegistration")]
        public DateTime DateOfRegistration { get; set; }
        
        [BsonElement("RegistrationDocumentPath")]
        public string? RegistrationDocumentPath { get; set; }
        
        // This property will be populated by the aggregation pipeline
        [BsonElement("Invoices")]
        public List<Invoice>? Invoices { get; set; }
    }
}
