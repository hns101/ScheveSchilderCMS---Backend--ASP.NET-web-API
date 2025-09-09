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
    }

    public class PdfService : IPdfService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<PdfService> _logger;

        public PdfService(IWebHostEnvironment env, ILogger<PdfService> logger)
        {
            _env = env;
            _logger = logger;
            
            // Ensure QuestPDF license is set
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerateInvoicePdf(Student student, Invoice invoice, string templateImagePath)
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
                                    column.Item().PaddingTop(150).PaddingLeft(400).MaxHeight(15)
                                        .AlignLeft().Text(student.Name ?? "").FontSize(10);

                                    // Student Address  
                                    column.Item().PaddingTop(165).PaddingLeft(400).MaxHeight(15)
                                        .AlignLeft().Text(student.Address ?? "").FontSize(10);

                                    // Invoice ID
                                    column.Item().PaddingTop(195).PaddingLeft(400).MaxHeight(15)
                                        .AlignLeft().Text(invoice.Id?.ToString() ?? "").FontSize(10);

                                    // Invoice Date
                                    column.Item().PaddingTop(210).PaddingLeft(400).MaxHeight(15)
                                        .AlignLeft().Text(invoice.Date.ToString("dd-MM-yyyy")).FontSize(10);

                                    // Invoice Description
                                    column.Item().PaddingTop(315).PaddingLeft(100).MaxHeight(15)
                                        .AlignLeft().Text(invoice.Description ?? "").FontSize(10);

                                    // Calculate amounts
                                    var baseAmount = invoice.AmountTotal / (1 + invoice.VAT / 100);
                                    var vatAmount = invoice.AmountTotal - baseAmount;

                                    // Base Amount (Subtotal)
                                    column.Item().PaddingTop(400).PaddingLeft(500).MaxHeight(15)
                                        .AlignLeft().Text($"€{baseAmount:N2}").FontSize(10).SemiBold();

                                    // VAT Amount
                                    column.Item().PaddingTop(415).PaddingLeft(500).MaxHeight(15)
                                        .AlignLeft().Text($"€{vatAmount:N2}").FontSize(10).SemiBold();

                                    // Total Amount
                                    column.Item().PaddingTop(430).PaddingLeft(500).MaxHeight(15)
                                        .AlignLeft().Text($"€{invoice.AmountTotal:N2}").FontSize(10).SemiBold();

                                    // Payment Note
                                    column.Item().PaddingTop(500).PaddingLeft(100).MaxHeight(15)
                                        .AlignLeft().Text("Dit bedrag wordt automatisch geïncasseerd omstreeks 11-10-2024.").FontSize(10);

                                    // Contact Info
                                    column.Item().PaddingTop(600).PaddingLeft(100).MaxHeight(15)
                                        .AlignLeft().Text($"Contact: {student.Email}").FontSize(10);

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
    }
}