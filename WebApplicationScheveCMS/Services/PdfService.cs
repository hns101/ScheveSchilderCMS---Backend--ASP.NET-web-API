using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WebApplicationScheveCMS.Models;

namespace WebApplicationScheveCMS.Services
{
    public class PdfService
    {
        // This is a placeholder for your PDF generation logic
        public byte[] GenerateInvoicePdf(Student student, Invoice invoice)
        {
            // You will customize this with your company info and invoice details
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header()
                        .Text($"Factuur: {invoice.Id}")
                        .SemiBold().FontSize(18).FontColor(Colors.Blue.Darken2);

                    page.Content()
                        .PaddingVertical(10)
                        .Column(column =>
                        {
                            column.Spacing(5);
                            column.Item().Text($"Aan: {student.Name}");
                            column.Item().Text($"Adres: {student.Address}");
                            column.Item().Text($"Datum: {invoice.Date:dd/MM/yyyy}");
                            column.Item().Text($"Beschrijving: {invoice.Description}");
                            column.Item().Text($"Totaalbedrag: {invoice.AmountTotal:C}");
                            column.Item().Text($"BTW: {invoice.VAT:C}");
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Contact: ").SemiBold();
                            x.Span(student.Email);
                        });
                });
            }).GeneratePdf();
        }
    }
}
