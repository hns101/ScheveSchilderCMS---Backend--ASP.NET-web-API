using WebApplicationScheveCMS.Models;
using WebApplicationScheveCMS.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

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
    // In non-development environments (like Docker), we explicitly disable HTTPS redirection
    // and HSTS to avoid certificate issues. Nginx will handle HTTPS for the frontend.
    app.UseHsts(); // Use HSTS in production, but ensure it's not redirecting to an unconfigured HTTPS port
}

// Removed app.UseHttpsRedirection() to prevent HTTPS binding issues in Docker.
// Nginx in the frontend container will handle HTTPS for external access.
// app.UseHttpsRedirection(); // This line is now removed.

app.UseAuthorization();

app.MapControllers();

app.Run();
