using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using WebApplicationScheveCMS.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

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
                _logger.LogInformation($"GetStudentWithInvoicesAsync received ID: '{id}' (Length: {id?.Length ?? 0})");

                if (string.IsNullOrEmpty(id) || id.Length != 24 || !ObjectId.TryParse(id, out ObjectId objectId))
                {
                    _logger.LogWarning($"Attempted to get student with invalid ObjectId format: '{id}'. ObjectId.TryParse failed.");
                    return null;
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
                    // Group back to a single student document, ensuring all fields are PascalCase
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
                        { "Invoices", new BsonDocument("$first", "$Invoices") } // Keep the entire invoices array from lookup
                    }),
                    // Project stage to sort invoices within the array and ensure consistent casing
                    // REMOVED explicit projection of "Id": "$_id" here.
                    new BsonDocument("$project", new BsonDocument
                    {
                        // "Id" is now implicitly mapped from "_id" by BsonClassMap
                        { "Name", "$Name" },
                        { "StudentNumber", "$StudentNumber" },
                        { "Address", "$Address" },
                        { "Email", "$Email" },
                        { "PhoneNumber", "$PhoneNumber" },
                        { "EmergencyContact", "$EmergencyContact" },
                        { "BankName", "$BankName" },
                        { "AccountNumber", "$AccountNumber" },
                        { "DateOfRegistration", "$DateOfRegistration" },
                        { "RegistrationDocumentPath", "$RegistrationDocumentPath" },
                        { "Invoices", new BsonDocument("$sortArray", new BsonDocument
                            {
                                { "input", "$Invoices" },
                                { "sortBy", new BsonDocument("Date", -1) } // Sort by invoice Date descending (PascalCase)
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
                _logger.LogError(ex, $"Error in GetStudentWithInvoicesAsync for ID: '{id}'.");
                throw;
            }
        }

        public async Task<Student?> GetStudentWithInvoicesByNumberAsync(string studentNumber)
        {
            try
            {
                _logger.LogInformation($"GetStudentWithInvoicesByNumberAsync received StudentNumber: '{studentNumber}'");

                var pipeline = new BsonDocument[]
                {
                    new BsonDocument("$match", new BsonDocument("StudentNumber", studentNumber)), // Match by StudentNumber
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
                        { "Invoices", new BsonDocument("$first", "$Invoices") }
                    }),
                    // Project stage to sort invoices within the array and ensure consistent casing
                    // REMOVED explicit projection of "Id": "$_id" here.
                    new BsonDocument("$project", new BsonDocument
                    {
                        // "Id" is now implicitly mapped from "_id" by BsonClassMap
                        { "Name", "$Name" },
                        { "StudentNumber", "$StudentNumber" },
                        { "Address", "$Address" },
                        { "Email", "$Email" },
                        { "PhoneNumber", "$PhoneNumber" },
                        { "EmergencyContact", "$EmergencyContact" },
                        { "BankName", "$BankName" },
                        { "AccountNumber", "$AccountNumber" },
                        { "DateOfRegistration", "$DateOfRegistration" },
                        { "RegistrationDocumentPath", "$RegistrationDocumentPath" },
                        { "Invoices", new BsonDocument("$sortArray", new BsonDocument
                            {
                                { "input", "$Invoices" },
                                { "sortBy", new BsonDocument("Date", -1) }
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
                _logger.LogError(ex, $"Error in GetStudentWithInvoicesByNumberAsync for StudentNumber: '{studentNumber}'.");
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
