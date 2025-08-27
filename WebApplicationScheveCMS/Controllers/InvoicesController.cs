using Microsoft.AspNetCore.Mvc;
using WebApplicationScheveCMS.Models;
using WebApplicationScheveCMS.Services;
using System.IO;

namespace WebApplicationScheveCMS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InvoicesController : ControllerBase
    {
        private readonly InvoiceService _invoiceService;

        public InvoicesController(InvoiceService invoiceService)
        {
            _invoiceService = invoiceService;
        }

        // GET: api/invoices
        // This will be expanded later to include filtering
        [HttpGet]
        public async Task<ActionResult<List<Services.Invoice>>> Get() =>
            await _invoiceService.GetAsync();

        // GET: api/invoices/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Services.Invoice>> Get(string id)
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

            // TODO: Add logic here to delete the associated PDF file from the server's file system

            await _invoiceService.RemoveAsync(id);

            return NoContent();
        }

        // GET: api/invoices/file/{id}
        // This will be implemented later
        [HttpGet("file/{id}")]
        public IActionResult GetFile(string id)
        {
            // TODO: Add logic here to find the file path and return the PDF file
            return NotFound();
        }

        // POST: api/invoices/batch-generate
        // This will be implemented later
        [HttpPost("batch-generate")]
        public async Task<IActionResult> BatchGenerate(BatchInvoiceRequest request)
        {
            // TODO: Add the batch generation logic here
            return Ok();
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