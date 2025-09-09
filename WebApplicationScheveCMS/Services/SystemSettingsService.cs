using Microsoft.Extensions.Options;
using MongoDB.Driver;
using WebApplicationScheveCMS.Models;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

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
            
            // Create index on Id field for better performance
            CreateIndexes();
        }

        private void CreateIndexes()
        {
            try
            {
                var indexKeys = Builders<SystemSettings>.IndexKeys.Ascending(x => x.Id);
                var indexOptions = new CreateIndexOptions { Unique = true };
                _settingsCollection.Indexes.CreateOne(new CreateIndexModel<SystemSettings>(indexKeys, indexOptions));
                _logger.LogInformation("Created unique index on SystemSettings.Id");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Index may already exist or failed to create for SystemSettings");
            }
        }

        public async Task<SystemSettings?> GetSettingsAsync()
        {
            try
            {
                _logger.LogInformation("Attempting to retrieve system settings with ID: {SettingsId}", SETTINGS_ID);
                
                var settings = await _settingsCollection
                    .Find(x => x.Id == SETTINGS_ID)
                    .FirstOrDefaultAsync();

                if (settings == null)
                {
                    _logger.LogInformation("No existing settings found, creating default settings");
                    
                    // Create default settings if none exist
                    settings = new SystemSettings
                    {
                        Id = SETTINGS_ID,
                        DefaultInvoiceTemplatePath = "",
                        LastUpdated = DateTime.UtcNow,
                        UpdatedBy = "System"
                    };
                    
                    await _settingsCollection.InsertOneAsync(settings);
                    _logger.LogInformation("Created default system settings with ID: {SettingsId}", SETTINGS_ID);
                }
                else
                {
                    _logger.LogInformation("Retrieved existing system settings: {TemplatePath}", 
                        settings.DefaultInvoiceTemplatePath ?? "NULL");
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

                var options = new UpdateOptions { IsUpsert = true };
                var result = await _settingsCollection.UpdateOneAsync(filter, update, options);

                if (result.IsAcknowledged)
                {
                    if (result.UpsertedId != null)
                    {
                        _logger.LogInformation("Created new settings document with template path: {TemplatePath}", templatePath);
                    }
                    else
                    {
                        _logger.LogInformation("Updated existing settings with template path: {TemplatePath}", templatePath);
                    }
                }
                else
                {
                    _logger.LogWarning("Update operation was not acknowledged by MongoDB");
                    throw new InvalidOperationException("Failed to update template path - operation not acknowledged");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating template path to: {TemplatePath}", templatePath);
                throw;
            }
        }

        public async Task<SystemSettings> CreateOrUpdateSettingsAsync(SystemSettings settings)
        {
            try
            {
                if (settings == null)
                {
                    throw new ArgumentNullException(nameof(settings));
                }

                settings.Id = SETTINGS_ID;
                settings.LastUpdated = DateTime.UtcNow;
                settings.UpdatedBy = settings.UpdatedBy ?? "System";

                _logger.LogInformation("Creating or updating system settings");

                var filter = Builders<SystemSettings>.Filter.Eq(x => x.Id, SETTINGS_ID);
                var options = new ReplaceOptions { IsUpsert = true };
                var result = await _settingsCollection.ReplaceOneAsync(filter, settings, options);

                if (result.IsAcknowledged)
                {
                    _logger.LogInformation("System settings updated successfully");
                    return settings;
                }
                else
                {
                    _logger.LogWarning("Replace operation was not acknowledged by MongoDB");
                    throw new InvalidOperationException("Failed to update settings - operation not acknowledged");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating system settings");
                throw;
            }
        }
    }
}