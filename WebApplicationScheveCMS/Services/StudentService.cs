using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using WebApplicationScheveCMS.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic; // Added for List<Invoice>

namespace WebApplicationScheveCMS.Services
{
    public class StudentService
    {
        private readonly IMongoCollection<Student> _studentsCollection;
        private readonly IMongoCollection<Invoice> _invoicesCollection;
        private readonly ILogger<StudentService> _logger;

        public StudentService(
            IOptions<StudentDatabaseSettings> studentDatabaseSettings,
            ILogger<StudentService> logger)
        {
            var mongoClient = new MongoClient(
                studentDatabaseSettings.Value.ConnectionString);

            var mongoDatabase = mongoClient.GetDatabase(
                studentDatabaseSettings.Value.DatabaseName);

            _studentsCollection = mongoDatabase.GetCollection<Student>(
                studentDatabaseSettings.Value.StudentsCollectionName);
            _invoicesCollection = mongoDatabase.GetCollection<Invoice>(
                studentDatabaseSettings.Value.InvoicesCollectionName);
            _logger = logger;
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
                // Add a basic length check before attempting to parse
                if (string.IsNullOrEmpty(id) || id.Length != 24 || !ObjectId.TryParse(id, out ObjectId objectId))
                {
                    _logger.LogWarning($"Attempted to get student with invalid ObjectId format: '{id}'");
                    // Throw a FormatException here to be caught by the controller and return BadRequest
                    throw new FormatException($"Invalid ObjectId format for ID: '{id}'");
                }

                var pipeline = new BsonDocument[]
                {
                    new BsonDocument("$match", new BsonDocument("_id", objectId)),
                    new BsonDocument("$lookup",
                        new BsonDocument
                        {
                            { "from", _invoicesCollection.CollectionNamespace.CollectionName },
                            { "localField", "_id" },
                            { "foreignField", "StudentId" },
                            { "as", "Invoices" }
                        }),
                    new BsonDocument("$group", new BsonDocument
                    {
                        { "_id", "$_id" },
                        { "name", new BsonDocument("$first", "$Name") },
                        { "studentNumber", new BsonDocument("$first", "$StudentNumber") },
                        { "address", new BsonDocument("$first", "$Address") },
                        { "email", new BsonDocument("$first", "$Email") },
                        { "phoneNumber", new BsonDocument("$first", "$PhoneNumber") },
                        { "emergencyContact", new BsonDocument("$first", "$EmergencyContact") },
                        { "bankName", new BsonDocument("$first", "$BankName") },
                        { "accountNumber", new BsonDocument("$first", "$AccountNumber") },
                        { "dateOfRegistration", new BsonDocument("$first", "$DateOfRegistration") },
                        { "registrationDocumentPath", new BsonDocument("$first", "$RegistrationDocumentPath") },
                        { "invoices", new BsonDocument("$first", "$Invoices") }
                    }),
                    new BsonDocument("$project", new BsonDocument
                    {
                        { "_id", "$_id" },
                        { "name", "$name" },
                        { "studentNumber", "$studentNumber" },
                        { "address", "$address" },
                        { "email", "$email" },
                        { "phoneNumber", "$phoneNumber" },
                        { "emergencyContact", "$emergencyContact" },
                        { "bankName", "$bankName" },
                        { "accountNumber", "$accountNumber" },
                        { "dateOfRegistration", "$dateOfRegistration" },
                        { "registrationDocumentPath", "$registrationDocumentPath" },
                        { "invoices", new BsonDocument("$sortArray", new BsonDocument
                            {
                                { "input", "$invoices" },
                                { "sortBy", new BsonDocument("date", -1) }
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
                throw;
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
