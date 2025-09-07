using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WebApplicationScheveCMS.Models;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using System.Drawing;
using System.Drawing.Imaging;

namespace WebApplicationScheveCMS.Services
{
    public class PdfService
    {
        private readonly IWebHostEnvironment _env;

        public PdfService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public byte[] GenerateInvoicePdf(Student student, Invoice invoice, string templatePath)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            
            return Document.Create(container =>
            {
                // Load the uploaded PDF template from a file and render it to an image
                using (var pdfDocument = PdfiumViewer.PdfDocument.Load(templatePath))
                {
                    var page = pdfDocument.Render(0, 300, 300, false);
                    using (var stream = new MemoryStream())
                    {
                        page.Save(stream, ImageFormat.Png);
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4);
                            page.Margin(0);
                            page.PageColor(Colors.White);
                            page.DefaultTextStyle(x => x.FontSize(10));

                            page.Canvas(canvas =>
                            {
                                // Draw the PDF page as a background image
                                canvas.DrawImage(stream.ToArray(), PageSizes.A4.Width, PageSizes.A4.Height);

                                // Dynamic data placement
                                var textStyle = TextStyle.Default.FontSize(12).FontColor(Colors.Black);
                                var dateStyle = TextStyle.Default.FontSize(12).FontColor(Colors.Black);

                                // Place student data
                                canvas.Translate(420, 150)
                                    .Text($"Aan: {student.Name}")
                                    .Style(textStyle);
                                canvas.Translate(420, 165)
                                    .Text(student.Address)
                                    .Style(textStyle);
                                
                                // Place invoice data
                                canvas.Translate(420, 195)
                                    .Text($"Datum: {invoice.Date:dd-MM-yyyy}")
                                    .Style(dateStyle);
                                canvas.Translate(100, 250)
                                    .Text(invoice.Description)
                                    .Style(textStyle);

                                // Place invoice totals
                                canvas.Translate(450, 400)
                                    .Text($"{invoice.AmountTotal / (1 + invoice.VAT / 100):C}")
                                    .Style(textStyle);
                                canvas.Translate(450, 420)
                                    .Text($"{invoice.AmountTotal - (invoice.AmountTotal / (1 + invoice.VAT / 100)):C}")
                                    .Style(textStyle);
                                canvas.Translate(450, 440)
                                    .Text($"{invoice.AmountTotal:C}")
                                    .Style(textStyle);

                            });
                        });
                    }
                }
            }).GeneratePdf();
        }
    }
}
