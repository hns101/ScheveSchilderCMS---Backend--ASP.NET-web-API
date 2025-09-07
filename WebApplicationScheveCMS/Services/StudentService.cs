using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using WebApplicationScheveCMS.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

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

        public async Task<Student?> GetAsync(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length != 24 || !ObjectId.TryParse(id, out _))
            {
                return null;
            }
            return await _studentsCollection.Find(x => x.Id == id).FirstOrDefaultAsync();
        }
        
        public async Task<Student?> GetByStudentNumberAsync(string studentNumber) =>
            await _studentsCollection.Find(x => x.StudentNumber == studentNumber).FirstOrDefaultAsync();

        public async Task<Student?> GetStudentWithInvoicesAsync(string id)
        {
            _logger.LogInformation("GetStudentWithInvoicesAsync received ID: '{Id}' (Length: {Length})", id, id?.Length ?? 0);

            try
            {
                if (string.IsNullOrEmpty(id) || id.Length != 24 || !ObjectId.TryParse(id, out ObjectId objectId))
                {
                    _logger.LogWarning("Attempted to get student with invalid ObjectId format: '{Id}'. ObjectId.TryParse failed.", id);
                    return null;
                }

                // First, get the student directly
                var student = await _studentsCollection.Find(x => x.Id == id).FirstOrDefaultAsync();
                
                if (student == null)
                {
                    _logger.LogWarning("Student with ID '{Id}' not found", id);
                    return null;
                }

                // Then get the invoices for this student
                var invoices = await _invoicesCollection
                    .Find(x => x.StudentId == id)
                    .SortByDescending(x => x.Date)
                    .ToListAsync();

                // Assign invoices to student
                student.Invoices = invoices;

                _logger.LogInformation("Successfully retrieved student '{StudentName}' with {InvoiceCount} invoices", student.Name, invoices.Count);

                return student;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetStudentWithInvoicesAsync for ID: '{Id}'.", id);
                throw;
            }
        }

        public async Task<Student?> GetStudentWithInvoicesByNumberAsync(string studentNumber)
        {
            _logger.LogInformation("GetStudentWithInvoicesByNumberAsync received StudentNumber: '{StudentNumber}'", studentNumber);

            try
            {
                // First, get the student by student number
                var student = await _studentsCollection.Find(x => x.StudentNumber == studentNumber).FirstOrDefaultAsync();
                
                if (student == null)
                {
                    _logger.LogWarning("Student with student number '{StudentNumber}' not found", studentNumber);
                    return null;
                }

                // Then get the invoices for this student using the student's ID
                var invoices = await _invoicesCollection
                    .Find(x => x.StudentId == student.Id)
                    .SortByDescending(x => x.Date)
                    .ToListAsync();

                // Assign invoices to student
                student.Invoices = invoices;

                _logger.LogInformation("Successfully retrieved student '{StudentName}' by number with {InvoiceCount} invoices", student.Name, invoices.Count);

                return student;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetStudentWithInvoicesByNumberAsync for StudentNumber: '{StudentNumber}'.", studentNumber);
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