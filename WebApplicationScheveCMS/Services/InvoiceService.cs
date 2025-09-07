using Microsoft.Extensions.Options;
using MongoDB.Driver;
using WebApplicationScheveCMS.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace WebApplicationScheveCMS.Services
{
    public class InvoiceService
    {
        private readonly IMongoCollection<Invoice> _invoicesCollection;
        private readonly ILogger<InvoiceService> _logger;

        public InvoiceService(
            IOptions<StudentDatabaseSettings> studentDatabaseSettings,
            ILogger<InvoiceService> logger)
        {
            var mongoClient = new MongoClient(
                studentDatabaseSettings.Value.ConnectionString);

            var mongoDatabase = mongoClient.GetDatabase(
                studentDatabaseSettings.Value.DatabaseName);

            _invoicesCollection = mongoDatabase.GetCollection<Invoice>(
                studentDatabaseSettings.Value.InvoicesCollectionName);
            
            _logger = logger;
        }

        // Get all invoices
        public async Task<List<Invoice>> GetAsync()
        {
            try
            {
                return await _invoicesCollection.Find(_ => true)
                    .SortByDescending(x => x.Date)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all invoices");
                throw;
            }
        }

        // Get a single invoice by ID
        public async Task<Invoice?> GetAsync(string id)
        {
            try
            {
                return await _invoicesCollection.Find(x => x.Id == id).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoice with ID: {InvoiceId}", id);
                throw;
            }
        }

        // Get all invoices for a specific student
        public async Task<List<Invoice>> GetInvoicesByStudentIdAsync(string studentId)
        {
            try
            {
                return await _invoicesCollection.Find(x => x.StudentId == studentId)
                    .SortByDescending(x => x.Date)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoices for student ID: {StudentId}", studentId);
                throw;
            }
        }

        // Create a new invoice
        public async Task CreateAsync(Invoice newInvoice)
        {
            try
            {
                await _invoicesCollection.InsertOneAsync(newInvoice);
                _logger.LogInformation("Created new invoice: {InvoiceId} for student: {StudentId}", newInvoice.Id, newInvoice.StudentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating invoice for student: {StudentId}", newInvoice.StudentId);
                throw;
            }
        }

        // Update an existing invoice
        public async Task UpdateAsync(string id, Invoice updatedInvoice)
        {
            try
            {
                await _invoicesCollection.ReplaceOneAsync(x => x.Id == id, updatedInvoice);
                _logger.LogInformation("Updated invoice: {InvoiceId}", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating invoice: {InvoiceId}", id);
                throw;
            }
        }

        // Remove an invoice by ID
        public async Task RemoveAsync(string id)
        {
            try
            {
                await _invoicesCollection.DeleteOneAsync(x => x.Id == id);
                _logger.LogInformation("Deleted invoice: {InvoiceId}", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting invoice: {InvoiceId}", id);
                throw;
            }
        }

        // Remove all invoices for a specific student
        public async Task RemoveAllByStudentIdAsync(string studentId)
        {
            try
            {
                var result = await _invoicesCollection.DeleteManyAsync(x => x.StudentId == studentId);
                _logger.LogInformation("Deleted {DeletedCount} invoices for student: {StudentId}", result.DeletedCount, studentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting invoices for student: {StudentId}", studentId);
                throw;
            }
        }

        // Get invoice statistics for a student
        public async Task<InvoiceStatistics> GetInvoiceStatisticsAsync(string studentId)
        {
            try
            {
                var invoices = await GetInvoicesByStudentIdAsync(studentId);
                
                return new InvoiceStatistics
                {
                    TotalInvoices = invoices.Count,
                    TotalAmount = invoices.Sum(i => i.AmountTotal),
                    AverageAmount = invoices.Count > 0 ? invoices.Average(i => i.AmountTotal) : 0,
                    LatestInvoiceDate = invoices.Count > 0 ? invoices.Max(i => i.Date) : (DateTime?)null,
                    OldestInvoiceDate = invoices.Count > 0 ? invoices.Min(i => i.Date) : (DateTime?)null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating invoice statistics for student: {StudentId}", studentId);
                throw;
            }
        }

        // Get invoices within a date range
        public async Task<List<Invoice>> GetInvoicesByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                return await _invoicesCollection.Find(x => x.Date >= startDate && x.Date <= endDate)
                    .SortByDescending(x => x.Date)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoices for date range: {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        // Get invoices by amount range
        public async Task<List<Invoice>> GetInvoicesByAmountRangeAsync(decimal minAmount, decimal maxAmount)
        {
            try
            {
                return await _invoicesCollection.Find(x => x.AmountTotal >= minAmount && x.AmountTotal <= maxAmount)
                    .SortByDescending(x => x.Date)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoices for amount range: {MinAmount} to {MaxAmount}", minAmount, maxAmount);
                throw;
            }
        }

        // Search invoices by description
        public async Task<List<Invoice>> SearchInvoicesByDescriptionAsync(string searchTerm)
        {
            try
            {
                var filter = Builders<Invoice>.Filter.Regex(x => x.Description, new MongoDB.Bson.BsonRegularExpression(searchTerm, "i"));
                return await _invoicesCollection.Find(filter)
                    .SortByDescending(x => x.Date)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching invoices by description: {SearchTerm}", searchTerm);
                throw;
            }
        }
    }

    // Helper class for invoice statistics
    public class InvoiceStatistics
    {
        public int TotalInvoices { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AverageAmount { get; set; }
        public DateTime? LatestInvoiceDate { get; set; }
        public DateTime? OldestInvoiceDate { get; set; }
    }
}