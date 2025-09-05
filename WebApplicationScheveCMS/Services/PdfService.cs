using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WebApplicationScheveCMS.Models;
using System.IO;

namespace WebApplicationScheveCMS.Services
{
    public class PdfService
    {
        private readonly IWebHostEnvironment _env;

        public PdfService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public byte[] GenerateInvoicePdf(Student student, Invoice invoice)
        {
            // Set QuestPDF license type to Community (required even for free use)
            QuestPDF.Settings.License = LicenseType.Community;
            
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header()
                        .Row(row =>
                        {
                            row.RelativeColumn().Column(column =>
                            {
                                // Placeholder for logo, to be added later
                                // This section is now empty as requested
                            });

                            row.RelativeColumn().AlignRight().Column(column =>
                            {
                                column.Item().Text($"Factuur: {invoice.Id}").SemiBold().FontSize(18).FontColor(Colors.Blue.Darken2);
                                column.Item().PaddingTop(5).Text($"Aan: {student.Name}");
                                column.Item().Text(student.Address);
                                column.Item().Text($"Datum: {invoice.Date:dd-MM-yyyy}");
                            });
                        });

                    page.Content()
                        .PaddingTop(10)
                        .Column(column =>
                        {
                            column.Spacing(10);
                            column.Item().Text("Omschrijving").SemiBold();
                            column.Item().Text(invoice.Description);

                            column.Item().PaddingTop(20).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn();
                                });
                                
                                table.Cell().Text("Totaal Excl BTW").SemiBold();
                                table.Cell().AlignRight().Text($"{invoice.AmountTotal / (1 + invoice.VAT / 100):C}");

                                table.Cell().Text($"BTW {invoice.VAT}%").SemiBold();
                                table.Cell().AlignRight().Text($"{invoice.AmountTotal - (invoice.AmountTotal / (1 + invoice.VAT / 100)):C}");

                                table.Cell().Text("Totaal Incl BTW").SemiBold();
                                table.Cell().AlignRight().Text($"{invoice.AmountTotal:C}");
                            });

                            column.Item().PaddingTop(20).Text($"Dit bedrag wordt automatisch geïncasseerd omstreeks {invoice.Date:dd-MM-yyyy}.");
                        });

                    page.Footer()
                        .AlignCenter()
                        .PaddingTop(20)
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
