namespace LibrusApixNet;

public sealed record StudentInformation(string Name, string ClassName, int Number, string Tutor, string School, int? LuckyNumber);

public static class StudentInformationService
{
    public static async Task<StudentInformation> GetStudentInformationAsync(Client client, CancellationToken cancellationToken = default) =>
        Parse(await client.GetTextAsync(client.Options.InfoUrl, cancellationToken));

    public static StudentInformation Parse(string html)
    {
        using var document = Html.Parse(html);
        int? lucky = int.TryParse(Html.Text(document.QuerySelector("span.luckyNumber > b")), out var n) ? n : null;
        var table = Html.Require(document, "table.decorated.big.center > tbody", "Error while parsing student information table.");
        var data = Html.Rows(table).Take(5).Select(row => Html.Text(row.QuerySelector("td")).Trim()).ToList();
        if (data.Count < 5) throw new ParseException("Error while retrieving student information.");
        return new StudentInformation(data[0], data[1], Html.Int(data[2], "student number"), data[3],
            string.Join('\n', data[4].Split('\n').Select(s => s.Trim())), lucky);
    }
}
