using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace WebApplicationScheveCMS.Services
{
    public class FileService
    {
        private readonly IWebHostEnvironment _env;

        public FileService(IWebHostEnvironment env)
        {
            _env = env;
        }

        // Saves a PDF file to the server's file system and returns the full path
        public string SavePdf(string fileName, byte[] pdfBytes)
        {
            var folderPath = Path.Combine(_env.ContentRootPath, "Files", "Invoices");

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var filePath = Path.Combine(folderPath, fileName);
            File.WriteAllBytes(filePath, pdfBytes);
            
            return filePath;
        }

        // Saves a general file (like a contract) and returns the full path
        public string SaveStudentDocument(string studentId, IFormFile file)
        {
            // Create a unique folder for each student's documents
            var studentFolderPath = Path.Combine(_env.ContentRootPath, "Files", "StudentDocuments", studentId);
            
            if (!Directory.Exists(studentFolderPath))
            {
                Directory.CreateDirectory(studentFolderPath);
            }
            
            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(studentFolderPath, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                file.CopyTo(stream);
            }
            
            return filePath;
        }

        // Deletes a file from the file system
        public void DeleteFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
