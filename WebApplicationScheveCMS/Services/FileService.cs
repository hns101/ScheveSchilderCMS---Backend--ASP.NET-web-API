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
                var safeFileName = GetSafeFileName(fileName);
                var folderPath = Path.Combine(_env.ContentRootPath, "Files", "Invoices");

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                var filePath = Path.Combine(folderPath, safeFileName);
                File.WriteAllBytes(filePath, pdfBytes);
                
                _logger.LogInformation("PDF saved successfully: {FilePath}", filePath);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving PDF file: {FileName}", fileName);
                throw;
            }
        }

        public string SaveTemplateFile(string fileName, byte[] fileBytes)
        {
            try
            {
                var safeFileName = GetSafeFileName(fileName);
                var templatesDir = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, "templates");
                
                if (!Directory.Exists(templatesDir))
                {
                    Directory.CreateDirectory(templatesDir);
                }

                var filePath = Path.Combine(templatesDir, safeFileName);
                File.WriteAllBytes(filePath, fileBytes);
                
                _logger.LogInformation("Template file saved successfully: {FilePath}", filePath);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving template file: {FileName}", fileName);
                throw;
            }
        }

        public string SaveStudentDocument(string studentId, IFormFile file)
        {
            try
            {
                var safeFileName = GetSafeFileName(file.FileName);
                var studentFolderPath = Path.Combine(_env.ContentRootPath, "Files", "StudentDocuments", studentId);
                
                if (!Directory.Exists(studentFolderPath))
                {
                    Directory.CreateDirectory(studentFolderPath);
                }
                
                var uniqueFileName = $"{Guid.NewGuid()}_{safeFileName}";
                var filePath = Path.Combine(studentFolderPath, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    file.CopyTo(stream);
                }
                
                _logger.LogInformation("Student document saved successfully: {FilePath}", filePath);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving student document for student {StudentId}", studentId);
                throw;
            }
        }

        public void DeleteFile(string filePath)
        {
            try
            {
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
                throw;
            }
        }

        public bool FileExists(string filePath)
        {
            return File.Exists(filePath);
        }

        public FileInfo? GetFileInfo(string filePath)
        {
            return File.Exists(filePath) ? new FileInfo(filePath) : null;
        }

        private static string GetSafeFileName(string fileName)
        {
            // Remove path components and invalid characters
            var safeFileName = Path.GetFileName(fileName);
            var invalidChars = Path.GetInvalidFileNameChars();
            
            foreach (var invalidChar in invalidChars)
            {
                safeFileName = safeFileName.Replace(invalidChar, '_');
            }
            
            return safeFileName;
        }
    }
}