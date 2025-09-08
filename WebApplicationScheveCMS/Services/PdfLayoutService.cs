using Microsoft.Extensions.Options;
using MongoDB.Driver;
using WebApplicationScheveCMS.Models;
using Microsoft.Extensions.Logging;

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
        private const string LAYOUT_SETTINGS_ID = "default_pdf_layout";

        public PdfLayoutService(
            IOptions<StudentDatabaseSettings> studentDatabaseSettings,
            ILogger<PdfLayoutService> logger)
        {
            var mongoClient = new MongoClient(studentDatabaseSettings.Value.ConnectionString);
            var mongoDatabase = mongoClient.GetDatabase(studentDatabaseSettings.Value.DatabaseName);
            
            _layoutCollection = mongoDatabase.GetCollection<PdfLayoutSettings>("PdfLayoutSettings");
            _logger = logger;
        }

        public async Task<PdfLayoutSettings> GetLayoutSettingsAsync()
        {
            try
            {
                var settings = await _layoutCollection
                    .Find(x => x.Id == LAYOUT_SETTINGS_ID)
                    .FirstOrDefaultAsync();

                if (settings == null)
                {
                    // Create default settings if none exist
                    settings = CreateDefaultLayoutSettings();
                    await _layoutCollection.InsertOneAsync(settings);
                    _logger.LogInformation("Created default PDF layout settings");
                }

                return settings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving PDF layout settings");
                return CreateDefaultLayoutSettings();
            }
        }

        public async Task<PdfLayoutSettings> UpdateLayoutSettingsAsync(PdfLayoutSettings settings)
        {
            try
            {
                settings.Id = LAYOUT_SETTINGS_ID;
                settings.LastUpdated = DateTime.Now;

                await _layoutCollection.ReplaceOneAsync(
                    x => x.Id == LAYOUT_SETTINGS_ID,
                    settings,
                    new ReplaceOptions { IsUpsert = true });

                _logger.LogInformation("PDF layout settings updated successfully");
                return settings;
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
                var settings = await GetLayoutSettingsAsync();
                
                // Update the specific element position using reflection
                var property = typeof(PdfLayoutSettings).GetProperty(elementName);
                if (property != null && property.PropertyType == typeof(PdfElementPosition))
                {
                    property.SetValue(settings, position);
                    return await UpdateLayoutSettingsAsync(settings);
                }
                else
                {
                    throw new ArgumentException($"Invalid element name: {elementName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating element position for: {ElementName}", elementName);
                throw;
            }
        }

        public async Task<PdfLayoutSettings> ResetToDefaultAsync()
        {
            try
            {
                var defaultSettings = CreateDefaultLayoutSettings();
                return await UpdateLayoutSettingsAsync(defaultSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting PDF layout to default");
                throw;
            }
        }

        private static PdfLayoutSettings CreateDefaultLayoutSettings()
        {
            return new PdfLayoutSettings
            {
                Id = LAYOUT_SETTINGS_ID,
                StudentName = new PdfElementPosition { Top = 150, Left = 400, FontSize = 10 },
                StudentAddress = new PdfElementPosition { Top = 165, Left = 400, FontSize = 10 },
                InvoiceId = new PdfElementPosition { Top = 195, Left = 400, FontSize = 10 },
                InvoiceDate = new PdfElementPosition { Top = 210, Left = 400, FontSize = 10 },
                InvoiceDescription = new PdfElementPosition { Top = 315, Left = 100, FontSize = 10 },
                BaseAmount = new PdfElementPosition { Top = 400, Left = 500, FontSize = 10, IsBold = true },
                VatAmount = new PdfElementPosition { Top = 415, Left = 500, FontSize = 10, IsBold = true },
                TotalAmount = new PdfElementPosition { Top = 430, Left = 500, FontSize = 10, IsBold = true },
                PaymentNote = new PdfElementPosition { Top = 500, Left = 100, FontSize = 10 },
                ContactInfo = new PdfElementPosition { Top = 600, Left = 100, FontSize = 10 },
                LastUpdated = DateTime.Now,
                UpdatedBy = "System"
            };
        }
    }
}