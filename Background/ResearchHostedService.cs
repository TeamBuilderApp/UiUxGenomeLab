using System.Collections.Concurrent;
using UiUxGenomeLab.Domain;
using UiUxGenomeLab.Services;

namespace UiUxGenomeLab.Background;

//Background hosted service + HTTP API

public sealed class ResearchHostedService : BackgroundService
{
    private readonly UiUxResearchService _researchService;
    private readonly ILogger<ResearchHostedService> _logger;
    private readonly ConcurrentDictionary<string, UiUxResearchResult> _results = new();
    private readonly ConcurrentQueue<(string JobId, UiUxResearchConfig Config)> _queue = new();

    public ResearchHostedService(
        UiUxResearchService researchService,
        ILogger<ResearchHostedService> logger)
    {
        _researchService = researchService;
        _logger = logger;
    }

    public string Enqueue(UiUxResearchConfig config)
    {
        var jobId = Guid.NewGuid().ToString("N");
        _queue.Enqueue((jobId, config));
        _logger.LogInformation("Enqueued research job {JobId}", jobId);
        return jobId;
    }

    public bool TryGetResult(string jobId, out UiUxResearchResult? result) =>
        _results.TryGetValue(jobId, out result);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ResearchHostedService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_queue.TryDequeue(out var job))
            {
                try
                {
                    var result = await _researchService.RunJobAsync(job.JobId, job.Config, stoppingToken);
                    _results[job.JobId] = result;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Job {JobId} cancelled.", job.JobId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Job {JobId} failed.", job.JobId);
                }
            }
            else
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }

        _logger.LogInformation("ResearchHostedService stopping.");
    }
}
