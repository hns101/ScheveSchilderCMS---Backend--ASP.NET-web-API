using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace WebApplicationScheveCMS.Services
{
    public interface IFileService
    {
        string SavePdf(string fileName, byte[] pdfBytes);
        string SaveTemplateFile(string fileName, byte[] fileBytes);
        string SaveStudentDocument(string studentId, IFormFile file);
        void DeleteFile(string filePath);
        bool FileExists(string filePath);
        FileInfo? GetFileInfo(string filePath);
    }

    public class FileService : IFileService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<FileService> _logger;

        public FileService(IWebHostEnvironment env, ILogger<FileService> logger)
        {
            _env = env;
            _logger = logger;
        }

        public string SavePdf(string fileName, byte[] pdfBytes)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    throw new ArgumentException("File name cannot be null or empty", nameof(fileName));
                }

                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    throw new ArgumentException("PDF bytes cannot be null or empty", nameof(pdfBytes));
                }

                var safeFileName = GetSafeFileName(fileName);
                var folderPath = Path.Combine(_env.ContentRootPath, "Files", "Invoices");

                _logger.LogDebug("Saving PDF to folder: {FolderPath}", folderPath);

                // Ensure directory exists
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                    _logger.LogInformation("Created directory: {FolderPath}", folderPath);
                }

                var filePath = Path.Combine(folderPath, safeFileName);
                
                // Check if file already exists and create unique name if needed
                var uniqueFilePath = GetUniqueFilePath(filePath);
                
                File.WriteAllBytes(uniqueFilePath, pdfBytes);
                
                _logger.LogInformation("PDF saved successfully: {FilePath} ({FileSize} bytes)", uniqueFilePath, pdfBytes.Length);
                return uniqueFilePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving PDF file: {FileName}", fileName);
                throw new InvalidOperationException($"Failed to save PDF file '{fileName}': {ex.Message}", ex);
            }
        }

        public string SaveTemplateFile(string fileName, byte[] fileBytes)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    throw new ArgumentException("File name cannot be null or empty", nameof(fileName));
                }

                if (fileBytes == null || fileBytes.Length == 0)
                {
                    throw new ArgumentException("File bytes cannot be null or empty", nameof(fileBytes));
                }

                var safeFileName = GetSafeFileName(fileName);
                var templatesDir = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, "templates");
                
                _logger.LogDebug("Saving template to folder: {TemplatesDir}", templatesDir);

                if (!Directory.Exists(templatesDir))
                {
                    Directory.CreateDirectory(templatesDir);
                    _logger.LogInformation("Created templates directory: {TemplatesDir}", templatesDir);
                }

                var filePath = Path.Combine(templatesDir, safeFileName);
                File.WriteAllBytes(filePath, fileBytes);
                
                _logger.LogInformation("Template file saved successfully: {FilePath} ({FileSize} bytes)", filePath, fileBytes.Length);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving template file: {FileName}", fileName);
                throw new InvalidOperationException($"Failed to save template file '{fileName}': {ex.Message}", ex);
            }
        }

        public string SaveStudentDocument(string studentId, IFormFile file)
        {
            try
            {
                if (string.IsNullOrEmpty(studentId))
                {
                    throw new ArgumentException("Student ID cannot be null or empty", nameof(studentId));
                }

                if (file == null || file.Length == 0)
                {
                    throw new ArgumentException("File cannot be null or empty", nameof(file));
                }

                var safeFileName = GetSafeFileName(file.FileName);
                var studentFolderPath = Path.Combine(_env.ContentRootPath, "Files", "StudentDocuments", studentId);
                
                _logger.LogDebug("Saving student document to folder: {StudentFolderPath}", studentFolderPath);

                if (!Directory.Exists(studentFolderPath))
                {
                    Directory.CreateDirectory(studentFolderPath);
                    _logger.LogInformation("Created student documents directory: {StudentFolderPath}", studentFolderPath);
                }
                
                var uniqueFileName = $"{Guid.NewGuid()}_{safeFileName}";
                var filePath = Path.Combine(studentFolderPath, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    file.CopyTo(stream);
                }
                
                _logger.LogInformation("Student document saved successfully: {FilePath} ({FileSize} bytes)", filePath, file.Length);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving student document for student {StudentId}: {FileName}", studentId, file?.FileName);
                throw new InvalidOperationException($"Failed to save student document for student {studentId}: {ex.Message}", ex);
            }
        }

        public void DeleteFile(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    _logger.LogWarning("Attempted to delete file with null or empty path");
                    return;
                }

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("File deleted successfully: {FilePath}", filePath);
                }
                else
                {
                    _logger.LogWarning("Attempted to delete non-existent file: {FilePath}", filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file: {FilePath}", filePath);
                throw new InvalidOperationException($"Failed to delete file '{filePath}': {ex.Message}", ex);
            }
        }

        public bool FileExists(string filePath)
        {
            try
            {
                return !string.IsNullOrEmpty(filePath) && File.Exists(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking if file exists: {FilePath}", filePath);
                return false;
            }
        }

        public FileInfo? GetFileInfo(string filePath)
        {
            try
            {
                return File.Exists(filePath) ? new FileInfo(filePath) : null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting file info: {FilePath}", filePath);
                return null;
            }
        }

        private static string GetSafeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return "unknown_file";
            }

            // Remove path components and invalid characters
            var safeFileName = Path.GetFileName(fileName);
            var invalidChars = Path.GetInvalidFileNameChars();
            
            foreach (var invalidChar in invalidChars)
            {
                safeFileName = safeFileName.Replace(invalidChar, '_');
            }

            // Remove any potentially dangerous characters
            safeFileName = safeFileName.Replace("..", "_");
            
            return string.IsNullOrEmpty(safeFileName) ? "unknown_file" : safeFileName;
        }

        private static string GetUniqueFilePath(string originalPath)
        {
            if (!File.Exists(originalPath))
            {
                return originalPath;
            }

            var directory = Path.GetDirectoryName(originalPath);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalPath);
            var extension = Path.GetExtension(originalPath);

            var counter = 1;
            string uniquePath;

            do
            {
                var uniqueFileName = $"{fileNameWithoutExtension}_{counter}{extension}";
                uniquePath = Path.Combine(directory!, uniqueFileName);
                counter++;
            } while (File.Exists(uniquePath));

            return uniquePath;
        }
    }
}