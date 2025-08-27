using Microsoft.Extensions.Options;
using MongoDB.Driver;
using WebApplicationScheveCMS.Models;

namespace WebApplicationScheveCMS.Services
{
    public class StudentService
    {
        // IMongoCollection is the main class for interacting with a specific MongoDB collection.
        private readonly IMongoCollection<Student> _studentsCollection;

        public StudentService(
            IOptions<StudentDatabaseSettings> studentDatabaseSettings)
        {
            // MongoClient is the entry point for all interactions with a MongoDB server.
            var mongoClient = new MongoClient(
                studentDatabaseSettings.Value.ConnectionString);

            // Get a reference to the database and then to the specific collection.
            var mongoDatabase = mongoClient.GetDatabase(
                studentDatabaseSettings.Value.DatabaseName);

            _studentsCollection = mongoDatabase.GetCollection<Student>(
                studentDatabaseSettings.Value.StudentsCollectionName);
        }

        // Asynchronous method to get all students from the collection.
        public async Task<List<Student>> GetAsync() =>
            await _studentsCollection.Find(_ => true).ToListAsync();

        // Asynchronous method to get a single student by ID.
        public async Task<Student?> GetAsync(string id) =>
            await _studentsCollection.Find(x => x.Id == id).FirstOrDefaultAsync();

        // Asynchronous method to create a new student.
        public async Task CreateAsync(Student newStudent) =>
            await _studentsCollection.InsertOneAsync(newStudent);

        // Asynchronous method to update an existing student by ID.
        public async Task UpdateAsync(string id, Student updatedStudent) =>
            await _studentsCollection.ReplaceOneAsync(x => x.Id == id, updatedStudent);

        // Asynchronous method to delete a student by ID.
        public async Task RemoveAsync(string id) =>
            await _studentsCollection.DeleteOneAsync(x => x.Id == id);
    }
}