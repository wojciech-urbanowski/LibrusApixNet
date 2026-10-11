using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace LibrusApixNet;

public sealed record Gpa(int Semester, string Average, string Subject)
{
    public double? NumericValue => double.TryParse(Average.Replace(',', '.'), NumberStyles.Float,
        CultureInfo.InvariantCulture, out var value) ? value : null;
}

public sealed record Grade(string Title, string Mark, bool Counts, string Date, string Href, string Description,
    int Semester, string Category, string Teacher, int Weight)
{
    /// <summary>Null means the grade does not count or has no numeric value; Mark preserves its text.</summary>
    public double? Value
    {
        get
        {
            if (!Counts) return null;
            if (Regex.IsMatch(Mark, @"^[0-9][+-]$"))
                return double.Parse(Mark[..1], CultureInfo.InvariantCulture) + (Mark.EndsWith('+') ? 0.5 : -0.25);
            return double.TryParse(Mark.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value : null;
        }
    }
}

public sealed record GradeDescriptive(string Title, string Mark, string Date, string Href, string Description, int Semester, string Teacher);

public sealed record GradesResult(
    IReadOnlyList<Dictionary<string, List<Grade>>> Semesters,
    Dictionary<string, List<Gpa>> Averages,
    IReadOnlyList<Dictionary<string, List<GradeDescriptive>>> Descriptive);

public static class Grades
{
    public static async Task<GradesResult> GetGradesAsync(Client client, string sortBy = "all", CancellationToken cancellationToken = default)
    {
        var filter = Html.Filter(sortBy);
        var html = await client.PostTextAsync(client.Options.GradesUrl,
            new Dictionary<string, string> { [filter] = "1" }, cancellationToken);
        var result = Parse(html);
        using var document = Html.Parse(html);
        if (document.QuerySelector("table.newSchemaGradesByStudentTable") is null) return result;

        var subjectNames = new Dictionary<string, string>();
        foreach (var row in document.QuerySelectorAll("table.newSchemaGradesByStudentTable tr.studentRow"))
        {
            var id = row.GetAttribute("data-subject_id");
            if (id is not null) subjectNames[id] = Html.Text(row.QuerySelectorAll("td").ElementAtOrDefault(1)).Trim();
        }
        try
        {
            var gatewayGrades = await GetGatewayDescriptiveGradesAsync(client, subjectNames, cancellationToken);
            for (var semester = 0; semester < 2; semester++)
                foreach (var (subject, grades) in gatewayGrades[semester])
                    if (!result.Descriptive[semester].TryGetValue(subject, out var existing) || existing.Count == 0)
                        result.Descriptive[semester][subject] = grades;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Optional gateway timeout: preserve grades already parsed from HTML.
        }
        catch (Exception exception) when (exception is HttpRequestException or LibrusException or JsonException
            or InvalidOperationException or KeyNotFoundException or FormatException or ArgumentException or OverflowException)
        {
            // Upstream treats gateway descriptive grades as optional data.
        }
        return result;
    }

    private static async Task<Dictionary<string, List<GradeDescriptive>>[]> GetGatewayDescriptiveGradesAsync(
        Client client, Dictionary<string, string> subjectNames, CancellationToken cancellationToken)
    {
        var semesters = new[] { new Dictionary<string, List<GradeDescriptive>>(), new Dictionary<string, List<GradeDescriptive>>() };
        await client.RefreshOAuthAsync(cancellationToken);
        var tokenInfo = await client.GetJsonAsync(client.Options.GatewayApiTokenInfoUrl, cancellationToken);
        var userId = JsonText(tokenInfo.GetProperty("UserIdentifier"));
        var userInfo = await client.GetJsonAsync(client.Options.GatewayApiUserInfoUrl + Uri.EscapeDataString(userId), cancellationToken);
        var studentId = JsonField(userInfo, "IdentifierOfStudentAssignedWithUser");
        if (string.IsNullOrEmpty(studentId)) studentId = userId;
        using var response = await client.PostJsonAsync(client.Options.GatewayApiDescriptiveGradesUrl + Uri.EscapeDataString(studentId),
            cancellationToken: cancellationToken);
        response.EnsureSuccessStatusCode();
        var gradeData = await Client.ReadJsonAsync(response, cancellationToken);
        if (!gradeData.TryGetProperty("data", out var items) || items.ValueKind == JsonValueKind.Null) return semesters;
        if (items.GetArrayLength() == 0) return semesters;

        var subjectData = await client.GetJsonAsync(client.Options.GatewayApiSubjectsUrl, cancellationToken);
        var subjects = subjectData.GetProperty("data").EnumerateArray()
            .ToDictionary(subject => JsonText(subject.GetProperty("identifier")), subject => subject);
        var userData = await client.GetJsonAsync(client.Options.GatewayApiUsersUrl, cancellationToken);
        var teachers = userData.GetProperty("Users").EnumerateArray().ToDictionary(
            user => JsonText(user.GetProperty("AccountId")),
            user => (JsonField(user, "LastName") + " " + JsonField(user, "FirstName")).Trim());
        foreach (var item in items.EnumerateArray())
        {
            var semester = int.Parse(JsonText(item.GetProperty("semester")), CultureInfo.InvariantCulture);
            if (semester is not (1 or 2)) continue;
            var subjectId = JsonText(item.GetProperty("subjectId"));
            subjects.TryGetValue(subjectId, out var subject);
            var title = subjectNames.GetValueOrDefault(JsonField(subject, "numericIdentifier"));
            if (string.IsNullOrEmpty(title)) title = JsonField(subject, "name");
            if (string.IsNullOrEmpty(title)) title = subjectId;
            var mark = JsonField(item, "scaleValue", "value");
            if (string.IsNullOrEmpty(mark)) mark = "OO";
            var teacher = teachers.GetValueOrDefault(JsonField(item, "teacherId"), "");
            var date = JsonField(item, "date");
            date = date[..Math.Min(10, date.Length)];
            var description = $"Ocena: {mark}\nPrzedmiot: {title}\nObszar: {JsonField(item, "area", "name")}\n" +
                $"Data: {date}\nNauczyciel: {teacher}\nTreść oceny: {JsonField(item, "content")}\n" +
                $"Dodał: {teachers.GetValueOrDefault(JsonField(item, "addedBy"), "")}\nKomentarz: {JsonField(item, "comments")}\n";
            semesters[semester - 1].TryAdd(title, []);
            semesters[semester - 1][title].Add(new GradeDescriptive(title, mark, date, "", description, semester, teacher));
        }
        return semesters;
    }

    private static string JsonText(JsonElement value) => value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
        ? "" : value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();

    private static string JsonField(JsonElement value, params string[] names)
    {
        foreach (var name in names)
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(name, out value)) return "";
        return JsonText(value);
    }

    public static GradesResult Parse(string html)
    {
        using var document = Html.Parse(html);
        var rows = Html.Rows(document).Where(r => !r.HasAttribute("id")).ToList();
        if (rows.Count == 0) throw new ParseException("Error in parsing grades.");
        var semesters = new[] { new Dictionary<string, List<Grade>>(), new Dictionary<string, List<Grade>>() };
        var averages = new Dictionary<string, List<Gpa>>();
        var descriptive = new[] { new Dictionary<string, List<GradeDescriptive>>(), new Dictionary<string, List<GradeDescriptive>>() };

        foreach (var row in rows)
        {
            var numericMarker = row.Children.FirstOrDefault(e => Html.Attr(e, "class") == "center micro screen-only");
            if (numericMarker is not null)
            {
                var cells = row.Children.Where(c => c.LocalName == "td" && c != numericMarker).ToList();
                if (cells.Count >= 9)
                {
                    var subject = Html.NoNewlines(cells[0].TextContent).Trim();
                    var averageCells = row.QuerySelectorAll("td.right").Select(e => e.TextContent).ToList();
                    averages.TryAdd(subject, []);
                    for (var sem = 0; sem < 2; sem++)
                    {
                        semesters[sem].TryAdd(subject, []);
                        foreach (var cell in cells.Skip(1 + sem * 3).Take(3))
                        {
                            foreach (var anchor in cell.QuerySelectorAll("span.grade-box > a"))
                            {
                                if (anchor.ParentElement?.ParentElement?.GetAttribute("class") == "center") continue;
                                var info = ExtractGradeInfo(anchor, subject);
                                semesters[sem][subject].Add(new Grade(subject, info.Mark, info.Counts, info.Date, info.Href,
                                    info.Description, sem + 1, info.Category, info.Teacher, info.Weight));
                            }
                        }
                        averages[subject].Add(new Gpa(sem + 1, averageCells.ElementAtOrDefault(sem) ?? "0", subject));
                    }
                    averages[subject].Add(new Gpa(0, averageCells.LastOrDefault() ?? "0", subject));
                }
            }

            var descriptiveMarker = row.Children.FirstOrDefault(e => Html.Attr(e, "class") == "micro center screen-only");
            if (descriptiveMarker is not null)
            {
                var cells = row.Children.Where(c => c.LocalName == "td" && c != descriptiveMarker).ToList();
                if (cells.Count < 3) continue;
                var subject = Html.NoNewlines(cells[0].TextContent).Trim();
                for (var sem = 0; sem < 2; sem++)
                {
                    descriptive[sem].TryAdd(subject, []);
                    foreach (var element in cells[sem + 1].QuerySelectorAll("span.grade-box > a, span.grade-box > span.ocena"))
                    {
                        var gradeTag = element.LocalName == "span" ? element.ParentElement! : element;
                        var info = ExtractGradeInfo(gradeTag, subject);
                        var href = info.Href.Contains("javascript", StringComparison.OrdinalIgnoreCase) ? "" : info.Href;
                        descriptive[sem][subject].Add(new GradeDescriptive(subject, info.Mark, info.Date, href,
                            info.Description, sem + 1, info.Teacher));
                    }
                }
            }
        }

        // Upstream currently describes only the first-semester summary schema.
        for (var i = 0; i + 1 < rows.Count; i++)
        {
            var header = rows[i].QuerySelector("th strong");
            if (header is null) continue;
            var details = header.NextSibling?.TextContent ?? "";
            var date = Regex.Match(details, @"opublikowano: (\S+)");
            var teacher = Regex.Match(details, @"nauczyciel: (.+?)\)");
            if (!date.Success || !teacher.Success) throw new ParseException("Error in descriptive semester summary metadata.");
            var title = header.TextContent.Trim();
            var description = string.Join('\n', rows[i + 1].QuerySelectorAll("p").Select(p => p.TextContent.Trim())).Trim();
            descriptive[0].TryAdd(title, []);
            descriptive[0][title].Add(new GradeDescriptive(title, "", date.Groups[1].Value, "", description, 1, teacher.Groups[1].Value));
            break;
        }
        return new GradesResult(semesters, averages, descriptive);
    }

    private sealed record GradeInfo(string Mark, string Date, string Href, string Description, bool Counts, string Category, string Teacher, int Weight);

    private static GradeInfo ExtractGradeInfo(IElement element, string subject)
    {
        var title = Html.Attr(element, "title");
        var attributes = Html.TitlePairs(title);
        var date = Regex.Match(title, @"Data:\s*(\d{4}-\d{2}-\d{2})");
        if (!date.Success) throw new ParseException("Error in getting grade's date.");
        var mark = Html.NoNewlines(element.TextContent.Replace("\u00a0", "")).Trim();
        // Preserve the description's original formatting, including the upstream <br/> removal.
        var description = $"Ocena: {mark}\nPrzedmiot: {subject}\n" +
            Regex.Replace(title.Replace("<br/>", "").Replace("<br />", "\n"), @"<br*>", "\n");
        return new GradeInfo(mark, date.Groups[1].Value, Html.Attr(element, "href"), description,
            attributes.GetValueOrDefault("Licz do średniej") == "tak", attributes.GetValueOrDefault("Kategoria", ""),
            attributes.GetValueOrDefault("Nauczyciel", ""), Html.Int(attributes.GetValueOrDefault("Waga", "0"), "grade weight"));
    }
}
