namespace LibrusApixNet;

public sealed record MessageData(string Author, string Title, string Content, string Date);
public sealed record Message(string Author, string Title, string Date, string Href, bool Unread, bool HasAttachment);
public sealed record SendMessageResult(bool Success, string Message);

public static class Messages
{
    public static async Task<List<string>> RecipientGroupsAsync(Client client, CancellationToken cancellationToken = default)
    {
        using var document = Html.Parse(await client.GetTextAsync(client.Options.RecipientGroupsUrl, cancellationToken));
        return document.QuerySelectorAll("table.message-recipients > tbody > tr")
            .Select(row => Html.Attr(Html.Require(row, "input.recipiantTypeRadio", "Error getting recipient group radio."), "value")).ToList();
    }

    public static async Task<Dictionary<string, string>> GetRecipientsAsync(Client client, string group, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, string>
        {
            ["typAdresata"] = group, ["poprzednia"] = "5", ["tabZaznaczonych"] = "", ["czyWirtualneKlasy"] = "False", ["idGrupy"] = "0"
        };
        using var document = Html.Parse(await client.PostTextAsync(client.Options.RecipientsUrl, data, cancellationToken));
        var recipients = new Dictionary<string, string>();
        foreach (var label in document.QuerySelectorAll("label"))
            recipients[label.TextContent.Replace("\u00a0", "")] = Html.Attr(label, "for", "_").Split('_')[^1];
        return recipients;
    }

    public static async Task<SendMessageResult> SendMessageAsync(Client client, string title, string content,
        IReadOnlyList<string> recipientIds, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, string>
        {
            ["filtrUzytkownikow"] = "0", ["idPojemnika"] = "", ["Rodzaj"] = "0", ["temat"] = title,
            ["tresc"] = content, ["poprzednia"] = "5", ["fileStorageIdentifier"] = "", ["wyslij"] = "Wyślij"
        }.ToList();
        data.AddRange(recipientIds.Select(id => new KeyValuePair<string, string>("DoKogo", id)));
        using var response = await client.PostAsync(client.Options.SendMessageUrl, data, cancellationToken);
        using var document = Html.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var message = Html.Require(document, "div.container-background > p", "Error getting the result of the message.").TextContent;
        var failed = message.Contains("nie zostala", StringComparison.OrdinalIgnoreCase) || message.Contains("nie została", StringComparison.OrdinalIgnoreCase);
        return new SendMessageResult(response.StatusCode == System.Net.HttpStatusCode.OK && !failed, message);
    }

    public static async Task<MessageData> MessageContentAsync(Client client, string contentUrl, CancellationToken cancellationToken = default) =>
        ParseContent(await client.GetTextAsync(client.Options.MessageUrl + "/" + contentUrl, cancellationToken));

    public static MessageData ParseContent(string html)
    {
        using var document = Html.Parse(html);
        var table = Html.Require(document, "table.stretch", "Error in parsing message data.");
        var rows = table.QuerySelectorAll("tr");
        if (rows.Length < 3) throw new ParseException("Not enough values to unpack from message data.");
        var content = Html.Require(document, "div.container-message-content", "Error in parsing message content.");
        return new MessageData(Html.Text(rows[0].QuerySelector("td[class='left']")), Html.Text(rows[1].QuerySelector("td[class='left']")),
            content.TextContent, Html.Text(rows[2].QuerySelector("td[class='left']")));
    }

    public static List<Message> Parse(string html) => ParseMessages(html, false);
    public static List<Message> ParseSent(string html) => ParseMessages(html, true);

    private static List<Message> ParseMessages(string html, bool sent)
    {
        using var document = Html.Parse(html);
        var table = Html.Require(document, "table.decorated.stretch > tbody", "Error in parsing messages table/body.");
        var rows = Html.Rows(table);
        if (rows.Count == 0 || rows[0].TextContent.Trim() == "Brak wiadomości") return [];
        return rows.Select(row =>
        {
            var cells = row.QuerySelectorAll("td");
            if (cells.Length < (sent ? 7 : 6)) throw new ParseException("Message data has too few elements.");
            var href = Html.Attr(cells[2].QuerySelector("a"), "href").Split('/');
            var unread = sent ? cells[5].TextContent.Trim() == "NIE" : Html.Attr(cells[3], "style").Contains("font-weight: bold", StringComparison.Ordinal);
            return new Message(cells[2].TextContent, cells[3].TextContent, cells[4].TextContent, href.Length > 4 ? href[4] : "", unread,
                cells[1].QuerySelector("img") is not null);
        }).ToList();
    }

    public static async Task<int> GetMaxPageNumberAsync(Client client, CancellationToken cancellationToken = default)
    {
        using var document = Html.Parse(await client.GetTextAsync(client.Options.MessageUrl, cancellationToken));
        return Math.Max(0, Html.PageCount(document) - 1);
    }

    public static Task<List<Message>> GetReceivedAsync(Client client, int page = 0, CancellationToken cancellationToken = default) =>
        GetPageAsync(client, page, false, cancellationToken);
    public static Task<List<Message>> GetSentAsync(Client client, int page = 0, CancellationToken cancellationToken = default) =>
        GetPageAsync(client, page, true, cancellationToken);

    private static async Task<List<Message>> GetPageAsync(Client client, int page, bool sent, CancellationToken ct)
    {
        if (page < 0) throw new LibrusArgumentException("Page must not be negative.");
        var html = await client.PostTextAsync(sent ? client.Options.SendMessageUrl : client.Options.MessageUrl,
            new Dictionary<string, string> { ["numer_strony105"] = page.ToString(System.Globalization.CultureInfo.InvariantCulture), ["porcjowanie_pojemnik105"] = "105" }, ct);
        return ParseMessages(html, sent);
    }
}
