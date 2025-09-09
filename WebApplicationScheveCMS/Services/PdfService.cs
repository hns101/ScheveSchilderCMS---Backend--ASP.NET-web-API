using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WebApplicationScheveCMS.Models;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using QuestPDF;
using Microsoft.Extensions.Logging;

namespace WebApplicationScheveCMS.Services
{
    public interface IPdfService
    {
        byte[] GenerateInvoicePdf(Student student, Invoice invoice, string templateImagePath);
        Task<byte[]> GenerateInvoicePdfWithLayoutAsync(Student student, Invoice invoice, string templateImagePath, PdfLayoutSettings? layoutSettings = null);
    }

    public class PdfService : IPdfService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<PdfService> _logger;
        private readonly IPdfLayoutService _layoutService;

        public PdfService(IWebHostEnvironment env, ILogger<PdfService> logger, IPdfLayoutService layoutService)
        {
            _env = env;
            _logger = logger;
            _layoutService = layoutService;
            
            // Ensure QuestPDF license is set
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerateInvoicePdf(Student student, Invoice invoice, string templateImagePath)
        {
            // Use the async version with default layout
            return GenerateInvoicePdfWithLayoutAsync(student, invoice, templateImagePath).Result;
        }

        public async Task<byte[]> GenerateInvoicePdfWithLayoutAsync(Student student, Invoice invoice, string templateImagePath, PdfLayoutSettings? layoutSettings = null)
        {
            try
            {
                _logger.LogInformation("Starting PDF generation for student: {StudentName} ({StudentId})", 
                    student.Name, student.Id);

                // Validate inputs
                if (student == null)
                {
                    throw new ArgumentNullException(nameof(student), "Student cannot be null");
                }

                if (invoice == null)
                {
                    throw new ArgumentNullException(nameof(invoice), "Invoice cannot be null");
                }

                if (string.IsNullOrEmpty(templateImagePath))
                {
                    throw new ArgumentException("Template image path cannot be null or empty", nameof(templateImagePath));
                }

                if (!File.Exists(templateImagePath))
                {
                    throw new FileNotFoundException($"Template image not found at path: {templateImagePath}");
                }

                // Get layout settings if not provided
                if (layoutSettings == null)
                {
                    layoutSettings = await _layoutService.GetLayoutSettingsAsync();
                }

                _logger.LogDebug("Template image found at: {TemplatePath}", templateImagePath);

                // Ensure QuestPDF license is set (defensive programming)
                QuestPDF.Settings.License = LicenseType.Community;

                var pdfBytes = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(0);

                        page.Content().Layers(layers =>
                        {
                            // Background layer - the template image
                            try
                            {
                                layers.Layer().Image(templateImagePath);
                                _logger.LogDebug("Template image loaded successfully");
                            }
                            catch (Exception imageEx)
                            {
                                _logger.LogWarning(imageEx, "Failed to load template image, continuing without background");
                                // Continue without background image
                            }

                            // Text overlay layer (PRIMARY LAYER - REQUIRED)
                            layers.PrimaryLayer().Column(column =>
                            {
                                try
                                {
                                    // Student Name
                                    AddTextElement(column, layoutSettings.StudentName, student.Name ?? "");

                                    // Student Address  
                                    AddTextElement(column, layoutSettings.StudentAddress, student.Address ?? "");

                                    // Invoice ID
                                    AddTextElement(column, layoutSettings.InvoiceId, invoice.Id?.ToString() ?? "");

                                    // Invoice Date
                                    AddTextElement(column, layoutSettings.InvoiceDate, invoice.Date.ToString("dd-MM-yyyy"));

                                    // Invoice Description
                                    AddTextElement(column, layoutSettings.InvoiceDescription, invoice.Description ?? "");

                                    // Calculate amounts
                                    var baseAmount = invoice.AmountTotal / (1 + invoice.VAT / 100);
                                    var vatAmount = invoice.AmountTotal - baseAmount;

                                    // Base Amount (Subtotal)
                                    AddTextElement(column, layoutSettings.BaseAmount, $"€{baseAmount:N2}");

                                    // VAT Amount
                                    AddTextElement(column, layoutSettings.VatAmount, $"€{vatAmount:N2}");

                                    // Total Amount
                                    AddTextElement(column, layoutSettings.TotalAmount, $"€{invoice.AmountTotal:N2}");

                                    // Payment Note
                                    AddTextElement(column, layoutSettings.PaymentNote, "Dit bedrag wordt automatisch geïncasseerd omstreeks 11-10-2024.");

                                    // Contact Info
                                    AddTextElement(column, layoutSettings.ContactInfo, $"Contact: {student.Email}");

                                    _logger.LogDebug("All text elements added successfully");
                                }
                                catch (Exception textEx)
                                {
                                    _logger.LogError(textEx, "Error adding text elements to PDF");
                                    throw;
                                }
                            });
                        });
                    });
                }).GeneratePdf();

                _logger.LogInformation("PDF generated successfully for student: {StudentName}, size: {PdfSize} bytes", 
                    student.Name, pdfBytes.Length);

                return pdfBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PDF for student {StudentId}. Template: {TemplatePath}", 
                    student?.Id, templateImagePath);
                throw new InvalidOperationException($"Failed to generate PDF for student {student?.Name}: {ex.Message}", ex);
            }
        }

        private static void AddTextElement(ColumnDescriptor column, PdfElementPosition position, string text)
        {
            var item = column.Item()
                .PaddingTop(position.Top)
                .PaddingLeft(position.Left)
                .MaxHeight(position.MaxHeight);

            var textElement = position.TextAlign.ToLower() switch
            {
                "center" => item.AlignCenter(),
                "right" => item.AlignRight(),
                _ => item.AlignLeft()
            };

            var textDescriptor = textElement.Text(text).FontSize(position.FontSize);

            if (position.IsBold)
            {
                textDescriptor.SemiBold();
            }
        }
    }
}