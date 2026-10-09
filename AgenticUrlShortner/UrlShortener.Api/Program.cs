using Microsoft.EntityFrameworkCore;
using UrlShortener.Application.Contracts;
using UrlShortener.Application.Services;
using UrlShortener.Infrastructure.Persistence;
using UrlShortener.Infrastructure.Repositories;
using UrlShortener.Api.Middleware;
using System.Net.Http.Headers;
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

// Agent provider priority: Groq (cloud, free) > Ollama (local, free) > Demo (no LLM)
var groqApiKey    = builder.Configuration["Groq:ApiKey"] ?? string.Empty;
var groqBaseUrl   = builder.Configuration["Groq:BaseUrl"] ?? "https://api.groq.com";
var groqModel     = builder.Configuration["Groq:Model"] ?? "llama3-8b-8192";

var ollamaEnabled = builder.Configuration.GetValue<bool>("Ollama:Enabled");
var ollamaBaseUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
var ollamaModel   = builder.Configuration["Ollama:Model"] ?? "llama3";

if (!string.IsNullOrWhiteSpace(groqApiKey))
{
    // Groq: free cloud LLM — sign up at console.groq.com (no credit card required)
    builder.Services.AddHttpClient<IAgentProvider, GroqAgentProvider>(client =>
    {
        client.BaseAddress = new Uri(groqBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(45);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", groqApiKey);
    }).AddTypedClient<IAgentProvider>((http, _) =>
        new GroqAgentProvider(http, groqModel));
}
else if (ollamaEnabled)
{
    // Ollama: free local LLM — install from https://ollama.com then: ollama pull llama3
    builder.Services.AddHttpClient<IAgentProvider, OllamaAgentProvider>(client =>
    {
        client.BaseAddress = new Uri(ollamaBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(90);
    }).AddTypedClient<IAgentProvider>((http, _) =>
        new OllamaAgentProvider(http, ollamaModel));
}
else
{
    // Demo: deterministic outputs with simulated ReAct tool-call traces (no LLM required)
    builder.Services.AddScoped<IAgentProvider, DemoAgentProvider>();
}
builder.Services.AddScoped<IWorkflowPlanner, WorkflowPlanner>();
builder.Services.AddScoped<WorkflowOrchestrator>();
builder.Services.AddSingleton<IWorkflowStore, InMemoryWorkflowStore>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Agentic URL Shortener API",
        Version = "v1",
        Description = "URL Shortener service with agentic SDLC orchestration"
    });
});


var app = builder.Build();

// Create the database automatically for the prototype.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider
        .GetRequiredService<UrlShortenerDbContext>();

    await dbContext.Database.EnsureCreatedAsync();
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Agentic URL Shortener v1");
    options.RoutePrefix = "swagger";
});


app.UseHttpsRedirection();

app.UseMiddleware<GlobalExceptionMiddleware>();

app.MapControllers();

app.Run();
public partial class Program
{
}