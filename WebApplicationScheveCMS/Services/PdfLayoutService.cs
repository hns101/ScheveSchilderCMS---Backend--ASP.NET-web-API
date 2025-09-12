using Microsoft.Extensions.Options;
using MongoDB.Driver;
using WebApplicationScheveCMS.Models;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace WebApplicationScheveCMS.Services
{
    public interface IPdfLayoutService
    {
        Task<PdfLayoutSettings> GetLayoutSettingsAsync();
        Task<PdfLayoutSettings> UpdateLayoutSettingsAsync(PdfLayoutSettings settings);
        Task<PdfLayoutSettings> UpdateElementPositionAsync(string elementName, PdfElementPosition position);
        Task<PdfLayoutSettings> ResetToDefaultAsync();
    }

    public class PdfLayoutService : IPdfLayoutService
    {
        private readonly IMongoCollection<PdfLayoutSettings> _layoutCollection;
        private readonly ILogger<PdfLayoutService> _logger;
        private const string LAYOUT_ID = "default_layout";

        public PdfLayoutService(
            IOptions<StudentDatabaseSettings> studentDatabaseSettings,
            ILogger<PdfLayoutService> logger)
        {
            var mongoClient = new MongoClient(studentDatabaseSettings.Value.ConnectionString);
            var mongoDatabase = mongoClient.GetDatabase(studentDatabaseSettings.Value.DatabaseName);
            
            _layoutCollection = mongoDatabase.GetCollection<PdfLayoutSettings>(
                studentDatabaseSettings.Value.PdfLayoutSettingsCollectionName);
            _logger = logger;
        }

        public async Task<PdfLayoutSettings> GetLayoutSettingsAsync()
        {
            try
            {
                _logger.LogInformation("Retrieving PDF layout settings");

                var settings = await _layoutCollection
                    .Find(x => x.Id == LAYOUT_ID)
                    .FirstOrDefaultAsync();

                if (settings == null)
                {
                    _logger.LogInformation("No existing layout settings found, creating default settings");
                    settings = CreateDefaultSettings();
                    await _layoutCollection.InsertOneAsync(settings);
                    _logger.LogInformation("Created default PDF layout settings");
                }

                return settings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving PDF layout settings");
                throw;
            }
        }

        public async Task<PdfLayoutSettings> UpdateLayoutSettingsAsync(PdfLayoutSettings settings)
        {
            try
            {
                _logger.LogInformation("Updating PDF layout settings");
                
                settings.Id = LAYOUT_ID;
                settings.LastUpdated = DateTime.UtcNow;
                settings.UpdatedBy = "User";

                var filter = Builders<PdfLayoutSettings>.Filter.Eq(x => x.Id, LAYOUT_ID);
                var result = await _layoutCollection.ReplaceOneAsync(
                    filter, 
                    settings, 
                    new ReplaceOptions { IsUpsert = true });

                if (result.IsAcknowledged)
                {
                    _logger.LogInformation("PDF layout settings updated successfully");
                    return settings;
                }
                else
                {
                    throw new InvalidOperationException("Failed to update PDF layout settings");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating PDF layout settings");
                throw;
            }
        }

        public async Task<PdfLayoutSettings> UpdateElementPositionAsync(string elementName, PdfElementPosition position)
        {
            try
            {
                _logger.LogInformation("Updating element position: {ElementName}", elementName);

                var settings = await GetLayoutSettingsAsync();
                
                // Use reflection to update the specific element
                var property = typeof(PdfLayoutSettings).GetProperty(elementName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                {
                    throw new ArgumentException($"Element '{elementName}' not found in layout settings");
                }

                property.SetValue(settings, position);
                
                return await UpdateLayoutSettingsAsync(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating element position: {ElementName}", elementName);
                throw;
            }
        }

        public async Task<PdfLayoutSettings> ResetToDefaultAsync()
        {
            try
            {
                _logger.LogInformation("Resetting PDF layout settings to default");

                var defaultSettings = CreateDefaultSettings();
                return await UpdateLayoutSettingsAsync(defaultSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting PDF layout settings to default");
                throw;
            }
        }

        private static PdfLayoutSettings CreateDefaultSettings()
        {
            return new PdfLayoutSettings
            {
                Id = LAYOUT_ID,
                StudentName = new PdfElementPosition { Top = 150, Left = 400, FontSize = 10, TextAlign = "Left", IsBold = false, MaxHeight = 15 },
                StudentAddress = new PdfElementPosition { Top = 165, Left = 400, FontSize = 10, TextAlign = "Left", IsBold = false, MaxHeight = 15 },
                InvoiceId = new PdfElementPosition { Top = 195, Left = 400, FontSize = 10, TextAlign = "Left", IsBold = false, MaxHeight = 15 },
                InvoiceDate = new PdfElementPosition { Top = 210, Left = 400, FontSize = 10, TextAlign = "Left", IsBold = false, MaxHeight = 15 },
                InvoiceDescription = new PdfElementPosition { Top = 315, Left = 100, FontSize = 10, TextAlign = "Left", IsBold = false, MaxHeight = 15 },
                BaseAmount = new PdfElementPosition { Top = 400, Left = 500, FontSize = 10, TextAlign = "Left", IsBold = true, MaxHeight = 15 },
                VatAmount = new PdfElementPosition { Top = 415, Left = 500, FontSize = 10, TextAlign = "Left", IsBold = true, MaxHeight = 15 },
                TotalAmount = new PdfElementPosition { Top = 430, Left = 500, FontSize = 10, TextAlign = "Left", IsBold = true, MaxHeight = 15 },
                PaymentNote = new PdfElementPosition { Top = 500, Left = 100, FontSize = 10, TextAlign = "Left", IsBold = false, MaxHeight = 15 },
                ContactInfo = new PdfElementPosition { Top = 600, Left = 100, FontSize = 10, TextAlign = "Left", IsBold = false, MaxHeight = 15 },
                LastUpdated = DateTime.UtcNow,
                UpdatedBy = "System"
            };
        }
    }
}