using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.Json;

namespace LibrusApixNet;

public sealed record Attendance(string Symbol, string Href, int Semester, string Date, string Type, string Teacher,
    int Period, bool Excursion, string Topic, string Subject);
public sealed record AttendanceType(string Short, string Name);
public sealed record GatewayAttendance(AttendanceType Type, int LessonNumber, int Semester);
/// <summary>Fractions in the range 0–1, as in the Python library.</summary>
public sealed record AttendanceFrequency(double FirstSemester, double SecondSemester, double Overall);

public static class Attendances
{
    private static readonly IReadOnlyDictionary<string, AttendanceType> Types = new Dictionary<string, AttendanceType>
    {
        ["1"] = new("nb", "Nieobecność"), ["2"] = new("sp", "Spóźnienie"), ["3"] = new("u", "Nieobecność uspr."),
        ["4"] = new("zw", "Zwolnienie"), ["100"] = new("ob", "Obecność"), ["1266"] = new("wy", "Wycieczka"),
        ["2022"] = new("k", "Konkurs szkolny"), ["2829"] = new("sz", "Szkolenie")
    };

    public static async Task<List<List<Attendance>>> GetAttendanceAsync(Client client, string sortBy = "all", CancellationToken cancellationToken = default)
    {
        var key = Html.Filter(sortBy);
        return Parse(await client.PostTextAsync(client.Options.AttendanceUrl,
            new Dictionary<string, string> { [key] = sortBy == "all" ? "" : key }, cancellationToken));
    }

    public static List<List<Attendance>> Parse(string html)
    {
        using var document = Html.Parse(html);
        var table = Html.Require(document, "table.center.big.decorated", "Error parsing attendance table.");
        List<List<Attendance>> semesters = [[], []];
        var semester = -1;
        foreach (var row in Html.Rows(table))
        {
            if (row.QuerySelector("td.center.bolded") is not null) semester++;
            if (semester > 1) throw new ParseException("More than two attendance semesters.");
            foreach (var anchor in row.QuerySelectorAll("td[class='center'] a"))
            {
                if (semester < 0) throw new ParseException("Attendance before semester marker.");
                if (!anchor.HasAttribute("title")) throw new ParseException("Absence anchor title is missing.");
                var title = Html.Attr(anchor, "title").Replace("</b>", "<br>").Replace("<br/>", "");
                var data = Html.TitlePairs(title);
                semesters[semester].Add(new Attendance(anchor.TextContent, Html.PopupId(Html.Attr(anchor, "onclick")), semester,
                    data.GetValueOrDefault("Data", "").Split(' ')[0], data.GetValueOrDefault("Rodzaj", ""),
                    data.GetValueOrDefault("Nauczyciel", ""), Html.Int(data.GetValueOrDefault("Godzina lekcyjna", "0"), "attendance period"),
                    data.GetValueOrDefault("Czy wycieczka") == "Tak", data.GetValueOrDefault("Temat zajęć", ""), data.GetValueOrDefault("Lekcja", "")));
            }
        }
        if (semester < 0) throw new ParseException("Couldn't find attendance semester.");
        if (semester == 1) semesters.Reverse();
        return semesters;
    }

    public static async Task<Dictionary<string, string>> GetDetailAsync(Client client, string detailUrl, CancellationToken cancellationToken = default) =>
        ParseDetail(await client.GetTextAsync(client.Options.AttendanceDetailsUrl + detailUrl, cancellationToken));

    public static Dictionary<string, string> ParseDetail(string html)
    {
        using var document = Html.Parse(html);
        var container = Html.Require(document, "div.container-background", "Error in parsing attendance details.");
        var rows = Html.Rows(container);
        if (rows.Count == 0) throw new ParseException("Attendance detail lines are empty.");
        var result = new Dictionary<string, string>();
        foreach (var row in rows)
        {
            var key = row.QuerySelector("th");
            var value = row.QuerySelector("td");
            if (key is not null && value is not null) result[key.TextContent] = value.TextContent;
        }
        return result;
    }

    public static async Task<List<GatewayAttendance>> GetGatewayAttendanceAsync(Client client, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(client.Token.OAuth)) await client.RefreshOAuthAsync(cancellationToken);
        var json = await client.GetJsonAsync(client.Options.GatewayApiAttendanceUrl, cancellationToken);
        return json.GetProperty("Attendances").EnumerateArray().Select(a =>
        {
            var id = JsonText(a.GetProperty("Type").GetProperty("Id"));
            var type = Types.GetValueOrDefault(id, new AttendanceType("unknown", "unknown"));
            return new GatewayAttendance(type, Html.Int(JsonText(a.GetProperty("LessonNo")), "gateway lesson number"),
                Html.Int(JsonText(a.GetProperty("Semester")), "gateway semester"));
        }).ToList();
    }

    public static async Task<AttendanceFrequency> GetAttendanceFrequencyAsync(Client client, CancellationToken cancellationToken = default) =>
        CalculateFrequency(await GetGatewayAttendanceAsync(client, cancellationToken));

    public static AttendanceFrequency CalculateFrequency(IReadOnlyList<GatewayAttendance> attendance)
    {
        static double Frequency(IEnumerable<GatewayAttendance> records)
        {
            var list = records.ToList();
            return list.Count == 0 ? 1 : (double)list.Count(a => a.Type.Short is "wy" or "ob" or "sp") / list.Count;
        }
        return new AttendanceFrequency(Frequency(attendance.Where(a => a.Semester == 1)), Frequency(attendance.Where(a => a.Semester == 2)), Frequency(attendance));
    }

    public static Dictionary<string, double> CalculateSubjectFrequency(IReadOnlyDictionary<string, Dictionary<string, int>> attendance)
    {
        var result = new Dictionary<string, double>();
        foreach (var (subject, counts) in attendance)
        {
            var attended = counts.GetValueOrDefault("ob") + counts.GetValueOrDefault("sp");
            var total = attended + counts.GetValueOrDefault("nb") + counts.GetValueOrDefault("u") + counts.GetValueOrDefault("zw");
            result[subject] = total > 0 ? Math.Round((double)attended / total * 100, 2, MidpointRounding.ToEven) : 100;
        }
        return result;
    }

    public static async Task<Dictionary<string, double>> GetSubjectFrequencyAsync(Client client,
        IReadOnlyDictionary<string, Dictionary<string, int>>? attendances = null, DateOnly? start = null, DateOnly? end = null,
        CancellationToken cancellationToken = default) =>
        CalculateSubjectFrequency(attendances is { Count: > 0 } ? attendances : await GetSubjectAttendanceAsync(client, start, end, cancellationToken));

    public static async Task<Dictionary<string, Dictionary<string, int>>> GetSubjectAttendanceAsync(Client client,
        DateOnly? start = null, DateOnly? end = null, CancellationToken cancellationToken = default)
    {
        if (start > end) throw new DateException("Start date must not be after end date.");
        await client.RefreshOAuthAsync(cancellationToken);
        var json = await client.GetJsonAsync(client.Options.GatewayApiAttendanceUrl, cancellationToken);
        var records = json.GetProperty("Attendances").EnumerateArray().Where(a =>
        {
            if (start is null && end is null) return true;
            var date = DateOnly.ParseExact(a.GetProperty("Date").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            return (start is null || date >= start) && (end is null || date <= end);
        }).ToList();
        var lessons = new ConcurrentDictionary<string, Lazy<Task<string>>>();
        var subjects = new ConcurrentDictionary<string, Lazy<Task<string>>>();
        using var throttle = new SemaphoreSlim(8);

        async Task<JsonElement> Request(string url)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    await throttle.WaitAsync(cancellationToken);
                    try { return await client.GetJsonAsync(url, cancellationToken); }
                    finally { throttle.Release(); }
                }
                catch (HttpRequestException e) when (attempt < 4 && (e.StatusCode is null or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)e.StatusCode >= 500))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500 * (1 << attempt)), client.Options.TimeProvider, cancellationToken);
                }
            }
        }

        async Task<string> SubjectForLesson(string id)
        {
            var lesson = await Request(client.Options.BaseUrl + "/gateway/api/2.0/Lessons/" + Uri.EscapeDataString(id));
            var subjectId = JsonText(lesson.GetProperty("Lesson").GetProperty("Subject").GetProperty("Id"));
            return await subjects.GetOrAdd(subjectId, key => new Lazy<Task<string>>(async () =>
            {
                var subject = await Request(client.Options.BaseUrl + "/gateway/api/2.0/Subjects/" + Uri.EscapeDataString(key));
                return subject.GetProperty("Subject").GetProperty("Name").GetString() ?? "";
            })).Value;
        }

        var results = await Task.WhenAll(records.Select(async a =>
        {
            var lessonId = JsonText(a.GetProperty("Lesson").GetProperty("Id"));
            var typeId = JsonText(a.GetProperty("Type").GetProperty("Id"));
            var subject = await lessons.GetOrAdd(lessonId, key => new Lazy<Task<string>>(() => SubjectForLesson(key))).Value;
            return (Subject: subject, Type: Types.GetValueOrDefault(typeId)?.Short ?? "unknown");
        }));
        var counts = new Dictionary<string, Dictionary<string, int>>();
        foreach (var item in results)
        {
            counts.TryAdd(item.Subject, []);
            counts[item.Subject][item.Type] = counts[item.Subject].GetValueOrDefault(item.Type) + 1;
        }
        return counts;
    }

    private static string JsonText(JsonElement element) => element.ValueKind == JsonValueKind.String ? element.GetString()! : element.ToString();
}
