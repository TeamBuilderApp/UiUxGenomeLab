using System.Text;
using UiUxGenomeLab.Domain;

namespace UiUxGenomeLab.Services;
/*
 * HTML demo generation for every candidate
 * Each candidate becomes a simple HTML file (one per variant) so you can visually compare.
 * At runtime write this HTML string out as {jobId}/{candidate.Id}.html.
 */

public static class HtmlDemoRenderer
{
    public static string RenderHtml(UiUxDesignCandidate candidate)
    {
        var s = candidate.Spec;
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\" />");
        sb.AppendLine($"<title>{System.Net.WebUtility.HtmlEncode(candidate.Name)}</title>");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");

        // Very light inline CSS. You can emit Tailwind / design tokens instead.
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; margin:0; padding:0; background:#f5f5f5; }");
        sb.AppendLine(".shell { max-width: 480px; margin: 0 auto; padding: 24px; }");
        sb.AppendLine(".card { background:white; border-radius:16px; padding:24px; box-shadow:0 10px 30px rgba(0,0,0,0.12); }");
        sb.AppendLine(".tag { display:inline-block; padding:2px 8px; border-radius:999px; font-size:11px; background:#eef; margin-right:4px; }");
        sb.AppendLine(".meta { font-size:12px; color:#555; margin-top:8px; }");
        sb.AppendLine("</style>");

        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<div class=\"shell\">");
        sb.AppendLine("<div class=\"card\">");

        sb.AppendLine($"<h1>{System.Net.WebUtility.HtmlEncode(candidate.Name)}</h1>");
        sb.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(candidate.Summary)}</p>");

        sb.AppendLine("<div class=\"meta\">");
        sb.AppendLine($"<span class=\"tag\">Layout: {System.Net.WebUtility.HtmlEncode(s.LayoutPattern)}</span>");
        sb.AppendLine($"<span class=\"tag\">Nav: {System.Net.WebUtility.HtmlEncode(s.NavigationPattern)}</span>");
        sb.AppendLine($"<span class=\"tag\">Palette: {System.Net.WebUtility.HtmlEncode(s.ColorPalette)}</span>");
        sb.AppendLine($"<span class=\"tag\">Type: {System.Net.WebUtility.HtmlEncode(s.TypographyScale)}</span>");
        sb.AppendLine($"<span class=\"tag\">Style: {System.Net.WebUtility.HtmlEncode(s.ComponentLibraryStyle)}</span>");
        sb.AppendLine("</div>");

        if (!string.IsNullOrWhiteSpace(s.InteractionNotes))
        {
            sb.AppendLine("<h2>Interaction notes</h2>");
            sb.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(s.InteractionNotes)}</p>");
        }

        if (!string.IsNullOrWhiteSpace(s.AccessibilityNotes))
        {
            sb.AppendLine("<h2>Accessibility notes</h2>");
            sb.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(s.AccessibilityNotes)}</p>");
        }

        if (!string.IsNullOrWhiteSpace(candidate.EvaluationRationale))
        {
            sb.AppendLine("<h2>Model evaluation</h2>");
            sb.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(candidate.EvaluationRationale)}</p>");
        }

        sb.AppendLine("<h2>Scores</h2>");
        sb.AppendLine("<ul>");
        sb.AppendLine($"<li>Usability: {candidate.UsabilityScore:F1}</li>");
        sb.AppendLine($"<li>Accessibility: {candidate.AccessibilityScore:F1}</li>");
        sb.AppendLine($"<li>Visual clarity: {candidate.VisualClarityScore:F1}</li>");
        sb.AppendLine($"<li>Implementation complexity: {candidate.ImplementationComplexityScore:F1}</li>");
        sb.AppendLine($"<li>Overall fitness: {candidate.OverallFitness:F2}</li>");
        sb.AppendLine("</ul>");

        sb.AppendLine("</div></div></body></html>");

        return sb.ToString();
    }
}
