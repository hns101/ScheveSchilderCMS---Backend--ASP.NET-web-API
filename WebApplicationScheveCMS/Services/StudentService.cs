using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using WebApplicationScheveCMS.Models;
using Microsoft.Extensions.Logging; // Add this using statement
using System; // Add this using statement

namespace WebApplicationScheveCMS.Services
{
    public class StudentService
    {
        private readonly IMongoCollection<Student> _studentsCollection;
        private readonly IMongoCollection<Invoice> _invoicesCollection;
        private readonly ILogger<StudentService> _logger; // Inject ILogger

        public StudentService(
            IOptions<StudentDatabaseSettings> studentDatabaseSettings,
            ILogger<StudentService> logger) // Add ILogger to constructor
        {
            var mongoClient = new MongoClient(
                studentDatabaseSettings.Value.ConnectionString);

            var mongoDatabase = mongoClient.GetDatabase(
                studentDatabaseSettings.Value.DatabaseName);

            _studentsCollection = mongoDatabase.GetCollection<Student>(
                studentDatabaseSettings.Value.StudentsCollectionName);
            _invoicesCollection = mongoDatabase.GetCollection<Invoice>(
                studentDatabaseSettings.Value.InvoicesCollectionName);
            _logger = logger; // Assign logger
        }

        public async Task<List<Student>> GetAllAsync() =>
            await _studentsCollection.Find(_ => true).ToListAsync();

        public async Task<Student?> GetAsync(string id) =>
            await _studentsCollection.Find(x => x.Id == id).FirstOrDefaultAsync();

        public async Task<Student?> GetByStudentNumberAsync(string studentNumber) =>
            await _studentsCollection.Find(x => x.StudentNumber == studentNumber).FirstOrDefaultAsync();

        public async Task<Student?> GetStudentWithInvoicesAsync(string id)
        {
            try
            {
                // Ensure the ID is a valid ObjectId before proceeding
                if (!ObjectId.TryParse(id, out ObjectId objectId))
                {
                    _logger.LogWarning($"Attempted to get student with invalid ObjectId format: '{id}'");
                    return null; // Or throw a specific exception
                }

                var pipeline = new BsonDocument[]
                {
                    new BsonDocument("$match", new BsonDocument("_id", objectId)), // Use the parsed ObjectId
                    new BsonDocument("$lookup",
                        new BsonDocument
                        {
                            { "from", _invoicesCollection.CollectionNamespace.CollectionName },
                            { "localField", "_id" },
                            { "foreignField", "StudentId" },
                            { "as", "Invoices" }
                        }),
                    // Unwind and sort, but preserve students without invoices
                    new BsonDocument("$unwind", new BsonDocument
                    {
                        { "path", "$Invoices" },
                        { "preserveNullAndEmptyArrays", true }
                    }),
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
                            { "Invoices", new BsonDocument("$push", new BsonDocument
                                {
                                    { "id", "$Invoices._id" }, // MongoDB _id maps to 'id'
                                    { "date", "$Invoices.Date" },
                                    { "amountTotal", "$Invoices.AmountTotal" },
                                    { "vat", "$Invoices.VAT" },
                                    { "description", "$Invoices.Description" },
                                    { "invoicePdfPath", "$Invoices.InvoicePdfPath" }
                                })
                            }
                        })
                };

                var studentWithInvoices = await _studentsCollection
                    .Aggregate<Student>(pipeline)
                    .FirstOrDefaultAsync();

                return studentWithInvoices;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetStudentWithInvoicesAsync for ID: {id}.");
                throw; // Re-throw to be caught by the controller's try-catch
            }
        }

        public async Task CreateAsync(Student newStudent)
        {
            var existingStudent = await _studentsCollection.Find(s => s.StudentNumber == newStudent.StudentNumber).FirstOrDefaultAsync();
            if (existingStudent != null)
            {
                throw new InvalidOperationException($"Student with number '{newStudent.StudentNumber}' already exists.");
            }
            await _studentsCollection.InsertOneAsync(newStudent);
        }

        public async Task UpdateAsync(string id, Student updatedStudent) =>
            await _studentsCollection.ReplaceOneAsync(x => x.Id == id, updatedStudent);

        public async Task RemoveAsync(string id) =>
            await _studentsCollection.DeleteOneAsync(x => x.Id == id);
    }
}
