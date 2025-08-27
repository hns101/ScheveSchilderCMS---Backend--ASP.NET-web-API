using System.IO;

namespace WebApplicationScheveCMS.Services
{
    public class FileService
    {
        private readonly IWebHostEnvironment _env;

        public FileService(IWebHostEnvironment env)
        {
            _env = env;
        }

        // Saves a file to the server's file system
        public string SaveInvoicePdf(string fileName, byte[] pdfBytes)
        {
            // Define the path where files will be stored
            var invoicesPath = Path.Combine(_env.ContentRootPath, "Invoices");

            if (!Directory.Exists(invoicesPath))
            {
                Directory.CreateDirectory(invoicesPath);
            }

            var filePath = Path.Combine(invoicesPath, fileName);
            File.WriteAllBytes(filePath, pdfBytes);
            
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
