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
                _logger.LogError(ex, "Error getting all invoices.");
                return StatusCode(500, "Internal server error.");
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
                    return NotFound();
                }

                return Ok(invoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting invoice with ID: {id}.");
                return StatusCode(500, "Internal server error.");
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
                    return NotFound();
                }

                if (!string.IsNullOrEmpty(invoice.InvoicePdfPath))
                {
                    _fileService.DeleteFile(invoice.InvoicePdfPath);
                }

                await _invoiceService.RemoveAsync(id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting invoice with ID: {id}.");
                return StatusCode(500, "Internal server error.");
            }
        }

        // GET: api/invoices/file/{id}
        [HttpGet("file/{id}")]
        public async Task<IActionResult> GetFile(string id)
        {
            try
            {
                var invoice = await _invoiceService.GetAsync(id);
                if (invoice == null || string.IsNullOrEmpty(invoice.InvoicePdfPath))
                {
                    return NotFound("Invoice or PDF path not found.");
                }

                var filePath = invoice.InvoicePdfPath;
                
                if (System.IO.File.Exists(filePath))
                {
                    var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                    return File(fileBytes, "application/pdf", Path.GetFileName(filePath));
                }

                return NotFound("PDF file not found on server.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting invoice file for ID: {id}.");
                return StatusCode(500, "Internal server error.");
            }
        }

        // POST: api/invoices/batch-generate
        [HttpPost("batch-generate")]
        public async Task<IActionResult> BatchGenerate(BatchInvoiceRequest request)
        {
            if (request.StudentIds == null || !request.StudentIds.Any())
            {
                return BadRequest("No student IDs provided for batch generation.");
            }

            var generatedInvoices = new List<Invoice>();

            var defaultTemplatePath = _configuration["StudentDatabaseSettings:DefaultInvoiceTemplatePath"];
            if (string.IsNullOrEmpty(defaultTemplatePath) || !System.IO.File.Exists(defaultTemplatePath))
            {
                return StatusCode(500, "Default invoice template is not configured or not found.");
            }

            foreach (var studentId in request.StudentIds)
            {
                try
                {
                    var student = await _studentService.GetAsync(studentId);

                    if (student is null)
                    {
                        _logger.LogWarning($"Student with ID '{studentId}' not found. Skipping invoice generation for this student.");
                        continue;
                    }

                    var newInvoice = new Invoice
                    {
                        StudentId = student.Id!,
                        Date = DateTime.Now,
                        AmountTotal = request.AmountTotal,
                        VAT = request.VAT,
                        Description = request.Description
                    };
                    
                    var pdfBytes = _pdfService.GenerateInvoicePdf(student, newInvoice, defaultTemplatePath);
                    
                    var fileName = $"Factuur_{Guid.NewGuid()}.pdf";

                    newInvoice.InvoicePdfPath = _fileService.SavePdf(fileName, pdfBytes);

                    await _invoiceService.CreateAsync(newInvoice);
                    generatedInvoices.Add(newInvoice);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error generating invoice for student ID: {studentId}.");
                }
            }

            if (!generatedInvoices.Any())
            {
                return StatusCode(500, "No invoices were successfully generated.");
            }

            return Ok(generatedInvoices);
        }
        
        // New endpoint to upload and set the default template
        [HttpPost("template")]
        public async Task<IActionResult> UploadInvoiceTemplate(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest("No file uploaded.");
                }

                // Delete the old template if it exists
                var oldFilePath = _configuration["StudentDatabaseSettings:DefaultInvoiceTemplatePath"];
                if (!string.IsNullOrEmpty(oldFilePath) && System.IO.File.Exists(oldFilePath))
                {
                    System.IO.File.Delete(oldFilePath);
                }
                
                // Save the new file
                var filePath = _fileService.SavePdf(file.FileName, await ReadAllBytesAsync(file));

                // Read and update the appsettings.json file
                var appSettingsPath = Path.Combine(_env.ContentRootPath, "appsettings.json");
                var json = await System.IO.File.ReadAllTextAsync(appSettingsPath);
                var jsonObj = JsonNode.Parse(json) as JsonObject;
                
                if (jsonObj != null && jsonObj.ContainsKey("StudentDatabaseSettings"))
                {
                    var newJsonObj = (System.Text.Json.Nodes.JsonObject)System.Text.Json.Nodes.JsonNode.Parse(jsonObj.ToString()!)!;
                    newJsonObj["StudentDatabaseSettings"]!["DefaultInvoiceTemplatePath"] = filePath;
                    string output = newJsonObj.ToString();
                    await System.IO.File.WriteAllTextAsync(appSettingsPath, output);
                }
                
                return Ok(new { filePath });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading invoice template.");
                return StatusCode(500, "Internal server error during template upload.");
            }
        }
        
        // New endpoint to get the default template file for viewing
        [HttpGet("template")]
        public IActionResult GetInvoiceTemplate()
        {
            try
            {
                var filePath = _configuration["StudentDatabaseSettings:DefaultInvoiceTemplatePath"];
                
                if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
                {
                    return NotFound("Default invoice template not found.");
                }

                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                var fileName = Path.GetFileName(filePath);

                return File(fileBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoice template.");
                return StatusCode(500, "Internal server error.");
            }
        }

        // New endpoint to generate a preview of the default template
        [HttpGet("template/preview")]
        public IActionResult GetInvoiceTemplatePreview()
        {
            try
            {
                var dummyStudent = new Student
                {
                    Name = "Voorbeeld Student",
                    Address = "Voorbeeldstraat 1, 1234 AB Voorbeeldstad",
                    Email = "voorbeeld@student.nl",
                    StudentNumber = "P001",
                    BankName = "Voorbeeld Bank",
                    AccountNumber = "NL12VOOR0123456789"
                };

                var dummyInvoice = new Invoice
                {
                    Date = DateTime.Now,
                    AmountTotal = 50.00M,
                    VAT = 21.00M,
                    Description = "Dit is een voorbeeld van de factuur. De tekst is bewerkbaar."
                };
                
                var defaultTemplatePath = _configuration["StudentDatabaseSettings:DefaultInvoiceTemplatePath"];

                if (string.IsNullOrEmpty(defaultTemplatePath) || !System.IO.File.Exists(defaultTemplatePath))
                {
                    return NotFound("Default invoice template is not configured or not found.");
                }

                var pdfBytes = _pdfService.GenerateInvoicePdf(dummyStudent, dummyInvoice, defaultTemplatePath);
                var fileName = "preview.pdf";
                
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating invoice template preview.");
                return StatusCode(500, "Internal server error.");
            }
        }
        
        // Helper method to read IFormFile into a byte array
        private static async Task<byte[]> ReadAllBytesAsync(IFormFile file)
        {
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                return memoryStream.ToArray();
            }
        }
    }

    public class BatchInvoiceRequest
    {
        public List<string> StudentIds { get; set; } = new List<string>();
        public string? Description { get; set; }
        public decimal AmountTotal { get; set; }
        public decimal VAT { get; set; }
    }
}