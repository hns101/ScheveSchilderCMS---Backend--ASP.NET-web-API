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

// CRITICAL: Set QuestPDF license FIRST before any other operations
try
{
    QuestPDF.Settings.License = LicenseType.Community;
    Console.WriteLine("✅ QuestPDF License set successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error setting QuestPDF license: {ex.Message}");
}

// --- BSON Class Map and Convention Registration ---
try
{
    // Register Student class map
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

    // Register Invoice class map
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

    // Register SystemSettings class map
    if (!BsonClassMap.IsClassMapRegistered(typeof(SystemSettings)))
    {
        BsonClassMap.RegisterClassMap<SystemSettings>(cm =>
        {
            cm.AutoMap();
            cm.MapIdProperty(c => c.Id);
        });
    }

    // Register PdfLayoutSettings class map
    if (!BsonClassMap.IsClassMapRegistered(typeof(PdfLayoutSettings)))
    {
        BsonClassMap.RegisterClassMap<PdfLayoutSettings>(cm =>
        {
            cm.AutoMap();
            cm.MapIdProperty(c => c.Id)
              .SetIdGenerator(StringObjectIdGenerator.Instance)
              .SetSerializer(new StringSerializer(BsonType.ObjectId));
        });
    }

    // Register camel case convention for all types
    ConventionRegistry.Register("CamelCaseConvention", 
        new ConventionPack { new CamelCaseElementNameConvention() }, 
        t => true);
    
    Console.WriteLine("✅ MongoDB BSON configuration completed");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error configuring MongoDB BSON: {ex.Message}");
}

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost", "http://localhost:5173", "http://localhost:3000", "http://localhost:5225")
                   .AllowAnyHeader()
                   .AllowAnyMethod()
                   .AllowCredentials();
        });
});

// Add services to the container.
builder.Services.AddControllers(options =>
{
    // Add validation filter
    options.Filters.Add<ValidationFilter>();
})
.AddJsonOptions(options =>
{
    // Configure JSON serialization to camelCase for API output
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.WriteIndented = true;
    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
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

// Register database settings from appsettings.json
builder.Services.Configure<StudentDatabaseSettings>(
    builder.Configuration.GetSection("StudentDatabaseSettings"));

// Register application services
builder.Services.AddSingleton<StudentService>();
builder.Services.AddSingleton<InvoiceService>();
builder.Services.AddSingleton<SystemSettingsService>();
builder.Services.AddSingleton<IFileService, FileService>();
builder.Services.AddSingleton<IPdfService, PdfService>();
builder.Services.AddSingleton<IPdfLayoutService, PdfLayoutService>(); // NEW: PDF Layout Service

// Configure file upload limits
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
});

// Add Swagger/OpenAPI services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Student CMS API",
        Version = "v1",
        Description = "API for managing students, invoices, and PDF layouts"
    });
});

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var app = builder.Build();

// Create persistent directories on startup
try
{
    Console.WriteLine("🔧 Setting up persistent storage directories...");
    
    // Get persistent storage path
    var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    var persistentBasePath = Path.Combine(appDataPath, "ScheveCMS", "Data");
    
    var directories = new[]
    {
        Path.Combine(persistentBasePath, "Invoices"),
        Path.Combine(persistentBasePath, "StudentDocuments"), 
        Path.Combine(persistentBasePath, "Templates")
    };

    foreach (var dir in directories)
    {
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            Console.WriteLine($"✅ Created persistent directory: {dir}");
        }
        else
        {
            Console.WriteLine($"✅ Persistent directory exists: {dir}");
        }
    }
    
    Console.WriteLine($"📁 Persistent storage base path: {persistentBasePath}");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error creating persistent directories: {ex.Message}");
    
    // Fallback to local directories if persistent storage fails
    try
    {
        Console.WriteLine("🔄 Attempting fallback to local directories...");
        var env = app.Services.GetRequiredService<IWebHostEnvironment>();
        var fallbackDirectories = new[]
        {
            Path.Combine(env.ContentRootPath, "Files"),
            Path.Combine(env.ContentRootPath, "Files", "Invoices"),
            Path.Combine(env.ContentRootPath, "Files", "StudentDocuments"),
            Path.Combine(env.WebRootPath ?? env.ContentRootPath, "templates")
        };

        foreach (var dir in fallbackDirectories)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                Console.WriteLine($"✅ Created fallback directory: {dir}");
            }
        }
    }
    catch (Exception fallbackEx)
    {
        Console.WriteLine($"❌ Error creating fallback directories: {fallbackEx.Message}");
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    Console.WriteLine("🔧 Development environment detected - enabling detailed error pages");
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Student CMS API V1");
        c.RoutePrefix = "swagger";
    });
    app.UseDeveloperExceptionPage();
}
else
{
    Console.WriteLine("🔧 Production environment detected - using production error handling");
    app.UseHsts();
    app.UseExceptionHandler("/error");
}

// Add middleware in correct order
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors("AllowFrontend");
app.UseStaticFiles();

// Map controllers
app.MapControllers();

// Add health check endpoint with detailed information
app.MapGet("/health", () => 
{
    var persistentPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ScheveCMS", "Data");
    
    return new { 
        Status = "Healthy", 
        Timestamp = DateTime.UtcNow, 
        Environment = app.Environment.EnvironmentName,
        QuestPDFLicense = QuestPDF.Settings.License.ToString(),
        PersistentStoragePath = persistentPath,
        PersistentStorageExists = Directory.Exists(persistentPath),
        Version = "1.0.0",
        Features = new[] 
        { 
            "Students Management", 
            "Invoice Generation", 
            "PDF Layout Configuration", 
            "Template Management",
            "Persistent File Storage"
        }
    };
});

// Error handling endpoint for production
app.Map("/error", () => 
{
    return Results.Problem("An error occurred while processing your request.");
});

// Log startup information
Console.WriteLine("🚀 Application starting...");
Console.WriteLine($"📋 Environment: {app.Environment.EnvironmentName}");
Console.WriteLine($"📄 QuestPDF License: {QuestPDF.Settings.License}");
Console.WriteLine($"💾 Persistent storage: {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ScheveCMS", "Data")}");
Console.WriteLine($"🌐 CORS enabled for frontend origins");
Console.WriteLine($"📊 Swagger UI available at: /swagger (in development)");
Console.WriteLine($"❤️ Health check available at: /health");

// Start the application
app.Run();