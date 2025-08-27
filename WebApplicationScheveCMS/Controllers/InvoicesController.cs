using Microsoft.AspNetCore.Mvc;
using WebApplicationScheveCMS.Models;
using WebApplicationScheveCMS.Services;
using System.Collections.Generic;
using System.IO;

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

        public InvoicesController(
            InvoiceService invoiceService, 
            StudentService studentService, 
            PdfService pdfService, 
            FileService fileService)
        {
            _invoiceService = invoiceService;
            _studentService = studentService;
            _pdfService = pdfService;
            _fileService = fileService;
        }

        // GET: api/invoices
        // This will be expanded later to include filtering
        [HttpGet]
        public async Task<ActionResult<List<Invoice>>> Get() =>
            await _invoiceService.GetAsync();

        // GET: api/invoices/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Invoice>> Get(string id)
        {
            var invoice = await _invoiceService.GetAsync(id);

            if (invoice is null)
            {
                return NotFound();
            }

            return invoice;
        }
        
        // DELETE: api/invoices/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var invoice = await _invoiceService.GetAsync(id);

            if (invoice is null)
            {
                return NotFound();
            }

            // Delete the associated PDF file
            if (!string.IsNullOrEmpty(invoice.InvoicePdfPath))
            {
                _fileService.DeleteFile(invoice.InvoicePdfPath);
            }

            await _invoiceService.RemoveAsync(id);

            return NoContent();
        }

        // GET: api/invoices/file/{id}
        [HttpGet("file/{id}")]
        public async Task<IActionResult> GetFile(string id)
        {
            var invoice = await _invoiceService.GetAsync(id);
            if (invoice == null || string.IsNullOrEmpty(invoice.InvoicePdfPath))
            {
                return NotFound();
            }

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Invoices", Path.GetFileName(invoice.InvoicePdfPath));
            
            if (System.IO.File.Exists(filePath))
            {
                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                return File(fileBytes, "application/pdf", Path.GetFileName(filePath));
            }

            return NotFound();
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
                var student = await _studentService.GetAsync(studentId);

                if (student is null)
                {
                    // Optionally log or handle a student not found case
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
                var fileName = $"Factuur_{newInvoice.Id}.pdf";

                // Save the PDF and get the file path
                newInvoice.InvoicePdfPath = _fileService.SaveInvoicePdf(fileName, pdfBytes);

                // Save the invoice to the database
                await _invoiceService.CreateAsync(newInvoice);
                generatedInvoices.Add(newInvoice);
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
