using Microsoft.Extensions.Options;
using MongoDB.Driver;
using WebApplicationScheveCMS.Models;
using Microsoft.Extensions.Logging;

namespace WebApplicationScheveCMS.Services
{
    public interface ISystemSettingsService
    {
        Task<SystemSettings?> GetSettingsAsync();
        Task UpdateTemplatePathAsync(string templatePath);
        Task<SystemSettings> CreateOrUpdateSettingsAsync(SystemSettings settings);
    }

    public class SystemSettingsService : ISystemSettingsService
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
                var settings = await _settingsCollection
                    .Find(x => x.Id == SETTINGS_ID)
                    .FirstOrDefaultAsync();

                if (settings == null)
                {
                    // Create default settings if none exist
                    settings = new SystemSettings
                    {
                        Id = SETTINGS_ID,
                        DefaultInvoiceTemplatePath = "",
                        LastUpdated = DateTime.Now,
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
                var settings = await GetSettingsAsync();
                if (settings == null)
                {
                    settings = new SystemSettings { Id = SETTINGS_ID };
                }

                settings.DefaultInvoiceTemplatePath = templatePath;
                settings.LastUpdated = DateTime.Now;
                settings.UpdatedBy = "System";

                await _settingsCollection.ReplaceOneAsync(
                    x => x.Id == SETTINGS_ID,
                    settings,
                    new ReplaceOptions { IsUpsert = true });

                _logger.LogInformation("Updated template path to: {TemplatePath}", templatePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating template path");
                throw;
            }
        }

        public async Task<SystemSettings> CreateOrUpdateSettingsAsync(SystemSettings settings)
        {
            try
            {
                settings.Id = SETTINGS_ID;
                settings.LastUpdated = DateTime.Now;

                await _settingsCollection.ReplaceOneAsync(
                    x => x.Id == SETTINGS_ID,
                    settings,
                    new ReplaceOptions { IsUpsert = true });

                _logger.LogInformation("System settings updated successfully");
                return settings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating system settings");
                throw;
            }
        }
    }
}