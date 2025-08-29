using WebApplicationScheveCMS.Models;
using WebApplicationScheveCMS.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson; // Keep for now, but the convention registration is removed

var builder = WebApplication.CreateBuilder(args);

// --- IMPORTANT: Removed CamelCaseElementNameConvention registration ---
// The CamelCaseElementNameConvention is causing a conflict.
// We will rely on direct PascalCase mapping between BSON and C# models.
// var conventionPack = new ConventionPack { new CamelCaseElementNameConvention() };
// ConventionRegistry.Register("CamelCaseConvention", conventionPack, t => true);
// --- End Removed Convention Registration ---

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        builder =>
        {
            // Allow requests from both the Dockerized Nginx frontend (http://localhost)
            // and your local Vue.js dev server (http://localhost:5173)
            builder.WithOrigins("http://localhost", "http://localhost:5173")
                   .AllowAnyHeader()
                   .AllowAnyMethod();
        });
});


// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options => // Configure JSON serialization to camelCase for API output
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

// --- BSON Class Map Registration ---
// This ensures MongoDB driver correctly maps string IDs to ObjectId
BsonClassMap.RegisterClassMap<Student>(cm =>
{
    cm.AutoMap(); // AutoMap will now use default PascalCase mapping
    cm.MapIdProperty(c => c.Id)
      .SetIdGenerator(StringObjectIdGenerator.Instance)
      .SetSerializer(new StringSerializer(BsonType.ObjectId));
});
BsonClassMap.RegisterClassMap<Invoice>(cm =>
{
    cm.AutoMap(); // AutoMap will now use default PascalCase mapping
    cm.MapIdProperty(c => c.Id)
      .SetIdGenerator(StringObjectIdGenerator.Instance)
      .SetSerializer(new StringSerializer(BsonType.ObjectId));
    cm.MapProperty(c => c.StudentId)
      .SetSerializer(new StringSerializer(BsonType.ObjectId));
});
// --- End BSON Class Map Registration ---


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

app.UseCors("AllowFrontend");

app.UseAuthorization();

app.MapControllers();

app.Run();
