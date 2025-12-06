using OpenAI.Responses;
using UiUxGenomeLab.Background;
using UiUxGenomeLab.Domain;
using UiUxGenomeLab.Services;

var builder = WebApplication.CreateBuilder(args);

// Config
builder.Configuration.AddJsonFile("appsettings.json", optional: true)
                     .AddEnvironmentVariables();

// OpenAI Responses client for DI
builder.Services.AddSingleton<OpenAIResponseClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var apiKey = config["OpenAI:ApiKey"]
        ?? throw new InvalidOperationException("Missing OpenAI:ApiKey");
    var model = config["OpenAI:Model"] ?? "gpt-4.1-mini";

    return new OpenAIResponseClient(model, apiKey);
});

// Services
builder.Services.AddSingleton<ISearchProvider, NoopSearchProvider>(); // swap for real implementation
builder.Services.AddSingleton<PromptRefinementService>();
builder.Services.AddSingleton<OpenAiDesignClient>();
builder.Services.AddSingleton<GeneticOptimizer>();
builder.Services.AddSingleton<UiUxResearchService>();

builder.Services.AddHostedService<ResearchHostedService>();

var app = builder.Build();

// HTTP endpoints
app.MapPost("/api/research/start", (
    UiUxResearchConfig config,
    ResearchHostedService host) =>
{
    var jobId = host.Enqueue(config);
    return Results.Ok(new { jobId });
});

app.MapGet("/api/research/{jobId}", (
    string jobId,
    ResearchHostedService host) =>
{
    if (!host.TryGetResult(jobId, out var result) || result == null)
        return Results.NotFound(new { jobId, status = "pending" });

    return Results.Ok(new
    {
        jobId = result.JobId,
        startedAt = result.StartedAtUtc,
        completedAt = result.CompletedAtUtc,
        bestCandidateId = result.BestCandidate?.Id,
        bundlePath = result.ResearchBundlePath,
        indexHtmlPath = result.IndexHtmlPath,
        candidateCount = result.AllCandidates.Count
    });
});

app.Run();
