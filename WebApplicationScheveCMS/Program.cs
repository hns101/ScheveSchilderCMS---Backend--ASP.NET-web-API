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

// --- BSON Class Map and Convention Registration ---
ConventionRegistry.Register("CamelCaseConvention", new ConventionPack { new CamelCaseElementNameConvention() }, t => true);

BsonClassMap.RegisterClassMap<Student>(cm =>
{
    cm.AutoMap();
    cm.MapIdProperty(c => c.Id)
      .SetIdGenerator(StringObjectIdGenerator.Instance)
      .SetSerializer(new StringSerializer(BsonType.ObjectId));
});

BsonClassMap.RegisterClassMap<Invoice>(cm =>
{
    cm.AutoMap();
    cm.MapIdProperty(c => c.Id)
      .SetIdGenerator(StringObjectIdGenerator.Instance)
      .SetSerializer(new StringSerializer(BsonType.ObjectId));
    cm.MapProperty(c => c.StudentId)
      .SetSerializer(new StringSerializer(BsonType.ObjectId));
});

BsonClassMap.RegisterClassMap<SystemSettings>(cm =>
{
    cm.AutoMap();
    cm.MapIdProperty(c => c.Id);
});
// --- End BSON Class Map Registration ---

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        builder =>
        {
            builder.WithOrigins("http://localhost", "http://localhost:5173", "http://localhost:3000")
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

// This line registers your database settings
builder.Services.Configure<StudentDatabaseSettings>(
    builder.Configuration.GetSection("StudentDatabaseSettings"));

// Register your services here
builder.Services.AddSingleton<StudentService>();
builder.Services.AddSingleton<InvoiceService>();
builder.Services.AddSingleton<SystemSettingsService>();
builder.Services.AddSingleton<IFileService, FileService>();
builder.Services.AddSingleton<IPdfService, PdfService>();

// Add file upload size limit
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
        Description = "API for managing students and invoices"
    });
});

// Add logging configuration
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Student CMS API V1");
        c.RoutePrefix = "swagger";
    });
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

// Add a health check endpoint
app.MapGet("/health", () => new { Status = "Healthy", Timestamp = DateTime.UtcNow });

// Error handling endpoint
app.Map("/error", () => 
{
    return Results.Problem("An error occurred while processing your request.");
});

app.Run();
