using Microsoft.EntityFrameworkCore;
using UrlShortener.Application.Contracts;
using UrlShortener.Application.Services;
using UrlShortener.Infrastructure.Persistence;
using UrlShortener.Infrastructure.Repositories;
using UrlShortener.Api.Middleware;
using UrlShortener.Orchestration.Agents;
using UrlShortener.Orchestration.Services;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<UrlShortenerDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=urlshortener.db"));

// Application services
builder.Services.AddScoped<IUrlShortenerService, UrlShortenerService>();

// Infrastructure services
builder.Services.AddScoped<IUrlMappingRepository, UrlMappingRepository>();

// Orchestration services
builder.Services.AddScoped<IAgentProvider, DemoAgentProvider>();
builder.Services.AddScoped<IWorkflowPlanner, WorkflowPlanner>();
builder.Services.AddScoped<WorkflowOrchestrator>();
builder.Services.AddSingleton<IWorkflowStore, InMemoryWorkflowStore>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddOpenApi();

var app = builder.Build();

// Create the database automatically for the prototype.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider
        .GetRequiredService<UrlShortenerDbContext>();

    await dbContext.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseMiddleware<GlobalExceptionMiddleware>();

app.MapControllers();

app.Run();
public partial class Program
{
}