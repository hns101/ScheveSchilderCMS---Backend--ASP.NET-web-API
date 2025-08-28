using WebApplicationScheveCMS.Models;
using WebApplicationScheveCMS.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using MongoDB.Bson.Serialization; // Add this using statement
using MongoDB.Bson.Serialization.IdGenerators; // Add this using statement
using MongoDB.Bson.Serialization.Serializers; // Add this using statement

var builder = WebApplication.CreateBuilder(args);

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        builder =>
        {
            builder.WithOrigins("http://localhost")
                   .AllowAnyHeader()
                   .AllowAnyMethod();
        });
});

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

// --- BSON Class Map Registration ---
// This ensures MongoDB driver correctly maps string IDs to ObjectId
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
    cm.MapProperty(c => c.StudentId) // Explicitly map StudentId as ObjectId string
      .SetSerializer(new StringSerializer(BsonType.ObjectId));
});
// --- End BSON Class Map Registration ---


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
}

app.UseCors("AllowFrontend");

app.UseAuthorization();

app.MapControllers();

app.Run();
