using System.Globalization;

namespace LibrusApixNet;

public sealed record PeriodChange(string TeacherSwap, string SubjectSwap, string ClassroomSwap, string DateAdded);
public sealed record Period(string Subject, string TeacherAndClassroom, string Date, string DateFrom, string DateTo,
    string Weekday, Dictionary<string, PeriodChange?> Info, int Number, string? NextRecessFrom, string? NextRecessTo);

public static class Timetable
{
    public static async Task<List<List<Period>>> GetTimetableAsync(Client client, DateOnly mondayDate, CancellationToken cancellationToken = default)
    {
        if (mondayDate.DayOfWeek != DayOfWeek.Monday) throw new DateException("You must input a Monday date.");
        var week = mondayDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "_" + mondayDate.AddDays(6).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return Parse(await client.PostTextAsync(client.Options.TimetableUrl, new Dictionary<string, string> { ["tydzien"] = week }, cancellationToken));
    }

    public static List<List<Period>> Parse(string html)
    {
        using var document = Html.Parse(html);
        var table = Html.Require(document, "table.decorated.plan-lekcji", "Error in parsing timetable table.");
        // HTML5 parsers insert tbody even if the original page omits it.
        var rows = table.QuerySelectorAll(":scope > tr.line1, :scope > tbody > tr.line1");
        var recess = table.QuerySelectorAll(":scope > tr.line0, :scope > tbody > tr.line0");
        if (rows.Length == 0) throw new ParseException("Error in parsing timetable.");
        var result = Enumerable.Range(0, 7).Select(_ => new List<Period>()).ToList();
        for (var i = 0; i < rows.Length; i++)
        {
            var cells = rows[i].QuerySelectorAll("td[id='timetableEntryBox'][class='line1']");
            if (cells.Length != 7) throw new ParseException("Expected seven timetable day cells.");
            var number = Html.Int(Html.Require(rows[i], "td[class='center']", "Missing lesson number.").TextContent, "timetable lesson number");
            string? recessFrom = null, recessTo = null;
            if (i < recess.Length)
            {
                var times = Html.Require(recess[i], "td.center", "Missing recess time.").TextContent.Replace("&nbsp;", "").Trim().Split('-', 2);
                if (times.Length != 2) throw new ParseException("Invalid recess time range.");
                recessFrom = times[0].Trim(); recessTo = times[1].Trim();
            }
            for (var day = 0; day < 7; day++)
            {
                var lesson = cells[day];
                var attributes = lesson.Attributes.Where(a => a.Name.StartsWith("data", StringComparison.Ordinal)).ToList();
                var date = lesson.GetAttribute("data-date") ?? attributes.ElementAtOrDefault(0)?.Value;
                var from = lesson.GetAttribute("data-from") ?? attributes.ElementAtOrDefault(1)?.Value;
                var to = lesson.GetAttribute("data-to") ?? attributes.ElementAtOrDefault(2)?.Value;
                if (date is null || from is null || to is null || !DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                    throw new ParseException("Invalid timetable date/time attributes.");
                var info = new Dictionary<string, PeriodChange?>();
                var tooltip = lesson.QuerySelector("div.center.plan-lekcji-info");
                if (tooltip is not null)
                {
                    var anchor = lesson.QuerySelector("a");
                    PeriodChange? change = null;
                    if (anchor is not null)
                    {
                        var data = Html.TitlePairs(Html.Attr(anchor, "title").Replace('\u00a0', ' '), stripBold: true);
                        change = new PeriodChange(data.GetValueOrDefault("Nauczyciel", ""), data.GetValueOrDefault("Przedmiot", ""),
                            data.GetValueOrDefault("Sala", ""), data.GetValueOrDefault("Data dodania", ""));
                    }
                    info[tooltip.TextContent.Trim()] = change;
                }
                var subjects = new List<string>();
                var teachers = new List<string>();
                foreach (var text in lesson.QuerySelectorAll("div.text"))
                {
                    var subject = text.QuerySelector("b");
                    if (subject is not null) subjects.Add(subject.TextContent.Trim());
                    var data = Html.NoNewlines(text.TextContent.Replace('\u00a0', ' ').Replace("&nbsp", "")).Split('-', 2);
                    if (data.Length >= 2) teachers.Add(data[1].Trim());
                }
                result[day].Add(new Period(string.Join(" / ", subjects), string.Join(" / ", teachers), date, from, to,
                    parsedDate.DayOfWeek.ToString(), info, number, recessFrom, recessTo));
            }
        }
        return result;
    }
}
