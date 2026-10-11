namespace LibrusApixNet;

public sealed record Homework(string Lesson, string Teacher, string Subject, string Category, string TaskDate, string CompletionDate, string Href);

public static class HomeworkService
{
    public static async Task<List<Homework>> GetHomeworkAsync(Client client, string dateFrom, string dateTo, CancellationToken cancellationToken = default)
    {
        Html.ValidateDates(dateFrom, dateTo);
        return Parse(await client.PostTextAsync(client.Options.HomeworkUrl,
            new Dictionary<string, string> { ["dataOd"] = dateFrom, ["dataDo"] = dateTo, ["przedmiot"] = "-1", ["status"] = "-1" }, cancellationToken));
    }

    public static async Task<Dictionary<string, string>> HomeworkDetailAsync(Client client, string detailUrl, CancellationToken cancellationToken = default) =>
        ParseDetail(await client.GetTextAsync(client.Options.HomeworkDetailsUrl + detailUrl, cancellationToken));

    public static List<Homework> Parse(string html)
    {
        using var document = Html.Parse(html);
        var table = document.QuerySelector("table.decorated.myHomeworkTable");
        if (table is null)
        {
            if (document.QuerySelector("p.msgEmptyTable") is not null) return [];
            throw new ParseException("Error in parsing homework.");
        }
        return Html.Rows(table).Select(row =>
        {
            var cells = row.QuerySelectorAll("td").Select(td => Html.NoNewlines(td.TextContent)).ToList();
            if (cells.Count < 8) throw new ParseException("Homework has less than 8 elements.");
            var href = Html.PopupId(Html.Attr(row.QuerySelector("input"), "onclick"));
            return new Homework(cells[0], cells[1], cells[2], cells[3], cells[4] + " " + cells[5], cells[6] + " " + cells[7], href);
        }).ToList();
    }

    public static Dictionary<string, string> ParseDetail(string html)
    {
        using var document = Html.Parse(html);
        var container = Html.Require(document, "div.container-background", "Error in parsing homework details.");
        var result = new Dictionary<string, string>();
        foreach (var row in Html.Rows(container))
        {
            var cells = row.QuerySelectorAll("td");
            if (cells.Length >= 2) result[cells[0].TextContent.Replace('\u00a0', ' ')] = cells[1].TextContent.Replace('\u00a0', ' ');
        }
        return result;
    }
}
