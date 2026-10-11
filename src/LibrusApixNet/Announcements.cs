namespace LibrusApixNet;

public sealed record Announcement(string Title = "", string Author = "", string Description = "", string Date = "");

public static class Announcements
{
    public static async Task<List<Announcement>> GetAnnouncementsAsync(Client client, CancellationToken cancellationToken = default) =>
        Parse(await client.GetTextAsync(client.Options.AnnouncementsUrl, cancellationToken));

    public static List<Announcement> Parse(string html)
    {
        using var document = Html.Parse(html);
        if (document.QuerySelector("div.container.border-red.resizeable.center > div > p") is not null) return [];
        var tables = document.QuerySelectorAll("table.decorated.big.center.printable.margin-top");
        if (tables.Length == 0) throw new ParseException("Error in parsing announcements.");
        return tables.Select(table =>
        {
            var data = Html.Rows(table).Select(row => Html.Text(row.QuerySelector("td")).Trim()).ToList();
            if (data.Count != 3) throw new ParseException($"Expected 3 items in announcement data, got {data.Count}.");
            return new Announcement(Html.Text(table.QuerySelector("thead > tr > td")), data[0], data[2], data[1]);
        }).ToList();
    }
}
