using System.Globalization;
using System.Text.RegularExpressions;

namespace LibrusApixNet;

public sealed record Event(string Title, string Subject, Dictionary<string, string> Data, string Day, int? Number, string Hour, string Href);
public sealed record RecentEvent(string DateAdded, string Type, string Data);

public static class Schedule
{
    public static async Task<Dictionary<int, List<Event>>> GetScheduleAsync(Client client, int month, int year,
        bool includeEmpty = false, CancellationToken cancellationToken = default)
    {
        if (month is < 1 or > 12 || year is < 1 or > 9999) throw new DateException("Invalid schedule month/year.");
        return Parse(await client.PostTextAsync(client.Options.ScheduleUrl,
            new Dictionary<string, string> { ["rok"] = year.ToString(CultureInfo.InvariantCulture), ["miesiac"] = month.ToString(CultureInfo.InvariantCulture) }, cancellationToken), includeEmpty);
    }

    public static Dictionary<int, List<Event>> Parse(string html, bool includeEmpty = false)
    {
        using var document = Html.Parse(html);
        var days = document.QuerySelectorAll("div.kalendarz-dzien");
        if (days.Length == 0) throw new ParseException("Error in parsing days of the schedule.");
        var schedule = new Dictionary<int, List<Event>>();
        foreach (var day in days)
        {
            var d = Html.Int(Html.Require(day, "div.kalendarz-numer-dnia", "Missing day number.").TextContent, "schedule day");
            if (includeEmpty) schedule.TryAdd(d, []);
            foreach (var row in day.QuerySelectorAll("tr"))
            {
                var cell = row.QuerySelector("td");
                if (cell is null) continue;
                var data = Html.TitlePairs(Html.Attr(cell, "title", "Nauczyciel: unknown<br />Opis: unknown"));
                var span = cell.QuerySelector("span");
                var subject = span?.TextContent;
                span?.Remove();
                foreach (var br in cell.QuerySelectorAll("br")) br.Replace(document.CreateTextNode("###"));
                var text = Html.NoNewlines(cell.TextContent.Replace('\u00a0', ' ').Replace(", ", "")).Trim();
                var lines = text.Split("###");
                subject ??= lines[0];
                var title = lines.Length >= 2 ? lines[1] : lines[0];
                int? number = null;
                var hour = "unknown";
                var hourMatch = Regex.Match(text, @"\b([0-2]?\d:[0-5]\d)\b");
                if (hourMatch.Success) hour = hourMatch.Groups[1].Value;
                else
                {
                    var numberMatch = Regex.Match(text, @":\s*(\d{1,2})");
                    if (numberMatch.Success) number = Html.Int(numberMatch.Groups[1].Value, "schedule lesson number");
                }
                var path = Html.PopupPath(Html.Attr(cell, "onclick")).Split('/');
                var href = path.Length >= 2 ? string.Join('/', path.Skip(2)) : "";
                schedule.TryAdd(d, []);
                schedule[d].Add(new Event(title, subject, data, d.ToString(CultureInfo.InvariantCulture), number, hour, href));
            }
        }
        return schedule;
    }

    public static async Task<Dictionary<string, string>> ScheduleDetailAsync(Client client, string prefix, string detailUrl,
        CancellationToken cancellationToken = default) =>
        ParseDetail(await client.GetTextAsync(client.Options.ScheduleUrl + prefix + "/" + detailUrl, cancellationToken));

    public static Dictionary<string, string> ParseDetail(string html)
    {
        using var document = Html.Parse(html);
        var container = Html.Require(document, "div.container-background", "Error in parsing schedule details.");
        var result = new Dictionary<string, string>();
        foreach (var row in Html.Rows(container))
        {
            var key = row.QuerySelector("th"); var value = row.QuerySelector("td");
            if (key is not null && value is not null) result[key.TextContent.Trim()] = value.TextContent.Trim();
        }
        return result;
    }

    public static async Task<List<RecentEvent>> GetRecentlyAddedScheduleAsync(Client client, CancellationToken cancellationToken = default) =>
        ParseRecentlyAdded(await client.GetTextAsync(client.Options.RecentScheduleUrl, cancellationToken));

    public static List<RecentEvent> ParseRecentlyAdded(string html)
    {
        using var document = Html.Parse(html);
        _ = Html.Require(document, "div.container-background", "Unable to locate recent schedule container.");
        var table = document.QuerySelector("table");
        if (table is null) return [];
        var events = new List<RecentEvent>();
        foreach (var row in table.QuerySelectorAll("tr"))
        {
            var cells = row.QuerySelectorAll("td");
            if (cells.Length != 4) continue;
            var date = cells[1].TextContent.Trim(); var type = cells[2].TextContent.Trim();
            if (date.Contains("czas dodania", StringComparison.OrdinalIgnoreCase) && type.Contains("rodzaj zdarzenia", StringComparison.OrdinalIgnoreCase)) continue;
            var data = cells[3].TextContent.Replace("&nbsp;", " ").Replace("<br/>", "<br>").Replace("<br>", "\n").Trim();
            events.Add(new RecentEvent(date, type, data));
        }
        return events;
    }
}
