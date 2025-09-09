using Microsoft.Extensions.Options;
using MongoDB.Driver;
using WebApplicationScheveCMS.Models;
using Microsoft.Extensions.Logging;

namespace WebApplicationScheveCMS.Services
{
    public class SystemSettingsService
    {
        private readonly IMongoCollection<SystemSettings> _settingsCollection;
        private readonly ILogger<SystemSettingsService> _logger;
        private const string SETTINGS_ID = "default_settings";

        public SystemSettingsService(
            IOptions<StudentDatabaseSettings> studentDatabaseSettings,
            ILogger<SystemSettingsService> logger)
        {
            var mongoClient = new MongoClient(studentDatabaseSettings.Value.ConnectionString);
            var mongoDatabase = mongoClient.GetDatabase(studentDatabaseSettings.Value.DatabaseName);
            
            _settingsCollection = mongoDatabase.GetCollection<SystemSettings>(
                studentDatabaseSettings.Value.SystemSettingsCollectionName);
            _logger = logger;
        }

        public async Task<SystemSettings?> GetSettingsAsync()
        {
            try
            {
                _logger.LogInformation("Retrieving system settings");
                
                var settings = await _settingsCollection
                    .Find(x => x.Id == SETTINGS_ID)
                    .FirstOrDefaultAsync();

                if (settings == null)
                {
                    _logger.LogInformation("No existing settings found, creating default settings");
                    
                    settings = new SystemSettings
                    {
                        Id = SETTINGS_ID,
                        DefaultInvoiceTemplatePath = "",
                        LastUpdated = DateTime.UtcNow,
                        UpdatedBy = "System"
                    };
                    
                    await _settingsCollection.InsertOneAsync(settings);
                    _logger.LogInformation("Created default system settings");
                }

                return settings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving system settings");
                return null;
            }
        }

        public async Task UpdateTemplatePathAsync(string templatePath)
        {
            try
            {
                _logger.LogInformation("Updating template path to: {TemplatePath}", templatePath);
                
                var filter = Builders<SystemSettings>.Filter.Eq(x => x.Id, SETTINGS_ID);
                var update = Builders<SystemSettings>.Update
                    .Set(x => x.DefaultInvoiceTemplatePath, templatePath)
                    .Set(x => x.LastUpdated, DateTime.UtcNow)
                    .Set(x => x.UpdatedBy, "System");

                var result = await _settingsCollection.UpdateOneAsync(
                    filter, 
                    update, 
                    new UpdateOptions { IsUpsert = true });

                if (result.IsAcknowledged)
                {
                    _logger.LogInformation("Template path updated successfully");
                }
                else
                {
                    throw new InvalidOperationException("Failed to update template path");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating template path");
                throw;
            }
        }
    }
}