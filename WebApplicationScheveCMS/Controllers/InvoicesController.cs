using Microsoft.AspNetCore.Mvc;
using WebApplicationScheveCMS.Models;
using WebApplicationScheveCMS.Services;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WebApplicationScheveCMS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InvoicesController : ControllerBase
    {
        private readonly InvoiceService _invoiceService;
        private readonly StudentService _studentService;
        private readonly PdfService _pdfService;
        private readonly FileService _fileService;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<InvoicesController> _logger;
        private readonly IConfiguration _configuration;

        // Allowed image file extensions
        private readonly string[] _allowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff" };
        private const long MaxFileSize = 5 * 1024 * 1024; // 5MB

        public InvoicesController(
            InvoiceService invoiceService, 
            StudentService studentService, 
            PdfService pdfService, 
            FileService fileService,
            IWebHostEnvironment env,
            ILogger<InvoicesController> logger,
            IConfiguration configuration)
        {
            _invoiceService = invoiceService;
            _studentService = studentService;
            _pdfService = pdfService;
            _fileService = fileService;
            _env = env;
            _logger = logger;
            _configuration = configuration;
        }

        // GET: api/invoices
        [HttpGet]
        public async Task<ActionResult<List<Invoice>>> Get()
        {
            try
            {
                var invoices = await _invoiceService.GetAsync();
                return Ok(invoices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all invoices");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/invoices/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Invoice>> Get(string id)
        {
            try
            {
                var invoice = await _invoiceService.GetAsync(id);

                if (invoice is null)
                {
                    return NotFound($"Invoice with ID '{id}' not found");
                }

                return Ok(invoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting invoice with ID: {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }
        
        // DELETE: api/invoices/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                var invoice = await _invoiceService.GetAsync(id);

                if (invoice is null)
                {
                    return NotFound($"Invoice with ID '{id}' not found");
                }

                // Clean up associated PDF file
                if (!string.IsNullOrEmpty(invoice.InvoicePdfPath) && System.IO.File.Exists(invoice.InvoicePdfPath))
                {
                    try
                    {
                        System.IO.File.Delete(invoice.InvoicePdfPath);
                    }
                    catch (Exception fileEx)
                    {
                        _logger.LogWarning(fileEx, "Could not delete PDF file: {FilePath}", invoice.InvoicePdfPath);
                    }
                }

                await _invoiceService.RemoveAsync(id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting invoice with ID: {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/invoices/file/{id}
        [HttpGet("file/{id}")]
        public async Task<IActionResult> GetFile(string id)
        {
            try
            {
                var invoice = await _invoiceService.GetAsync(id);
                if (invoice == null)
                {
                    return NotFound($"Invoice with ID '{id}' not found");
                }

                if (string.IsNullOrEmpty(invoice.InvoicePdfPath))
                {
                    return NotFound("PDF file path not found for this invoice");
                }

                if (!System.IO.File.Exists(invoice.InvoicePdfPath))
                {
                    return NotFound("PDF file not found on server");
                }

                var fileBytes = await System.IO.File.ReadAllBytesAsync(invoice.InvoicePdfPath);
                var fileName = Path.GetFileName(invoice.InvoicePdfPath);
                
                return File(fileBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting invoice file for ID: {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/invoices/batch-generate
        [HttpPost("batch-generate")]
        public async Task<IActionResult> BatchGenerate(BatchInvoiceRequest request)
        {
            try
            {
                // Validate request
                var validationResult = ValidateBatchRequest(request);
                if (!validationResult.IsValid)
                {
                    return BadRequest(validationResult.ErrorMessage);
                }

                var defaultTemplatePath = _configuration["StudentDatabaseSettings:DefaultInvoiceTemplatePath"];
                if (string.IsNullOrEmpty(defaultTemplatePath) || !System.IO.File.Exists(defaultTemplatePath))
                {
                    return BadRequest("Default invoice template is not configured or file not found");
                }

                var results = new BatchGenerationResult
                {
                    SuccessfulInvoices = new List<Invoice>(),
                    Errors = new List<string>()
                };

                foreach (var studentId in request.StudentIds)
                {
                    try
                    {
                        var student = await _studentService.GetAsync(studentId);

                        if (student is null)
                        {
                            var errorMsg = $"Student with ID '{studentId}' not found";
                            _logger.LogWarning(errorMsg);
                            results.Errors.Add(errorMsg);
                            continue;
                        }

                        var newInvoice = new Invoice
                        {
                            StudentId = student.Id!,
                            Date = DateTime.Now,
                            AmountTotal = request.AmountTotal,
                            VAT = request.VAT,
                            Description = request.Description ?? "Invoice"
                        };
                        
                        var pdfBytes = _pdfService.GenerateInvoicePdf(student, newInvoice, defaultTemplatePath);
                        
                        var fileName = $"Invoice_{student.StudentNumber}_{DateTime.Now:yyyyMMdd}_{Guid.NewGuid():N[..8]}.pdf";
                        newInvoice.InvoicePdfPath = _fileService.SavePdf(fileName, pdfBytes);

                        await _invoiceService.CreateAsync(newInvoice);
                        results.SuccessfulInvoices.Add(newInvoice);
                        
                        _logger.LogInformation("Successfully generated invoice for student: {StudentId}", studentId);
                    }
                    catch (Exception ex)
                    {
                        var errorMsg = $"Error generating invoice for student ID '{studentId}': {ex.Message}";
                        _logger.LogError(ex, "Error generating invoice for student ID: {StudentId}", studentId);
                        results.Errors.Add(errorMsg);
                    }
                }

                if (!results.SuccessfulInvoices.Any())
                {
                    return StatusCode(500, new { message = "No invoices were successfully generated", errors = results.Errors });
                }

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch invoice generation");
                return StatusCode(500, "Internal server error during batch generation");
            }
        }
        
        // POST: api/invoices/template - Upload invoice template (image)
        [HttpPost("template")]
        public async Task<IActionResult> UploadInvoiceTemplate(IFormFile file)
        {
            try
            {
                // Validate file
                var validationResult = ValidateImageFile(file);
                if (!validationResult.IsValid)
                {
                    return BadRequest(validationResult.ErrorMessage);
                }

                // Delete the old template if it exists
                await CleanupOldTemplate();
                
                // Save the new file with a consistent name
                var fileName = $"invoice_template{Path.GetExtension(file.FileName).ToLowerInvariant()}";
                var fileBytes = await ReadAllBytesAsync(file);
                
                // Use existing FileService.SavePdf method (assuming it can save any file type)
                // Or create templates directory and save manually
                var filePath = SaveTemplateFile(fileName, fileBytes);

                // Update configuration
                await UpdateTemplatePathInConfig(filePath);
                
                _logger.LogInformation("Invoice template updated successfully: {FilePath}", filePath);
                
                return Ok(new { 
                    message = "Template uploaded successfully", 
                    filePath,
                    fileName = fileName,
                    fileSize = fileBytes.Length
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading invoice template");
                return StatusCode(500, "Internal server error during template upload");
            }
        }
        
        // GET: api/invoices/template - Get the default template image for viewing
        [HttpGet("template")]
        public IActionResult GetInvoiceTemplate()
        {
            try
            {
                var filePath = _configuration["StudentDatabaseSettings:DefaultInvoiceTemplatePath"];
                
                if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
                {
                    return NotFound("Default invoice template not found");
                }

                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                var fileName = Path.GetFileName(filePath);
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                
                // Determine content type based on extension
                var contentType = extension switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".bmp" => "image/bmp",
                    ".tiff" => "image/tiff",
                    _ => "application/octet-stream"
                };

                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoice template");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/invoices/template/preview - Generate a preview of the template with dummy data
        [HttpGet("template/preview")]
        public IActionResult GetInvoiceTemplatePreview()
        {
            try
            {
                var defaultTemplatePath = _configuration["StudentDatabaseSettings:DefaultInvoiceTemplatePath"];

                if (string.IsNullOrEmpty(defaultTemplatePath) || !System.IO.File.Exists(defaultTemplatePath))
                {
                    return NotFound("Default invoice template is not configured or not found");
                }

                var dummyStudent = CreateDummyStudent();
                var dummyInvoice = CreateDummyInvoice();

                var pdfBytes = _pdfService.GenerateInvoicePdf(dummyStudent, dummyInvoice, defaultTemplatePath);
                var fileName = $"template_preview_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating invoice template preview");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/invoices/template/info - Get template information
        [HttpGet("template/info")]
        public IActionResult GetTemplateInfo()
        {
            try
            {
                var filePath = _configuration["StudentDatabaseSettings:DefaultInvoiceTemplatePath"];
                
                if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
                {
                    return Ok(new { hasTemplate = false, message = "No template configured" });
                }

                var fileInfo = new FileInfo(filePath);
                
                return Ok(new
                {
                    hasTemplate = true,
                    fileName = fileInfo.Name,
                    fileSize = fileInfo.Length,
                    lastModified = fileInfo.LastWriteTime,
                    extension = fileInfo.Extension
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting template info");
                return StatusCode(500, "Internal server error");
            }
        }

        #region Private Helper Methods

        private string SaveTemplateFile(string fileName, byte[] fileBytes)
        {
            // Create templates directory if it doesn't exist
            var templatesDir = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, "templates");
            if (!Directory.Exists(templatesDir))
            {
                Directory.CreateDirectory(templatesDir);
            }

            var filePath = Path.Combine(templatesDir, fileName);
            System.IO.File.WriteAllBytes(filePath, fileBytes);
            
            return filePath;
        }

        private static async Task<byte[]> ReadAllBytesAsync(IFormFile file)
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }

        private ValidationResult ValidateImageFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return new ValidationResult(false, "No file uploaded");
            }

            if (file.Length > MaxFileSize)
            {
                return new ValidationResult(false, $"File size exceeds maximum allowed size of {MaxFileSize / (1024 * 1024)}MB");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_allowedImageExtensions.Contains(extension))
            {
                return new ValidationResult(false, $"Invalid file type. Allowed types: {string.Join(", ", _allowedImageExtensions)}");
            }

            return new ValidationResult(true);
        }

        private ValidationResult ValidateBatchRequest(BatchInvoiceRequest request)
        {
            if (request?.StudentIds == null || !request.StudentIds.Any())
            {
                return new ValidationResult(false, "No student IDs provided for batch generation");
            }

            if (request.AmountTotal <= 0)
            {
                return new ValidationResult(false, "Amount total must be greater than zero");
            }

            if (request.VAT < 0 || request.VAT > 100)
            {
                return new ValidationResult(false, "VAT must be between 0 and 100 percent");
            }

            return new ValidationResult(true);
        }

        private async Task CleanupOldTemplate()
        {
            var oldFilePath = _configuration["StudentDatabaseSettings:DefaultInvoiceTemplatePath"];
            if (!string.IsNullOrEmpty(oldFilePath) && System.IO.File.Exists(oldFilePath))
            {
                try
                {
                    await Task.Run(() => System.IO.File.Delete(oldFilePath));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not delete old template file: {FilePath}", oldFilePath);
                }
            }
        }

        private async Task UpdateTemplatePathInConfig(string filePath)
        {
            var appSettingsPath = Path.Combine(_env.ContentRootPath, "appsettings.json");
            var json = await System.IO.File.ReadAllTextAsync(appSettingsPath);
            var jsonObj = JsonNode.Parse(json) as JsonObject;
            
            if (jsonObj?.ContainsKey("StudentDatabaseSettings") == true)
            {
                var settingsNode = jsonObj["StudentDatabaseSettings"];
                if (settingsNode is JsonObject settingsObj)
                {
                    settingsObj["DefaultInvoiceTemplatePath"] = filePath;
                    var output = jsonObj.ToString();
                    await System.IO.File.WriteAllTextAsync(appSettingsPath, output);
                }
            }
        }

        private static Student CreateDummyStudent()
        {
            return new Student
            {
                Name = "Voorbeeld Student",
                Address = "Voorbeeldstraat 123, 1234 AB Voorbeeldstad",
                Email = "voorbeeld@student.nl",
                StudentNumber = "STU001",
                BankName = "Voorbeeld Bank",
                AccountNumber = "NL12VOOR0123456789"
            };
        }

        private static Invoice CreateDummyInvoice()
        {
            return new Invoice
            {
                Date = DateTime.Now,
                AmountTotal = 125.50M,
                VAT = 21.00M,
                Description = "Voorbeeld factuur beschrijving - Dit is een voorbeeldtekst die toont hoe de factuur eruit ziet"
            };
        }

        #endregion
    }

    public class BatchInvoiceRequest
    {
        public List<string> StudentIds { get; set; } = new List<string>();
        public string? Description { get; set; }
        public decimal AmountTotal { get; set; }
        public decimal VAT { get; set; }
    }

    public class BatchGenerationResult
    {
        public List<Invoice> SuccessfulInvoices { get; set; } = new List<Invoice>();
        public List<string> Errors { get; set; } = new List<string>();
        public int SuccessCount => SuccessfulInvoices.Count;
        public int ErrorCount => Errors.Count;
    }

    public class ValidationResult
    {
        public bool IsValid { get; }
        public string ErrorMessage { get; }

        public ValidationResult(bool isValid, string errorMessage = "")
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }
    }
}