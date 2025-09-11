using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WebApplicationScheveCMS.Models;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using QuestPDF;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

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
        private readonly IServiceProvider _serviceProvider;

        public PdfService(IWebHostEnvironment env, ILogger<PdfService> logger, IServiceProvider serviceProvider)
        {
            _env = env;
            _logger = logger;
            _serviceProvider = serviceProvider;
            
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
                ValidateInputs(student, invoice, templateImagePath);

                // Get layout settings if not provided
                layoutSettings ??= await GetLayoutSettingsAsync();

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
                                if (File.Exists(templateImagePath))
                                {
                                    layers.Layer().Image(templateImagePath);
                                    _logger.LogDebug("Template image loaded successfully");
                                }
                                else
                                {
                                    _logger.LogWarning("Template image not found at: {TemplatePath}", templateImagePath);
                                }
                            }
                            catch (Exception imageEx)
                            {
                                _logger.LogWarning(imageEx, "Failed to load template image, continuing without background");
                            }

                            // Text overlay layer (PRIMARY LAYER - REQUIRED)
                            layers.PrimaryLayer().Column(column =>
                            {
                                try
                                {
                                    AddAllTextElements(column, student, invoice, layoutSettings);
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

        private void ValidateInputs(Student student, Invoice invoice, string templateImagePath)
        {
            if (student == null)
                throw new ArgumentNullException(nameof(student), "Student cannot be null");

            if (invoice == null)
                throw new ArgumentNullException(nameof(invoice), "Invoice cannot be null");

            if (string.IsNullOrEmpty(templateImagePath))
                throw new ArgumentException("Template image path cannot be null or empty", nameof(templateImagePath));

            if (!File.Exists(templateImagePath))
                throw new FileNotFoundException($"Template image not found at path: {templateImagePath}");
        }

        private async Task<PdfLayoutSettings> GetLayoutSettingsAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var layoutService = scope.ServiceProvider.GetService<IPdfLayoutService>();
                
                if (layoutService != null)
                {
                    return await layoutService.GetLayoutSettingsAsync();
                }
                else
                {
                    _logger.LogWarning("PdfLayoutService not available, using default layout settings");
                    return CreateDefaultLayoutSettings();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting layout settings, using defaults");
                return CreateDefaultLayoutSettings();
            }
        }

        private void AddAllTextElements(ColumnDescriptor column, Student student, Invoice invoice, PdfLayoutSettings layoutSettings)
        {
            // Student Information
            AddTextElement(column, layoutSettings.StudentName, student.Name ?? "");
            AddTextElement(column, layoutSettings.StudentAddress, student.Address ?? "");

            // Invoice Information  
            AddTextElement(column, layoutSettings.InvoiceId, FormatInvoiceId(invoice.Id));
            AddTextElement(column, layoutSettings.InvoiceDate, invoice.Date.ToString("dd-MM-yyyy"));
            AddTextElement(column, layoutSettings.InvoiceDescription, invoice.Description ?? "");

            // Financial Information
            var (baseAmount, vatAmount) = CalculateAmounts(invoice.AmountTotal, invoice.VAT);
            AddTextElement(column, layoutSettings.BaseAmount, FormatCurrency(baseAmount));
            AddTextElement(column, layoutSettings.VatAmount, FormatCurrency(vatAmount));
            AddTextElement(column, layoutSettings.TotalAmount, FormatCurrency(invoice.AmountTotal));

            // Additional Information
            AddTextElement(column, layoutSettings.PaymentNote, GetPaymentNote());
            AddTextElement(column, layoutSettings.ContactInfo, FormatContactInfo(student));
        }

        private static void AddTextElement(ColumnDescriptor column, PdfElementPosition position, string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            try
            {
                var item = column.Item()
                    .PaddingTop(position.Top)
                    .PaddingLeft(position.Left)
                    .MaxHeight(position.MaxHeight);

                var textElement = position.TextAlign.ToLowerInvariant() switch
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
            catch (Exception ex)
            {
                // Log but don't fail the entire PDF generation for one element
                System.Diagnostics.Debug.WriteLine($"Error adding text element: {ex.Message}");
            }
        }

        // Helper methods
        private static string FormatInvoiceId(string? invoiceId)
        {
            if (string.IsNullOrEmpty(invoiceId)) return "N/A";
            return invoiceId.Length > 8 ? invoiceId[..8] + "..." : invoiceId;
        }

        private static (decimal baseAmount, decimal vatAmount) CalculateAmounts(decimal totalAmount, decimal vatPercentage)
        {
            var baseAmount = totalAmount / (1 + vatPercentage / 100);
            var vatAmount = totalAmount - baseAmount;
            return (baseAmount, vatAmount);
        }

        private static string FormatCurrency(decimal amount)
        {
            return $"€{amount:N2}";
        }

        private static string GetPaymentNote()
        {
            return $"Dit bedrag wordt automatisch geïncasseerd omstreeks {DateTime.Now.AddDays(30):dd-MM-yyyy}.";
        }

        private static string FormatContactInfo(Student student)
        {
            return $"Contact: {student.Email ?? "N/A"}";
        }

        private static PdfLayoutSettings CreateDefaultLayoutSettings()
        {
            return new PdfLayoutSettings
            {
                StudentName = new PdfElementPosition { Top = 150, Left = 400, FontSize = 10, TextAlign = "Left", IsBold = false, MaxHeight = 20 },
                StudentAddress = new PdfElementPosition { Top = 170, Left = 400, FontSize = 9, TextAlign = "Left", IsBold = false, MaxHeight = 40 },
                InvoiceId = new PdfElementPosition { Top = 220, Left = 400, FontSize = 10, TextAlign = "Left", IsBold = false, MaxHeight = 15 },
                InvoiceDate = new PdfElementPosition { Top = 240, Left = 400, FontSize = 10, TextAlign = "Left", IsBold = false, MaxHeight = 15 },
                InvoiceDescription = new PdfElementPosition { Top = 340, Left = 100, FontSize = 10, TextAlign = "Left", IsBold = false, MaxHeight = 60 },
                BaseAmount = new PdfElementPosition { Top = 450, Left = 450, FontSize = 10, TextAlign = "Right", IsBold = false, MaxHeight = 15 },
                VatAmount = new PdfElementPosition { Top = 470, Left = 450, FontSize = 10, TextAlign = "Right", IsBold = false, MaxHeight = 15 },
                TotalAmount = new PdfElementPosition { Top = 490, Left = 450, FontSize = 12, TextAlign = "Right", IsBold = true, MaxHeight = 15 },
                PaymentNote = new PdfElementPosition { Top = 550, Left = 100, FontSize = 9, TextAlign = "Left", IsBold = false, MaxHeight = 30 },
                ContactInfo = new PdfElementPosition { Top = 650, Left = 100, FontSize = 9, TextAlign = "Left", IsBold = false, MaxHeight = 15 },
                LastUpdated = DateTime.UtcNow,
                UpdatedBy = "System"
            };
        }
    }
}