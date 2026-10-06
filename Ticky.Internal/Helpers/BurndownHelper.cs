using System.Globalization;
using System.Text;

namespace Ticky.Internal.Helpers;

public static class BurndownHelper
{
    /// <summary>
    /// Open card count per day, from the day the first card was created up to and including <paramref name="today"/>.
    /// </summary>
    public static List<(DateTime Date, int Open)> GetOpenPerDay(
        IReadOnlyCollection<DateTime> cardCreatedAts,
        IReadOnlyCollection<DateTime> completionDates,
        DateTime today
    )
    {
        if (cardCreatedAts.Count == 0)
            return [];

        var startDate = cardCreatedAts.Min().Date;
        int totalDays = Math.Max(1, (today.Date - startDate).Days + 1);

        return Enumerable
            .Range(0, totalDays)
            .Select(d =>
            {
                var day = startDate.AddDays(d);
                int created = cardCreatedAts.Count(x => x.Date <= day);
                int completed = completionDates.Count(x => x.Date <= day);
                return (day, Math.Max(0, created - completed));
            })
            .ToList();
    }

    /// <summary>
    /// Renders the burndown as an inline SVG. Numbers are formatted with the invariant culture
    /// because SVG attributes require '.' as the decimal separator regardless of server locale.
    /// </summary>
    public static string RenderSvg(IReadOnlyList<(DateTime Date, int Open)> points)
    {
        if (!points.Any())
            return string.Empty;

        const int svgW = 760, svgH = 240;
        const int padLeft = 48, padTop = 16, padBottom = 32, padRight = 16;
        int chartW = svgW - padLeft - padRight;
        int chartH = svgH - padTop - padBottom;

        int maxOpen = Math.Max(1, points.Max(p => p.Open));
        int days = points.Count;

        double xScale = days > 1 ? (double)chartW / (days - 1) : chartW;
        double yScale = (double)chartH / maxOpen;

        string Px(int i) => (padLeft + i * xScale).ToString("F1", CultureInfo.InvariantCulture);
        string Py(int open) => (padTop + chartH - open * yScale).ToString("F1", CultureInfo.InvariantCulture);

        var actualPoints = string.Join(" ", points.Select((p, i) => $"{Px(i)},{Py(p.Open)}"));
        string idealStart = $"{Px(0)},{Py(maxOpen)}";
        string idealEnd   = $"{Px(days - 1)},{Py(0)}";

        var xLabels = new StringBuilder();
        int xStep = Math.Max(1, days / 6);
        for (int i = 0; i < days; i += xStep)
        {
            xLabels.Append(
                $"<text x=\"{Px(i)}\" y=\"{svgH - 4}\" text-anchor=\"middle\" font-size=\"10\" opacity=\"0.5\">"
                + points[i].Date.ToString("MM/dd", CultureInfo.InvariantCulture)
                + "</text>"
            );
        }

        var yLabels = new StringBuilder();
        int yStep = Math.Max(1, maxOpen / 4);
        for (int v = 0; v <= maxOpen; v += yStep)
        {
            yLabels.Append(
                $"<text x=\"{padLeft - 6}\" y=\"{Py(v)}\" text-anchor=\"end\""
                + $" dominant-baseline=\"middle\" font-size=\"10\" opacity=\"0.5\">{v}</text>"
            );
        }

        var sb = new StringBuilder();
        sb.Append($"<svg viewBox=\"0 0 {svgW} {svgH}\" xmlns=\"http://www.w3.org/2000/svg\" class=\"w-full\">");
        sb.Append($"<text x=\"10\" y=\"{padTop + chartH / 2}\" text-anchor=\"middle\" font-size=\"10\" opacity=\"0.5\" transform=\"rotate(-90,10,{padTop + chartH / 2})\">Open cards</text>");
        sb.Append($"<line x1=\"{padLeft}\" y1=\"{padTop}\" x2=\"{padLeft}\" y2=\"{padTop + chartH}\" stroke=\"currentColor\" stroke-width=\"1\" opacity=\"0.15\"/>");
        sb.Append($"<line x1=\"{padLeft}\" y1=\"{padTop + chartH}\" x2=\"{padLeft + chartW}\" y2=\"{padTop + chartH}\" stroke=\"currentColor\" stroke-width=\"1\" opacity=\"0.15\"/>");
        sb.Append(yLabels);
        sb.Append(xLabels);
        sb.Append($"<polyline points=\"{idealStart} {idealEnd}\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.5\" stroke-dasharray=\"4 3\" opacity=\"0.35\"/>");
        sb.Append($"<polyline points=\"{actualPoints}\" fill=\"none\" stroke=\"#ec4899\" stroke-width=\"2\" stroke-linejoin=\"round\"/>");
        sb.Append($"<circle cx=\"{Px(days - 1)}\" cy=\"{Py(points[^1].Open)}\" r=\"3\" fill=\"#ec4899\"/>");
        sb.Append("</svg>");
        return sb.ToString();
    }
}
