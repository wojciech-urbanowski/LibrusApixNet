using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace LibrusApixNet;

internal static class Html
{
    internal static IDocument Parse(string html)
    {
        var document = new HtmlParser().ParseDocument(html);
        if (document.QuerySelector("h2.inside")?.TextContent.Contains("Brak dostępu", StringComparison.Ordinal) == true)
            throw new TokenException("Malformed or expired token.");
        return document;
    }

    internal static IElement Require(IParentNode parent, string selector, string message) =>
        parent.QuerySelector(selector) ?? throw new ParseException(message);

    internal static List<IElement> Rows(IParentNode parent) => parent.QuerySelectorAll("tr.line0, tr.line1").ToList();
    internal static string Text(IElement? element) => element?.TextContent ?? "";
    internal static string Attr(IElement? element, string name, string fallback = "") => element?.GetAttribute(name) ?? fallback;
    internal static string NoNewlines(string text) => text.Replace("\r", "").Replace("\n", "");
    internal static int Int(string text, string context) => int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
        ? n : throw new ParseException($"Invalid integer in {context}: '{text}'.");

    internal static Dictionary<string, string> TitlePairs(string title, bool stripBold = false)
    {
        if (stripBold) title = title.Replace("<b>", "").Replace("</b>", "");
        var result = new Dictionary<string, string>();
        foreach (var part in Regex.Split(title, @"<br\s*/?>", RegexOptions.IgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            var pair = part.Split(':', 2);
            result[pair[0].Trim()] = pair.Length == 2 ? pair[1].Trim() : "unknown";
        }
        return result;
    }

    internal static string PopupPath(string onclick)
    {
        var match = Regex.Match(onclick, "['\"]([^'\"]+)['\"]");
        return match.Success ? match.Groups[1].Value : "";
    }

    internal static string PopupId(string onclick)
    {
        var parts = PopupPath(onclick).Split('/');
        return parts.Length >= 4 ? parts[3] : "";
    }

    internal static int PageCount(IDocument document)
    {
        var text = Text(document.QuerySelector("div.pagination > span")).Replace("\u00a0", "");
        var match = Regex.Match(text, @"z\s*(\d+)");
        return match.Success ? Int(match.Groups[1].Value, "pagination") : 0;
    }

    internal static string Filter(string sortBy) => sortBy switch
    {
        "all" => "zmiany_logowanie_wszystkie",
        "week" => "zmiany_logowanie_tydzien",
        "last_login" => "zmiany_logowanie",
        _ => throw new LibrusArgumentException("Wrong value for sortBy; expected all, week or last_login.")
    };

    internal static void ValidateDates(string start, string end)
    {
        if (!DateOnly.TryParseExact(start, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from) ||
            !DateOnly.TryParseExact(end, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to) || from > to)
            throw new DateException("Expected a valid, ordered date range in yyyy-MM-dd format.");
    }
}
