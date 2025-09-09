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
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using ValidationResult = WebApplicationScheveCMS.Models.ValidationResult;

namespace WebApplicationScheveCMS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InvoicesController : ControllerBase
    {
        private readonly InvoiceService _invoiceService;
        private readonly StudentService _studentService;
        private readonly IPdfService _pdfService;
        private readonly IFileService _fileService;
        private readonly SystemSettingsService _systemSettingsService;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<InvoicesController> _logger;
        private readonly StudentDatabaseSettings _settings;

        // Allowed image file extensions
        private readonly string[] _allowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff" };

        public InvoicesController(
            InvoiceService invoiceService, 
            StudentService studentService, 
            IPdfService pdfService, 
            IFileService fileService,
            SystemSettingsService systemSettingsService,
            IWebHostEnvironment env,
            ILogger<InvoicesController> logger,
            IOptions<StudentDatabaseSettings> settings)
        {
            _invoiceService = invoiceService;
            _studentService = studentService;
            _pdfService = pdfService;
            _fileService = fileService;
            _systemSettingsService = systemSettingsService;
            _env = env;
            _logger = logger;
            _settings = settings.Value;
        }

        // GET: api/invoices
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<Invoice>>>> Get()
        {
            try
            {
                var invoices = await _invoiceService.GetAsync();
                _logger.LogInformation("Retrieved {Count} invoices", invoices.Count);
                
                return Ok(ApiResponse<List<Invoice>>.SuccessResult(invoices, $"Retrieved {invoices.Count} invoices"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all invoices");
                return StatusCode(500, ApiResponse<List<Invoice>>.ErrorResult("Internal server error"));
            }
        }

        // GET: api/invoices/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<Invoice>>> Get(string id)
        {
            try
            {
                if (!IsValidObjectId(id))
                {
                    return BadRequest(ApiResponse<Invoice>.ErrorResult("Invalid invoice ID format"));
                }

                var invoice = await _invoiceService.GetAsync(id);

                if (invoice is null)
                {
                    return NotFound(ApiResponse<Invoice>.ErrorResult($"Invoice with ID '{id}' not found"));
                }

                return Ok(ApiResponse<Invoice>.SuccessResult(invoice));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting invoice with ID: {Id}", id);
                return StatusCode(500, ApiResponse<Invoice>.ErrorResult("Internal server error"));
            }
        }
        
        // DELETE: api/invoices/{id}
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<object>>> Delete(string id)
        {
            try
            {
                if (!IsValidObjectId(id))
                {
                    return BadRequest(ApiResponse<object>.ErrorResult("Invalid invoice ID format"));
                }

                var invoice = await _invoiceService.GetAsync(id);

                if (invoice is null)
                {
                    return NotFound(ApiResponse<object>.ErrorResult($"Invoice with ID '{id}' not found"));
                }

                // Clean up associated PDF file
                if (!string.IsNullOrEmpty(invoice.InvoicePdfPath))
                {
                    try
                    {
                        _fileService.DeleteFile(invoice.InvoicePdfPath);
                    }
                    catch (Exception fileEx)
                    {
                        _logger.LogWarning(fileEx, "Could not delete PDF file: {FilePath}", invoice.InvoicePdfPath);
                    }
                }

                await _invoiceService.RemoveAsync(id);
                _logger.LogInformation("Invoice deleted successfully: {InvoiceId}", id);
                
                return Ok(ApiResponse<object>.SuccessResult(null, "Invoice deleted successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting invoice with ID: {Id}", id);
                return StatusCode(500, ApiResponse<object>.ErrorResult("Internal server error"));
            }
        }

        // GET: api/invoices/file/{id}
        [HttpGet("file/{id}")]
        public async Task<IActionResult> GetFile(string id)
        {
            try
            {
                if (!IsValidObjectId(id))
                {
                    return BadRequest("Invalid invoice ID format");
                }

                var invoice = await _invoiceService.GetAsync(id);
                if (invoice == null)
                {
                    return NotFound($"Invoice with ID '{id}' not found");
                }

                if (string.IsNullOrEmpty(invoice.InvoicePdfPath))
                {
                    return NotFound("PDF file path not found for this invoice");
                }

                if (!_fileService.FileExists(invoice.InvoicePdfPath))
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
        public async Task<ActionResult<ApiResponse<BatchGenerationResult>>> BatchGenerate([FromBody] BatchInvoiceRequest request)
        {
            try
            {
                _logger.LogInformation("Starting batch generation request with {StudentCount} students", request?.StudentIds?.Count ?? 0);

                // Enhanced validation with detailed logging
                var validationResult = ValidateBatchRequest(request);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Batch request validation failed: {ErrorMessage}", validationResult.ErrorMessage);
                    return BadRequest(ApiResponse<BatchGenerationResult>.ErrorResult(validationResult.ErrorMessage, validationResult.Errors));
                }

                // Check system settings with enhanced error handling
                SystemSettings? systemSettings = null;
                try
                {
                    systemSettings = await _systemSettingsService.GetSettingsAsync();
                    _logger.LogInformation("Retrieved system settings: {HasSettings}", systemSettings != null);
                }
                catch (Exception settingsEx)
                {
                    _logger.LogError(settingsEx, "Failed to retrieve system settings");
                    return StatusCode(500, ApiResponse<BatchGenerationResult>.ErrorResult("Failed to retrieve system settings"));
                }

                var defaultTemplatePath = systemSettings?.DefaultInvoiceTemplatePath;
                _logger.LogInformation("Template path from settings: {TemplatePath}", defaultTemplatePath ?? "NULL");

                if (string.IsNullOrEmpty(defaultTemplatePath))
                {
                    _logger.LogWarning("No default invoice template path configured in system settings");
                    return BadRequest(ApiResponse<BatchGenerationResult>.ErrorResult("Default invoice template is not configured. Please upload a template in Settings."));
                }

                if (!_fileService.FileExists(defaultTemplatePath))
                {
                    _logger.LogWarning("Template file does not exist at path: {TemplatePath}", defaultTemplatePath);
                    return BadRequest(ApiResponse<BatchGenerationResult>.ErrorResult($"Default invoice template file not found at path: {defaultTemplatePath}"));
                }

                // Ensure directories exist
                try
                {
                    var invoicesDir = Path.Combine(_env.ContentRootPath, "Files", "Invoices");
                    if (!Directory.Exists(invoicesDir))
                    {
                        Directory.CreateDirectory(invoicesDir);
                        _logger.LogInformation("Created invoices directory: {InvoicesDir}", invoicesDir);
                    }
                }
                catch (Exception dirEx)
                {
                    _logger.LogError(dirEx, "Failed to create invoices directory");
                    return StatusCode(500, ApiResponse<BatchGenerationResult>.ErrorResult("Failed to create necessary directories"));
                }

                var results = new BatchGenerationResult
                {
                    SuccessfulInvoices = new List<Invoice>(),
                    Errors = new List<string>()
                };

                _logger.LogInformation("Starting batch generation for {Count} students", request.StudentIds.Count);

                foreach (var studentId in request.StudentIds)
                {
                    try
                    {
                        _logger.LogDebug("Processing student ID: {StudentId}", studentId);

                        if (!IsValidObjectId(studentId))
                        {
                            var errorMsg = $"Invalid student ID format: '{studentId}'";
                            _logger.LogWarning(errorMsg);
                            results.Errors.Add(errorMsg);
                            continue;
                        }

                        // Get student with enhanced error handling
                        Student? student = null;
                        try
                        {
                            student = await _studentService.GetAsync(studentId);
                        }
                        catch (Exception studentEx)
                        {
                            var errorMsg = $"Database error retrieving student '{studentId}': {studentEx.Message}";
                            _logger.LogError(studentEx, "Error retrieving student: {StudentId}", studentId);
                            results.Errors.Add(errorMsg);
                            continue;
                        }

                        if (student is null)
                        {
                            var errorMsg = $"Student with ID '{studentId}' not found";
                            _logger.LogWarning(errorMsg);
                            results.Errors.Add(errorMsg);
                            continue;
                        }

                        _logger.LogDebug("Found student: {StudentName} ({StudentNumber})", student.Name, student.StudentNumber);

                        // Create invoice object
                        var newInvoice = new Invoice
                        {
                            StudentId = student.Id!,
                            Date = DateTime.Now,
                            AmountTotal = request.AmountTotal,
                            VAT = request.VAT,
                            Description = request.Description ?? "Invoice"
                        };

                        // Generate PDF with enhanced error handling
                        byte[] pdfBytes;
                        try
                        {
                            _logger.LogDebug("Generating PDF for student: {StudentId}", studentId);
                            pdfBytes = _pdfService.GenerateInvoicePdf(student, newInvoice, defaultTemplatePath);
                            _logger.LogDebug("PDF generated successfully, size: {PdfSize} bytes", pdfBytes.Length);
                        }
                        catch (Exception pdfEx)
                        {
                            var errorMsg = $"PDF generation failed for student '{student.Name}': {pdfEx.Message}";
                            _logger.LogError(pdfEx, "Error generating PDF for student: {StudentId}", studentId);
                            results.Errors.Add(errorMsg);
                            continue;
                        }

                        // Save PDF file with enhanced error handling and FIXED filename generation
                        try
                        {
                            // FIXED: Proper GUID formatting
                            var guidString = Guid.NewGuid().ToString("N")[..8];
                            var fileName = $"Invoice_{student.StudentNumber}_{DateTime.Now:yyyyMMdd}_{guidString}.pdf";
                            
                            _logger.LogDebug("Saving PDF file: {FileName}", fileName);
                            newInvoice.InvoicePdfPath = _fileService.SavePdf(fileName, pdfBytes);
                            _logger.LogDebug("PDF saved to: {PdfPath}", newInvoice.InvoicePdfPath);
                        }
                        catch (Exception fileEx)
                        {
                            var errorMsg = $"Failed to save PDF for student '{student.Name}': {fileEx.Message}";
                            _logger.LogError(fileEx, "Error saving PDF for student: {StudentId}", studentId);
                            results.Errors.Add(errorMsg);
                            continue;
                        }

                        // Save invoice to database with enhanced error handling
                        try
                        {
                            _logger.LogDebug("Saving invoice to database for student: {StudentId}", studentId);
                            await _invoiceService.CreateAsync(newInvoice);
                            results.SuccessfulInvoices.Add(newInvoice);
                            _logger.LogInformation("Successfully generated invoice {InvoiceId} for student: {StudentName}", newInvoice.Id, student.Name);
                        }
                        catch (Exception dbEx)
                        {
                            var errorMsg = $"Database error saving invoice for student '{student.Name}': {dbEx.Message}";
                            _logger.LogError(dbEx, "Error saving invoice to database for student: {StudentId}", studentId);
                            results.Errors.Add(errorMsg);
                            
                            // Clean up PDF file if database save failed
                            try
                            {
                                if (!string.IsNullOrEmpty(newInvoice.InvoicePdfPath))
                                {
                                    _fileService.DeleteFile(newInvoice.InvoicePdfPath);
                                }
                            }
                            catch (Exception cleanupEx)
                            {
                                _logger.LogWarning(cleanupEx, "Failed to cleanup PDF file after database error: {PdfPath}", newInvoice.InvoicePdfPath);
                            }
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        var errorMsg = $"Unexpected error processing student ID '{studentId}': {ex.Message}";
                        _logger.LogError(ex, "Unexpected error processing student ID: {StudentId}", studentId);
                        results.Errors.Add(errorMsg);
                    }
                }

                if (!results.SuccessfulInvoices.Any() && results.Errors.Any())
                {
                    _logger.LogError("No invoices were successfully generated. Errors: {ErrorCount}", results.ErrorCount);
                    return StatusCode(500, ApiResponse<BatchGenerationResult>.ErrorResult("No invoices were successfully generated", results.Errors));
                }

                _logger.LogInformation("Batch generation completed. Success: {SuccessCount}, Errors: {ErrorCount}", 
                    results.SuccessCount, results.ErrorCount);

                var successMessage = $"Generated {results.SuccessCount} invoices successfully";
                if (results.ErrorCount > 0)
                {
                    successMessage += $" with {results.ErrorCount} errors";
                }

                return Ok(ApiResponse<BatchGenerationResult>.SuccessResult(results, successMessage));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error in batch invoice generation");
                return StatusCode(500, ApiResponse<BatchGenerationResult>.ErrorResult($"Internal server error during batch generation: {ex.Message}"));
            }
        }
        
        // POST: api/invoices/template - Upload invoice template (image)
        [HttpPost("template")]
        public async Task<ActionResult<ApiResponse<TemplateUploadResult>>> UploadInvoiceTemplate(IFormFile file)
        {
            try
            {
                _logger.LogInformation("Starting template upload process");
                
                // Validate file
                var validationResult = ValidateImageFile(file);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Template file validation failed: {ErrorMessage}", validationResult.ErrorMessage);
                    return BadRequest(ApiResponse<TemplateUploadResult>.ErrorResult(validationResult.ErrorMessage, validationResult.Errors));
                }

                _logger.LogInformation("File validation passed, file size: {FileSize} bytes", file.Length);

                // Delete the old template if it exists
                await CleanupOldTemplate();
                
                // Save the new file with a consistent name
                var fileName = $"invoice_template{Path.GetExtension(file.FileName).ToLowerInvariant()}";
                var fileBytes = await ReadAllBytesAsync(file);
                
                _logger.LogInformation("Saving new template file: {FileName}", fileName);
                var filePath = _fileService.SaveTemplateFile(fileName, fileBytes);
                _logger.LogInformation("Template file saved to: {FilePath}", filePath);

                // Update system settings in database
                _logger.LogInformation("Updating system settings with new template path");
                try 
                {
                    await _systemSettingsService.UpdateTemplatePathAsync(filePath);
                    _logger.LogInformation("System settings updated successfully with template path: {FilePath}", filePath);
                }
                catch (Exception settingsEx)
                {
                    _logger.LogError(settingsEx, "Failed to update system settings with template path: {FilePath}", filePath);
                    
                    // Try to clean up the uploaded file since we couldn't update the database
                    try 
                    {
                        _fileService.DeleteFile(filePath);
                        _logger.LogInformation("Cleaned up uploaded file due to settings update failure");
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger.LogWarning(cleanupEx, "Failed to cleanup file after settings update failure: {FilePath}", filePath);
                    }
                    
                    return StatusCode(500, ApiResponse<TemplateUploadResult>.ErrorResult("Failed to save template settings to database"));
                }
                
                _logger.LogInformation("Invoice template updated successfully: {FilePath}", filePath);

                var result = new TemplateUploadResult
                {
                    Message = "Template uploaded successfully",
                    FilePath = filePath,
                    FileName = fileName,
                    FileSize = fileBytes.Length
                };
                
                return Ok(ApiResponse<TemplateUploadResult>.SuccessResult(result, "Template uploaded successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading invoice template");
                return StatusCode(500, ApiResponse<TemplateUploadResult>.ErrorResult("Internal server error during template upload"));
            }
        }
        
        // GET: api/invoices/template - Get the default template image for viewing
        [HttpGet("template")]
        public async Task<IActionResult> GetInvoiceTemplate()
        {
            try
            {
                var systemSettings = await _systemSettingsService.GetSettingsAsync();
                var filePath = systemSettings?.DefaultInvoiceTemplatePath;
                
                if (string.IsNullOrEmpty(filePath) || !_fileService.FileExists(filePath))
                {
                    return NotFound("Default invoice template not found");
                }

                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
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
        public async Task<IActionResult> GetInvoiceTemplatePreview()
        {
            try
            {
                var systemSettings = await _systemSettingsService.GetSettingsAsync();
                var defaultTemplatePath = systemSettings?.DefaultInvoiceTemplatePath;

                if (string.IsNullOrEmpty(defaultTemplatePath) || !_fileService.FileExists(defaultTemplatePath))
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
        public async Task<ActionResult<ApiResponse<TemplateInfo>>> GetTemplateInfo()
        {
            try
            {
                _logger.LogInformation("Retrieving template info");
                
                var systemSettings = await _systemSettingsService.GetSettingsAsync();
                var filePath = systemSettings?.DefaultInvoiceTemplatePath;
                
                _logger.LogInformation("System settings retrieved, template path: {FilePath}", filePath ?? "NULL");
                
                if (string.IsNullOrEmpty(filePath) || !_fileService.FileExists(filePath))
                {
                    _logger.LogInformation("No template configured or file doesn't exist");
                    var noTemplateInfo = new TemplateInfo
                    {
                        HasTemplate = false,
                        Message = "No template configured"
                    };
                    return Ok(ApiResponse<TemplateInfo>.SuccessResult(noTemplateInfo));
                }

                var fileInfo = _fileService.GetFileInfo(filePath);
                
                if (fileInfo == null)
                {
                    _logger.LogWarning("File exists check passed but GetFileInfo returned null for: {FilePath}", filePath);
                    var noTemplateInfo = new TemplateInfo
                    {
                        HasTemplate = false,
                        Message = "Template file information unavailable"
                    };
                    return Ok(ApiResponse<TemplateInfo>.SuccessResult(noTemplateInfo));
                }
                
                var templateInfo = new TemplateInfo
                {
                    HasTemplate = true,
                    FileName = fileInfo.Name,
                    FileSize = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime,
                    Extension = fileInfo.Extension
                };
                
                _logger.LogInformation("Template info retrieved successfully: {FileName}, {FileSize} bytes", 
                    templateInfo.FileName, templateInfo.FileSize);
                
                return Ok(ApiResponse<TemplateInfo>.SuccessResult(templateInfo));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting template info");
                return StatusCode(500, ApiResponse<TemplateInfo>.ErrorResult("Internal server error"));
            }
        }

        #region Private Helper Methods

        private static async Task<byte[]> ReadAllBytesAsync(IFormFile file)
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }

        private ValidationResult ValidateImageFile(IFormFile file)
        {
            var errors = new List<string>();

            if (file == null || file.Length == 0)
            {
                return ValidationResult.Failure("No file uploaded");
            }

            if (file.Length > _settings.MaxFileUploadSize)
            {
                errors.Add($"File size exceeds maximum allowed size of {_settings.MaxFileUploadSize / (1024 * 1024)}MB");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_allowedImageExtensions.Contains(extension))
            {
                errors.Add($"Invalid file type. Allowed types: {string.Join(", ", _allowedImageExtensions)}");
            }

            // Additional file content validation could be added here
            if (!IsValidImageContent(file))
            {
                errors.Add("File content is not a valid image");
            }

            return errors.Any() ? ValidationResult.Failure(errors) : ValidationResult.Success();
        }

        private ValidationResult ValidateBatchRequest(BatchInvoiceRequest? request)
        {
            var errors = new List<string>();

            if (request == null)
            {
                return ValidationResult.Failure("Request body is null or invalid");
            }

            if (request.StudentIds == null || !request.StudentIds.Any())
            {
                errors.Add("No student IDs provided for batch generation");
            }
            else
            {
                var invalidIds = request.StudentIds.Where(id => string.IsNullOrWhiteSpace(id) || !IsValidObjectId(id)).ToList();
                if (invalidIds.Any())
                {
                    errors.Add($"Invalid student ID format: {string.Join(", ", invalidIds)}");
                }
            }

            if (request.AmountTotal <= 0)
            {
                errors.Add("Amount total must be greater than zero");
            }

            if (request.VAT < 0 || request.VAT > 100)
            {
                errors.Add("VAT must be between 0 and 100 percent");
            }

            return errors.Any() ? ValidationResult.Failure(errors) : ValidationResult.Success();
        }

        private async Task CleanupOldTemplate()
        {
            try
            {
                _logger.LogInformation("Attempting to cleanup old template");
                
                var systemSettings = await _systemSettingsService.GetSettingsAsync();
                var oldFilePath = systemSettings?.DefaultInvoiceTemplatePath;
                
                if (!string.IsNullOrEmpty(oldFilePath) && _fileService.FileExists(oldFilePath))
                {
                    _fileService.DeleteFile(oldFilePath);
                    _logger.LogInformation("Deleted old template file: {OldFilePath}", oldFilePath);
                }
                else
                {
                    _logger.LogInformation("No old template file to cleanup");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not cleanup old template file - continuing with upload");
            }
        }

        private static bool IsValidObjectId(string? id)
        {
            return !string.IsNullOrEmpty(id) && 
                   id.Length == 24 && 
                   ObjectId.TryParse(id, out _);
        }

        private static bool IsValidImageContent(IFormFile file)
        {
            try
            {
                // Basic image validation - you could enhance this with ImageSharp or similar
                var validHeaders = new Dictionary<string, byte[]>
                {
                    { ".jpg", new byte[] { 0xFF, 0xD8, 0xFF } },
                    { ".jpeg", new byte[] { 0xFF, 0xD8, 0xFF } },
                    { ".png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
                    { ".gif", new byte[] { 0x47, 0x49, 0x46, 0x38 } },
                    { ".bmp", new byte[] { 0x42, 0x4D } }
                };

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!validHeaders.ContainsKey(extension)) return true; // Allow other types for now

                using var stream = file.OpenReadStream();
                var header = new byte[8];
                stream.Read(header, 0, header.Length);
                stream.Position = 0; // Reset for potential future reads

                var expectedHeader = validHeaders[extension];
                return header.Take(expectedHeader.Length).SequenceEqual(expectedHeader);
            }
            catch
            {
                return false;
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
}