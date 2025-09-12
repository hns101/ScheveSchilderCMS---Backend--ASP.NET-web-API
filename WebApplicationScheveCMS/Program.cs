using WebApplicationScheveCMS.Models;
using WebApplicationScheveCMS.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Bson;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using QuestPDF.Infrastructure;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using WebApplicationScheveCMS.Middleware;
using WebApplicationScheveCMS.Filters;

var builder = WebApplication.CreateBuilder(args);

// Set QuestPDF license early
QuestPDF.Settings.License = LicenseType.Community;

// --- BSON Class Map and Convention Registration ---
ConventionRegistry.Register("CamelCaseConvention", new ConventionPack { new CamelCaseElementNameConvention() }, t => true);

// Register BSON class maps only once
if (!BsonClassMap.IsClassMapRegistered(typeof(Student)))
{
    BsonClassMap.RegisterClassMap<Student>(cm =>
    {
        cm.AutoMap();
        cm.MapIdProperty(c => c.Id)
          .SetIdGenerator(StringObjectIdGenerator.Instance)
          .SetSerializer(new StringSerializer(BsonType.ObjectId));
    });
}

if (!BsonClassMap.IsClassMapRegistered(typeof(Invoice)))
{
    BsonClassMap.RegisterClassMap<Invoice>(cm =>
    {
        cm.AutoMap();
        cm.MapIdProperty(c => c.Id)
          .SetIdGenerator(StringObjectIdGenerator.Instance)
          .SetSerializer(new StringSerializer(BsonType.ObjectId));
        cm.MapProperty(c => c.StudentId)
          .SetSerializer(new StringSerializer(BsonType.ObjectId));
    });
}

if (!BsonClassMap.IsClassMapRegistered(typeof(SystemSettings)))
{
    BsonClassMap.RegisterClassMap<SystemSettings>(cm =>
    {
        cm.AutoMap();
        cm.MapIdProperty(c => c.Id)
          .SetSerializer(new StringSerializer(BsonType.String));
    });
}


if (!BsonClassMap.IsClassMapRegistered(typeof(PdfLayoutSettings)))
{
    BsonClassMap.RegisterClassMap<PdfLayoutSettings>(cm =>
    {
        cm.AutoMap();
        cm.MapIdProperty(c => c.Id)
          .SetSerializer(new StringSerializer(BsonType.String));
    });
}
// --- End BSON Class Map Registration ---

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost", "http://localhost:5173", "http://localhost:3000")
                   .AllowAnyHeader()
                   .AllowAnyMethod()
                   .AllowCredentials();
        });
});

// Add services to the container.
builder.Services.AddControllers(options =>
{
    // Configure model validation
    options.Filters.Add<ValidationFilter>();
})
.AddJsonOptions(options => // Configure JSON serialization to camelCase for API output
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.WriteIndented = true;
});

// Configure model validation to return consistent error responses
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .SelectMany(x => x.Value!.Errors)
            .Select(x => x.ErrorMessage)
            .ToList();

        var response = ApiResponse<object>.ErrorResult("Validation failed", errors);
        return new BadRequestObjectResult(response);
    };
});

// Register database settings
builder.Services.Configure<StudentDatabaseSettings>(
    builder.Configuration.GetSection("StudentDatabaseSettings"));

// Register services with proper lifetimes
builder.Services.AddSingleton<StudentService>();
builder.Services.AddSingleton<InvoiceService>();
builder.Services.AddSingleton<SystemSettingsService>();
builder.Services.AddSingleton<IPdfLayoutService, PdfLayoutService>();
builder.Services.AddSingleton<IFileService, FileService>();
builder.Services.AddSingleton<IPdfService, PdfService>();

// Add file upload size limit
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
});

// Configure Kestrel for larger request bodies
builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
});

// Add Swagger/OpenAPI services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "De Scheve Schilder CMS API",
        Version = "v1",
        Description = "API for managing students and invoices with PDF generation"
    });
});

// Add logging configuration
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var app = builder.Build();

// Create necessary directories on startup
try
{
    var env = app.Services.GetRequiredService<IWebHostEnvironment>();
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    var settings = app.Configuration.GetSection("StudentDatabaseSettings").Get<StudentDatabaseSettings>();
    
    if (settings != null)
    {
        // Create Files directory structure based on settings
        var basePath = Path.IsPathRooted(settings.FileStoragePath) 
            ? settings.FileStoragePath 
            : Path.Combine(env.ContentRootPath, settings.FileStoragePath);
            
        var invoicesDir = Path.Combine(basePath, "Invoices");
        var studentDocsDir = Path.Combine(basePath, "StudentDocuments");
        var templatesDir = Path.Combine(basePath, "Templates");

        Directory.CreateDirectory(basePath);
        Directory.CreateDirectory(invoicesDir);
        Directory.CreateDirectory(studentDocsDir);
        Directory.CreateDirectory(templatesDir);
        
        logger.LogInformation("Created necessary directories: {BasePath}, {InvoicesDir}, {StudentDocsDir}, {TemplatesDir}",
            basePath, invoicesDir, studentDocsDir, templatesDir);
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Failed to create directories on startup");
}

// Test services are properly registered
try
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    
    // Test service registrations
    var pdfService = services.GetRequiredService<IPdfService>();
    var layoutService = services.GetRequiredService<IPdfLayoutService>();
    var fileService = services.GetRequiredService<IFileService>();
    
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("All services registered successfully - PDF: {PdfService}, Layout: {LayoutService}, File: {FileService}",
        pdfService.GetType().Name, layoutService.GetType().Name, fileService.GetType().Name);
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Service registration validation failed");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "De Scheve Schilder CMS API V1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "De Scheve Schilder CMS API Documentation";
    });
    
    // Enable detailed error pages in development
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseHsts();
    // Add global exception handler for production
    app.UseExceptionHandler("/error");
}

// Add global exception handling
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseCors("AllowFrontend");
app.UseStaticFiles();

app.MapControllers();

// Add a health check endpoint with service validation
app.MapGet("/health", (IServiceProvider services) =>
{
    try
    {
        // Test critical services
        var pdfService = services.GetRequiredService<IPdfService>();
        var layoutService = services.GetRequiredService<IPdfLayoutService>();
        var fileService = services.GetRequiredService<IFileService>();
        
        return Results.Ok(new 
        { 
            Status = "Healthy", 
            Timestamp = DateTime.UtcNow,
            Services = new 
            {
                PdfService = pdfService.GetType().Name,
                LayoutService = layoutService.GetType().Name,
                FileService = fileService.GetType().Name
            }
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Service Health Check Failed",
            detail: ex.Message,
            statusCode: 500
        );
    }
});

// Error handling endpoint
app.Map("/error", () => 
{
    return Results.Problem("An error occurred while processing your request.");
});

app.Run();