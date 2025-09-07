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
        }

        public byte[] GenerateInvoicePdf(Student student, Invoice invoice, string templateImagePath)
        {
            try
            {
                QuestPDF.Settings.License = LicenseType.Community;

                return Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(0);

                        page.Content().Layers(layers =>
                        {
                            // Background layer - the template image
                            if (File.Exists(templateImagePath))
                            {
                                layers.Layer().Image(templateImagePath);
                            }

                            // Text overlay layer (PRIMARY LAYER - REQUIRED)
                            layers.PrimaryLayer().Column(column =>
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
                            });
                        });
                    });
                }).GeneratePdf();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PDF for student {StudentId}", student.Id);
                throw;
            }
        }
    }
}