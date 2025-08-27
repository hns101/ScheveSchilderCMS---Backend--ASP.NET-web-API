using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using WebApplicationScheveCMS.Models;

namespace WebApplicationScheveCMS.Services
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
        public string? RegistrationDocumentPath { get; set; }

        // Property to hold the student's invoices after the lookup
        public List<Invoice>? Invoices { get; set; }
    }

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
        public string? InvoicePdfPath { get; set; }
    }

    public class StudentDatabaseSettings
    {
        public string ConnectionString { get; set; } = null!;
        public string DatabaseName { get; set; } = null!;
        public string StudentsCollectionName { get; set; } = null!;
        public string InvoicesCollectionName { get; set; } = null!;
    }
    
    public class StudentService
    {
        private readonly IMongoCollection<Student> _studentsCollection;
        private readonly IMongoCollection<Invoice> _invoicesCollection;

        public StudentService(
            IOptions<StudentDatabaseSettings> studentDatabaseSettings)
        {
            var mongoClient = new MongoClient(
                studentDatabaseSettings.Value.ConnectionString);

            var mongoDatabase = mongoClient.GetDatabase(
                studentDatabaseSettings.Value.DatabaseName);

            _studentsCollection = mongoDatabase.GetCollection<Student>(
                studentDatabaseSettings.Value.StudentsCollectionName);
            _invoicesCollection = mongoDatabase.GetCollection<Invoice>(
                studentDatabaseSettings.Value.InvoicesCollectionName);
        }

        public async Task<List<Student>> GetAsync() =>
            await _studentsCollection.Find(_ => true).ToListAsync();

        // New method to get a student along with their invoices
        public async Task<Student?> GetStudentWithInvoicesAsync(string id)
        {
            var pipeline = new BsonDocument[]
            {
                // Match the student by their ObjectId
                new BsonDocument("$match", new BsonDocument("_id", new ObjectId(id))),
                
                // Perform a left outer join to the Invoices collection
                new BsonDocument("$lookup",
                    new BsonDocument
                    {
                        { "from", _invoicesCollection.CollectionNamespace.CollectionName },
                        { "localField", "_id" },
                        { "foreignField", "StudentId" },
                        { "as", "Invoices" } // The new field name for the joined invoices
                    }),
                    
                // Sort the invoices by date in descending order (most recent first)
                new BsonDocument("$unwind", "$Invoices"),
                new BsonDocument("$sort", new BsonDocument("Invoices.Date", -1)),
                new BsonDocument("$group", new BsonDocument
                    {
                        { "_id", "$_id" },
                        { "Name", new BsonDocument("$first", "$Name") },
                        { "StudentNumber", new BsonDocument("$first", "$StudentNumber") },
                        { "Address", new BsonDocument("$first", "$Address") },
                        { "Email", new BsonDocument("$first", "$Email") },
                        { "PhoneNumber", new BsonDocument("$first", "$PhoneNumber") },
                        { "EmergencyContact", new BsonDocument("$first", "$EmergencyContact") },
                        { "BankName", new BsonDocument("$first", "$BankName") },
                        { "AccountNumber", new BsonDocument("$first", "$AccountNumber") },
                        { "DateOfRegistration", new BsonDocument("$first", "$DateOfRegistration") },
                        { "RegistrationDocumentPath", new BsonDocument("$first", "$RegistrationDocumentPath") },
                        { "Invoices", new BsonDocument("$push", "$Invoices") }
                    })
            };

            var studentWithInvoices = await _studentsCollection
                .Aggregate<Student>(pipeline)
                .FirstOrDefaultAsync();

            return studentWithInvoices;
        }

        public async Task CreateAsync(Student newStudent) =>
            await _studentsCollection.InsertOneAsync(newStudent);

        public async Task UpdateAsync(string id, Student updatedStudent) =>
            await _studentsCollection.ReplaceOneAsync(x => x.Id == id, updatedStudent);

        public async Task RemoveAsync(string id) =>
            await _studentsCollection.DeleteOneAsync(x => x.Id == id);
    }
}