using Microsoft.Extensions.Options;
using MongoDB.Driver;
using WebApplicationScheveCMS.Models;

namespace WebApplicationScheveCMS.Services
{
    public class InvoiceService
    {
        private readonly IMongoCollection<Invoice> _invoicesCollection;

        public InvoiceService(
            IOptions<StudentDatabaseSettings> studentDatabaseSettings)
        {
            var mongoClient = new MongoClient(
                studentDatabaseSettings.Value.ConnectionString);

            var mongoDatabase = mongoClient.GetDatabase(
                studentDatabaseSettings.Value.DatabaseName);

            _invoicesCollection = mongoDatabase.GetCollection<Invoice>(
                studentDatabaseSettings.Value.InvoicesCollectionName);
        }

        // Asynchronous method to get all invoices.
        public async Task<List<Invoice>> GetAsync() =>
            await _invoicesCollection.Find(_ => true).ToListAsync();

        // Asynchronous method to get a single invoice by ID.
        public async Task<Invoice?> GetAsync(string id) =>
            await _invoicesCollection.Find(x => x.Id == id).FirstOrDefaultAsync();

        // Asynchronous method to create a new invoice.
        public async Task CreateAsync(Invoice newInvoice) =>
            await _invoicesCollection.InsertOneAsync(newInvoice);

        // Asynchronous method to remove an invoice by ID.
        public async Task RemoveAsync(string id) =>
            await _invoicesCollection.DeleteOneAsync(x => x.Id == id);
    }
}
