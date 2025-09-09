using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WebApplicationScheveCMS.Models
{
    public class StudentDatabaseSettings
    {
        public string ConnectionString { get; set; } = null!;
        public string DatabaseName { get; set; } = null!;
        public string StudentsCollectionName { get; set; } = null!;
        public string InvoicesCollectionName { get; set; } = null!;
        public string SystemSettingsCollectionName { get; set; } = "SystemSettings";
        public string DefaultInvoiceTemplatePath { get; set; } = "";
        public string FileStoragePath { get; set; } = "Files";
        public long MaxFileUploadSize { get; set; } = 5 * 1024 * 1024; // 5MB
    }

    [BsonIgnoreExtraElements]
    public class SystemSettings
    {
        [BsonId]
        [BsonElement("_id")]
        public string? Id { get; set; }

        [BsonElement("DefaultInvoiceTemplatePath")]
        public string? DefaultInvoiceTemplatePath { get; set; }

        [BsonElement("LastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        [BsonElement("UpdatedBy")]
        public string UpdatedBy { get; set; } = "System";
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Message { get; set; }
        public List<string>? Errors { get; set; }

        public static ApiResponse<T> SuccessResult(T data, string? message = null)
        {
            return new ApiResponse<T>
            {
                Success = true,
                Data = data,
                Message = message
            };
        }

        public static ApiResponse<T> ErrorResult(string message, List<string>? errors = null)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                Errors = errors
            };
        }
    }
}