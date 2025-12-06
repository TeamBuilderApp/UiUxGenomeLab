//Research service + background runner.
using System.Text;
using UiUxGenomeLab.Domain;

namespace UiUxGenomeLab.Services;

public sealed class UiUxResearchService
{
    private readonly OpenAiDesignClient _designClient;
    private readonly GeneticOptimizer _genetic;
    private readonly PromptRefinementService _promptRefiner;
    private readonly ILogger<UiUxResearchService> _logger;
    private readonly string _outputRoot;

    public UiUxResearchService(
        OpenAiDesignClient designClient,
        GeneticOptimizer genetic,
        PromptRefinementService promptRefiner,
        IConfiguration config,
        ILogger<UiUxResearchService> logger)
    {
        _designClient = designClient;
        _genetic = genetic;
        _promptRefiner = promptRefiner;
        _logger = logger;
        _outputRoot = config["Output:RootDirectory"] ?? "Output";
        Directory.CreateDirectory(_outputRoot);
    }

    public async Task<UiUxResearchResult> RunJobAsync(
        string jobId,
        UiUxResearchConfig config,
        CancellationToken ct)
    {
        var result = new UiUxResearchResult
        {
            JobId = jobId,
            StartedAtUtc = DateTimeOffset.UtcNow
        };

        var jobDir = Path.Combine(_outputRoot, jobId);
        Directory.CreateDirectory(jobDir);

        var deadline = result.StartedAtUtc + config.MaxDuration;

        IReadOnlyList<UiUxDesignCandidate>? currentPopulation = null;

        for (int gen = 0; gen < config.MaxGenerations && DateTimeOffset.UtcNow < deadline; gen++)
        {
            ct.ThrowIfCancellationRequested();
            _logger.LogInformation("Job {JobId}: generation {Generation}", jobId, gen);

            if (currentPopulation is null) // initial
            {
                var refinedQuestion = await _promptRefiner.RefineQuestionAsync(
                    config.ProblemStatement, ct);

                var initConfig = config with { ProblemStatement = refinedQuestion };
                currentPopulation = await _designClient.GenerateInitialPopulationAsync(
                    initConfig,
                    generationIndex: gen,
                    populationSize: config.PopulationSize,
                    ct);
            }

            // Score population
            await _designClient.ScoreCandidatesAsync(currentPopulation, config, ct);

            // Persist candidates + demos
            foreach (var cand in currentPopulation)
            {
                result.AllCandidates.Add(cand);
                PersistCandidateHtml(jobDir, cand);
            }

            // Update best
            var bestInGen = currentPopulation.OrderByDescending(c => c.OverallFitness).First();
            if (result.BestCandidate == null || bestInGen.OverallFitness > result.BestCandidate.OverallFitness)
            {
                result.BestCandidate = bestInGen;
            }

            // Genetic step
            var elites = _genetic.SelectElite(currentPopulation, eliteCount: Math.Max(3, config.PopulationSize / 5));
            currentPopulation = _genetic.MutateAndCrossover(
                elites,
                targetPopulationSize: config.PopulationSize,
                generationIndex: gen + 1);
        }

        // Final bundle & index
        result.CompletedAtUtc = DateTimeOffset.UtcNow;
        result.ResearchBundlePath = WriteBundle(jobDir, result);
        result.IndexHtmlPath = WriteIndex(jobDir, result);

        _logger.LogInformation("Job {JobId} completed. Best fitness: {Fitness}",
            jobId, result.BestCandidate?.OverallFitness);

        return result;
    }

    private static void PersistCandidateHtml(string jobDir, UiUxDesignCandidate candidate)
    {
        var html = HtmlDemoRenderer.RenderHtml(candidate);
        var path = Path.Combine(jobDir, $"{candidate.Id}.html");
        File.WriteAllText(path, html);
    }

    private static string WriteBundle(string jobDir, UiUxResearchResult result)
    {
        var bundle = new
        {
            result.JobId,
            result.StartedAtUtc,
            result.CompletedAtUtc,
            BestCandidateId = result.BestCandidate?.Id,
            Candidates = result.AllCandidates.Select(c => new
            {
                c.Id,
                c.Name,
                c.Summary,
                c.Spec,
                Scores = new
                {
                    c.UsabilityScore,
                    c.AccessibilityScore,
                    c.VisualClarityScore,
                    c.ImplementationComplexityScore,
                    c.OverallFitness
                },
                c.EvaluationRationale
            })
        };

        var json = System.Text.Json.JsonSerializer.Serialize(
            bundle,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        var path = Path.Combine(jobDir, "research-bundle.json");
        File.WriteAllText(path, json);
        return path;
    }

    private static string WriteIndex(string jobDir, UiUxResearchResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\" />");
        sb.AppendLine("<title>UI/UX Genome Lab – Results</title>");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:system-ui,sans-serif;background:#f2f2f2;padding:24px;}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;background:white;border-radius:12px;overflow:hidden;}");
        sb.AppendLine("th,td{padding:8px 12px;border-bottom:1px solid #eee;font-size:13px;}");
        sb.AppendLine("th{background:#fafafa;text-align:left;}");
        sb.AppendLine("tr:hover{background:#f9f9ff;}");
        sb.AppendLine(".badge{display:inline-block;padding:2px 8px;border-radius:999px;font-size:11px;background:#eef;}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine($"<h1>Job {result.JobId} – UI/UX Genome Results</h1>");
        sb.AppendLine("<p>Each row links to an individual HTML demo. Scores approximate UX quality (higher is better, except complexity).</p>");

        sb.AppendLine("<table><thead><tr>");
        sb.AppendLine("<th>ID</th><th>Name</th><th>Layout</th><th>Nav</th><th>Palette</th><th>Style</th>");
        sb.AppendLine("<th>Usability</th><th>Access.</th><th>Visual</th><th>Complexity</th><th>Fitness</th>");
        sb.AppendLine("</tr></thead><tbody>");

        foreach (var c in result.AllCandidates.OrderByDescending(c => c.OverallFitness))
        {
            var fileName = $"{c.Id}.html";
            sb.AppendLine("<tr>");
            sb.AppendLine($"<td><a href=\"{fileName}\">{c.Id}</a></td>");
            sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(c.Name)}</td>");
            sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(c.Spec.LayoutPattern)}</td>");
            sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(c.Spec.NavigationPattern)}</td>");
            sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(c.Spec.ColorPalette)}</td>");
            sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(c.Spec.ComponentLibraryStyle)}</td>");
            sb.AppendLine($"<td>{c.UsabilityScore:F1}</td>");
            sb.AppendLine($"<td>{c.AccessibilityScore:F1}</td>");
            sb.AppendLine($"<td>{c.VisualClarityScore:F1}</td>");
            sb.AppendLine($"<td>{c.ImplementationComplexityScore:F1}</td>");
            sb.AppendLine($"<td><span class=\"badge\">{c.OverallFitness:F2}</span></td>");
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</tbody></table></body></html>");

        var path = Path.Combine(jobDir, "index.html");
        File.WriteAllText(path, sb.ToString());
        return path;
    }
}
