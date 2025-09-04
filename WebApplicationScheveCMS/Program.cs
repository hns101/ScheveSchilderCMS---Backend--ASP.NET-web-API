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
using Microsoft.AspNetCore.Hosting; // Added this using statement

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
// --- End BSON Class Map Registration ---

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        builder =>
        {
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

// Add Swagger/OpenAPI services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseCors("AllowFrontend");
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

app.Run();
