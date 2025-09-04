using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WebApplicationScheveCMS.Models;

namespace WebApplicationScheveCMS.Services
{
    public class PdfService
    {
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
                                column.Item().PaddingBottom(10).Image("Files/Logo/scheve-schilder-logo.png", ImageScaling.FitWidth);
                                column.Item().Text("Schilderschool De Scheve Schilder").FontSize(12).SemiBold();
                                column.Item().Text("Weereweg 120");
                                column.Item().Text("1732 LN Lutjewinkel");
                                column.Item().Text($"KVK: 89240626");
                                column.Item().Text($"BTW: NL004707541858");
                                column.Item().Text($"Bank: NL 03 RABO 0338 9173 22");
                                column.Item().Text($"Info@scheveschilder.nl");
                                column.Item().Text($"06-10910012");
                                column.Item().Text($"www.scheveschilder.nl");
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

                            // The summary table
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

                            // Automatic payment note
                            column.Item().PaddingTop(20).Text("Dit bedrag wordt automatisch geïncasseerd omstreeks 11-10-2024.");
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
