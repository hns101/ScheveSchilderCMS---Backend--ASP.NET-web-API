using Microsoft.AspNetCore.Mvc;
using WebApplicationScheveCMS.Models;
using WebApplicationScheveCMS.Services;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Hosting; // Ensure this is present
using Microsoft.Extensions.Logging; // Add this using statement

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
        private readonly ILogger<InvoicesController> _logger; // Inject ILogger

        public InvoicesController(
            InvoiceService invoiceService, 
            StudentService studentService, 
            PdfService pdfService, 
            FileService fileService,
            IWebHostEnvironment env,
            ILogger<InvoicesController> logger) // Add ILogger to constructor
        {
            _invoiceService = invoiceService;
            _studentService = studentService;
            _pdfService = pdfService;
            _fileService = fileService;
            _env = env;
            _logger = logger; // Assign logger
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

                var filePath = invoice.InvoicePdfPath; // Path from DB is already full path
                
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

            foreach (var studentId in request.StudentIds)
            {
                try // Add try-catch around each student's invoice generation
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
                    
                    // Generate the PDF
                    var pdfBytes = _pdfService.GenerateInvoicePdf(student, newInvoice);
                    
                    // Generate a unique file name for the PDF
                    var fileName = $"Factuur_{Guid.NewGuid()}.pdf";

                    // Save the PDF and get the full file path
                    newInvoice.InvoicePdfPath = _fileService.SaveInvoicePdf(fileName, pdfBytes);

                    // Save the invoice record to the database
                    await _invoiceService.CreateAsync(newInvoice);
                    generatedInvoices.Add(newInvoice);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error generating invoice for student ID: {studentId}.");
                    // Continue to process other students even if one fails
                }
            }

            if (!generatedInvoices.Any())
            {
                return StatusCode(500, "No invoices were successfully generated.");
            }

            return Ok(generatedInvoices);
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
