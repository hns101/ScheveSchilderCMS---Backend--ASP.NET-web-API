using WebApplicationScheveCMS.Models;
using WebApplicationScheveCMS.Services;
using Microsoft.Extensions.Logging; // Add this using statement
using System.Text.Json; // Add this for JsonNamingPolicy

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options => // Configure JSON serialization to camelCase
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

// This line registers your database settings
builder.Services.Configure<StudentDatabaseSettings>(
    builder.Configuration.GetSection("StudentDatabaseSettings"));

// Register your services here as Singletons
builder.Services.AddSingleton<StudentService>();
builder.Services.AddSingleton<InvoiceService>();
builder.Services.AddSingleton<FileService>();
builder.Services.AddSingleton<PdfService>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
}

// Removed app.UseHttpsRedirection() to prevent HTTPS binding issues in Docker.
app.UseAuthorization();

app.MapControllers();

app.Run();
