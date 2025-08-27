using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WebApplicationScheveCMS.Models
{
    public class Student
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string? Name { get; set; }
        public string? StudentNumber { get; set; }
        public string? Address { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? EmergencyContact { get; set; }
        public string? BankName { get; set; }
        public string? AccountNumber { get; set; }
        public DateTime DateOfRegistration { get; set; }
        
        // This will store the path to the registration PDF
        public string? RegistrationDocumentPath { get; set; }
    }
}
