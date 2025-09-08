using Microsoft.AspNetCore.Mvc;
using WebApplicationScheveCMS.Models;
using WebApplicationScheveCMS.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace WebApplicationScheveCMS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PdfLayoutController : ControllerBase
    {
        private readonly IPdfLayoutService _layoutService;
        private readonly IPdfService _pdfService;
        private readonly SystemSettingsService _systemSettingsService;
        private readonly IFileService _fileService;
        private readonly ILogger<PdfLayoutController> _logger;

        public PdfLayoutController(
            IPdfLayoutService layoutService,
            IPdfService pdfService,
            SystemSettingsService systemSettingsService,
            IFileService fileService,
            ILogger<PdfLayoutController> logger)
        {
            _layoutService = layoutService;
            _pdfService = pdfService;
            _systemSettingsService = systemSettingsService;
            _fileService = fileService;
            _logger = logger;
        }

        // GET: api/pdflayout
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PdfLayoutSettings>>> GetLayoutSettings()
        {
            try
            {
                var settings = await _layoutService.GetLayoutSettingsAsync();
                return Ok(ApiResponse<PdfLayoutSettings>.SuccessResult(settings));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving PDF layout settings");
                return StatusCode(500, ApiResponse<PdfLayoutSettings>.ErrorResult("Internal server error"));
            }
        }

        // PUT: api/pdflayout
        [HttpPut]
        public async Task<ActionResult<ApiResponse<PdfLayoutSettings>>> UpdateLayoutSettings([FromBody] PdfLayoutSettings settings)
        {
            try
            {
                if (settings == null)
                {
                    return BadRequest(ApiResponse<PdfLayoutSettings>.ErrorResult("Layout settings cannot be null"));
                }

                var updatedSettings = await _layoutService.UpdateLayoutSettingsAsync(settings);
                _logger.LogInformation("PDF layout settings updated successfully");
                return Ok(ApiResponse<PdfLayoutSettings>.SuccessResult(updatedSettings, "Layout settings updated successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating PDF layout settings");
                return StatusCode(500, ApiResponse<PdfLayoutSettings>.ErrorResult("Internal server error"));
            }
        }

        // PUT: api/pdflayout/element/{elementName}
        [HttpPut("element/{elementName}")]
        public async Task<ActionResult<ApiResponse<PdfLayoutSettings>>> UpdateElementPosition(string elementName, [FromBody] PdfElementPosition position)
        {
            try
            {
                if (string.IsNullOrEmpty(elementName))
                {
                    return BadRequest(ApiResponse<PdfLayoutSettings>.ErrorResult("Element name cannot be null or empty"));
                }

                if (position == null)
                {
                    return BadRequest(ApiResponse<PdfLayoutSettings>.ErrorResult("Position cannot be null"));
                }

                var updatedSettings = await _layoutService.UpdateElementPositionAsync(elementName, position);
                _logger.LogInformation("Updated position for element: {ElementName}", elementName);
                return Ok(ApiResponse<PdfLayoutSettings>.SuccessResult(updatedSettings, $"Updated {elementName} position"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<PdfLayoutSettings>.ErrorResult(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating element position for: {ElementName}", elementName);
                return StatusCode(500, ApiResponse<PdfLayoutSettings>.ErrorResult("Internal server error"));
            }
        }

        // POST: api/pdflayout/reset
        [HttpPost("reset")]
        public async Task<ActionResult<ApiResponse<PdfLayoutSettings>>> ResetToDefault()
        {
            try
            {
                var defaultSettings = await _layoutService.ResetToDefaultAsync();
                _logger.LogInformation("PDF layout settings reset to default");
                return Ok(ApiResponse<PdfLayoutSettings>.SuccessResult(defaultSettings, "Layout settings reset to default"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting PDF layout to default");
                return StatusCode(500, ApiResponse<PdfLayoutSettings>.ErrorResult("Internal server error"));
            }
        }

        // POST: api/pdflayout/preview
        [HttpPost("preview")]
        public async Task<IActionResult> GeneratePreviewWithLayout([FromBody] PdfPreviewRequest? request = null)
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

                // Use provided layout settings or get current ones
                var layoutSettings = request?.LayoutSettings ?? await _layoutService.GetLayoutSettingsAsync();

                var pdfBytes = await _pdfService.GenerateInvoicePdfWithLayoutAsync(
                    dummyStudent, dummyInvoice, defaultTemplatePath, layoutSettings);
                
                var fileName = $"layout_preview_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating layout preview");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/pdflayout/preview (for simple preview without request body)
        [HttpGet("preview")]
        public async Task<IActionResult> GenerateSimplePreview()
        {
            return await GeneratePreviewWithLayout();
        }

        // GET: api/pdflayout/elements
        [HttpGet("elements")]
        public ActionResult<ApiResponse<object>> GetAvailableElements()
        {
            try
            {
                var elements = new
                {
                    StudentName = "Student Name",
                    StudentAddress = "Student Address", 
                    InvoiceId = "Invoice ID",
                    InvoiceDate = "Invoice Date",
                    InvoiceDescription = "Invoice Description",
                    BaseAmount = "Base Amount (excluding VAT)",
                    VatAmount = "VAT Amount",
                    TotalAmount = "Total Amount",
                    PaymentNote = "Payment Note",
                    ContactInfo = "Contact Information"
                };

                return Ok(ApiResponse<object>.SuccessResult(elements, "Available PDF elements"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available elements");
                return StatusCode(500, ApiResponse<object>.ErrorResult("Internal server error"));
            }
        }

        // GET: api/pdflayout/defaults
        [HttpGet("defaults")]
        public ActionResult<ApiResponse<PdfLayoutSettings>> GetDefaultSettings()
        {
            try
            {
                var defaultSettings = new PdfLayoutSettings
                {
                    StudentName = new PdfElementPosition { Top = 150, Left = 400, FontSize = 10 },
                    StudentAddress = new PdfElementPosition { Top = 165, Left = 400, FontSize = 10 },
                    InvoiceId = new PdfElementPosition { Top = 195, Left = 400, FontSize = 10 },
                    InvoiceDate = new PdfElementPosition { Top = 210, Left = 400, FontSize = 10 },
                    InvoiceDescription = new PdfElementPosition { Top = 315, Left = 100, FontSize = 10 },
                    BaseAmount = new PdfElementPosition { Top = 400, Left = 500, FontSize = 10, IsBold = true },
                    VatAmount = new PdfElementPosition { Top = 415, Left = 500, FontSize = 10, IsBold = true },
                    TotalAmount = new PdfElementPosition { Top = 430, Left = 500, FontSize = 10, IsBold = true },
                    PaymentNote = new PdfElementPosition { Top = 500, Left = 100, FontSize = 10 },
                    ContactInfo = new PdfElementPosition { Top = 600, Left = 100, FontSize = 10 }
                };

                return Ok(ApiResponse<PdfLayoutSettings>.SuccessResult(defaultSettings, "Default layout settings"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting default settings");
                return StatusCode(500, ApiResponse<PdfLayoutSettings>.ErrorResult("Internal server error"));
            }
        }

        #region Private Helper Methods

        private static Student CreateDummyStudent()
        {
            return new Student
            {
                Id = "507f1f77bcf86cd799439011", // Dummy ObjectId
                Name = "Voorbeeld Student",
                Address = "Voorbeeldstraat 123, 1234 AB Voorbeeldstad",
                Email = "voorbeeld@student.nl",
                StudentNumber = "STU001",
                BankName = "Voorbeeld Bank",
                AccountNumber = "NL12VOOR0123456789",
                PhoneNumber = "+31 6 12345678",
                EmergencyContact = "Ouders: +31 6 87654321",
                DateOfRegistration = DateTime.Now.AddMonths(-6)
            };
        }

        private static Invoice CreateDummyInvoice()
        {
            return new Invoice
            {
                Id = "507f1f77bcf86cd799439012", // Dummy ObjectId
                Date = DateTime.Now,
                AmountTotal = 125.50M,
                VAT = 21.00M,
                Description = "Voorbeeld factuur beschrijving - Dit is een voorbeeldtekst die toont hoe de factuur eruit ziet met de huidige layout instellingen"
            };
        }

        #endregion
    }
}