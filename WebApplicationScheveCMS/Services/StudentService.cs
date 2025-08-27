using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using WebApplicationScheveCMS.Models;

namespace WebApplicationScheveCMS.Services
{
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

        // Renamed from GetAsync() to be more explicit
        public async Task<List<Student>> GetAllAsync() =>
            await _studentsCollection.Find(_ => true).ToListAsync();

        // New method to get a single student without invoices
        public async Task<Student?> GetAsync(string id) =>
            await _studentsCollection.Find(x => x.Id == id).FirstOrDefaultAsync();

        public async Task<Student?> GetStudentWithInvoicesAsync(string id)
        {
            var pipeline = new BsonDocument[]
            {
                new BsonDocument("$match", new BsonDocument("_id", new ObjectId(id))),
                new BsonDocument("$lookup",
                    new BsonDocument
                    {
                        { "from", _invoicesCollection.CollectionNamespace.CollectionName },
                        { "localField", "_id" },
                        { "foreignField", "StudentId" },
                        { "as", "Invoices" }
                    }),
                new BsonDocument("$unwind", new BsonDocument
                {
                    { "path", "$Invoices" },
                    { "preserveNullAndEmptyArrays", true } // Keep students with no invoices
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
                                { "InvoiceId", "$Invoices.Id" },
                                { "InvoiceDate", "$Invoices.Date" },
                                { "AmountTotal", "$Invoices.AmountTotal" },
                                { "VAT", "$Invoices.VAT" },
                                { "Description", "$Invoices.Description" },
                                { "InvoicePdfPath", "$Invoices.InvoicePdfPath" }
                            })
                        }
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
