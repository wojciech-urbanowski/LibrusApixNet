namespace LibrusApixNet;

public sealed record Lesson(string Subject, string Teacher, string Topic, string ZValue, string AttendanceSymbol,
    string AttendanceHref, int LessonNumber, string Weekday, string Date);

public static class CompletedLessons
{
    /// <summary>Returns a count of pages, matching upstream (unlike Messages.GetMaxPageNumberAsync).</summary>
    public static async Task<int> GetMaxPageNumberAsync(Client client, string dateFrom, string dateTo, CancellationToken cancellationToken = default)
    {
        using var document = Html.Parse(await client.PostTextAsync(client.Options.CompletedLessonsUrl, Payload(dateFrom, dateTo, 0), cancellationToken));
        return Html.PageCount(document);
    }

    public static async Task<List<Lesson>> GetCompletedAsync(Client client, string dateFrom, string dateTo, int page = 0,
        CancellationToken cancellationToken = default) =>
        Parse(await client.PostTextAsync(client.Options.CompletedLessonsUrl, Payload(dateFrom, dateTo, page), cancellationToken));

    private static Dictionary<string, string> Payload(string dateFrom, string dateTo, int page)
    {
        Html.ValidateDates(dateFrom, dateTo);
        if (page < 0) throw new LibrusArgumentException("Page must not be negative.");
        return new Dictionary<string, string>
        {
            ["data1"] = dateFrom, ["data2"] = dateTo, ["filtruj_id_przedmiotu"] = "-1",
            ["numer_strony1001"] = page.ToString(System.Globalization.CultureInfo.InvariantCulture), ["porcjowanie_pojemnik1001"] = "1001"
        };
    }

    public static List<Lesson> Parse(string html)
    {
        using var document = Html.Parse(html);
        return document.QuerySelectorAll("table[class='decorated'] > tbody > tr").Select(row =>
        {
            var data = row.QuerySelectorAll("td:not([class])").Select(td => td.TextContent.Trim()).ToList();
            if (data.Count < 5) throw new ParseException("Completed lesson data has less than 5 elements.");
            var subjectTeacher = data[1].Split(", ");
            var teacher = subjectTeacher.Length == 2 ? subjectTeacher[1] : subjectTeacher[0];
            return new Lesson(subjectTeacher[0], teacher, data[2], data[3], data[4], Html.PopupId(Html.Attr(row.QuerySelector("td > p.box > a"), "onclick")),
                Html.Int(data[0], "lesson number"), Html.Text(row.QuerySelector("td.tiny")), Html.Text(row.QuerySelector("td[class='center small']")) is { Length: > 0 } date ? date : "01-01-2000");
        }).ToList();
    }
}
