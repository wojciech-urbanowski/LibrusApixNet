using System.Net;
using System.Text.Json;

namespace LibrusApixNet.Tests;

public sealed class GatewayGradeTests
{
    private const string GatewayHtml = """
        <table class="newSchemaGradesByStudentTable"><tr class="studentRow line1" data-subject_id="7">
        <td class="micro center screen-only"></td><td>Matematyka</td>
        <td class="gradesCell" data-semester="1"></td><td class="gradesCell" data-semester="2"></td>
        </tr></table>
        """;

    private const string GatewayItems = """
        {"data":[
          {"semester":1,"subjectId":"subject-1","scaleValue":{"value":"5"},"teacherId":"teacher-1",
           "date":"2026-10-10T08:00:00","area":{"name":"Komunikacja"},"content":"Opis oceny",
           "addedBy":"teacher-1","comments":"Komentarz"},
          {"semester":"2","subjectId":"subject-1","scaleValue":null,"date":"2026-10-11"},
          {"semester":0,"subjectId":"subject-1"},
          {"semester":3,"subjectId":"subject-1"}
        ]}
        """;

    private static HttpResponseMessage GatewayResponse(RecordedRequest request, string html = GatewayHtml,
        string items = GatewayItems, string userInfo = "{\"IdentifierOfStudentAssignedWithUser\":\"student-1\"}")
    {
        if (request.Uri.AbsolutePath == "/refreshToken")
        {
            var refreshed = MockHttp.Response("{}");
            refreshed.Headers.Add("Set-Cookie", "oauth_token=fresh; Path=/; Secure");
            return refreshed;
        }
        return MockHttp.Response(request.Uri.AbsolutePath switch
        {
            "/przegladaj_oceny/uczen" => html,
            "/gateway/api/2.0/Auth/TokenInfo" or "/custom/token" => "{\"UserIdentifier\":\"user-1\"}",
            "/gateway/api/2.0/Auth/UserInfo/user-1" or "/custom/user/user-1" => userInfo,
            "/gateway/api/2.0/Auth/DescriptiveGradingSystem/PartialGrades/Student/student-1" or
                "/gateway/api/2.0/Auth/DescriptiveGradingSystem/PartialGrades/Student/user-1" or
                "/custom/grades/student-1" => items,
            "/gateway/api/3.0/Auth/Subjects" or "/custom/subjects" => "{\"data\":[{\"identifier\":\"subject-1\",\"numericIdentifier\":7,\"name\":\"API subject\"}]}",
            "/gateway/api/2.0/Users" or "/custom/users" => "{\"Users\":[{\"AccountId\":\"teacher-1\",\"LastName\":\"Kowalska\",\"FirstName\":\"Anna\"}]}",
            _ => throw new InvalidOperationException("Unexpected gateway endpoint: " + request.Uri.AbsolutePath)
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NewSchemaResolvesStudentSubjectsTeachersAndBothSemesters(bool overrideUrls)
    {
        var options = MockHttp.Options;
        if (overrideUrls)
            options = options with
            {
                GatewayApiTokenInfoUrl = options.BaseUrl + "/custom/token",
                GatewayApiUserInfoUrl = options.BaseUrl + "/custom/user/",
                GatewayApiDescriptiveGradesUrl = options.BaseUrl + "/custom/grades/",
                GatewayApiSubjectsUrl = options.BaseUrl + "/custom/subjects",
                GatewayApiUsersUrl = options.BaseUrl + "/custom/users"
            };
        var handler = new MockHttp(request => GatewayResponse(request));
        using var client = new Client(new Token("a:b"), options, handler);
        var result = await Grades.GetGradesAsync(client);
        var first = Assert.Single(result.Descriptive[0]["Matematyka"]);
        Assert.Equal("5", first.Mark);
        Assert.Equal("2026-10-10", first.Date);
        Assert.Equal("Kowalska Anna", first.Teacher);
        Assert.Equal(1, first.Semester);
        Assert.Equal("", first.Href);
        Assert.Equal("Ocena: 5\nPrzedmiot: Matematyka\nObszar: Komunikacja\nData: 2026-10-10\nNauczyciel: Kowalska Anna\nTreść oceny: Opis oceny\nDodał: Kowalska Anna\nKomentarz: Komentarz\n", first.Description);
        var second = Assert.Single(result.Descriptive[1]["Matematyka"]);
        Assert.Equal("OO", second.Mark);
        Assert.Equal("", second.Teacher);
        var post = Assert.Single(handler.Requests, request => request.Uri.AbsolutePath.EndsWith("student-1", StringComparison.Ordinal));
        Assert.Equal(HttpMethod.Post, post.Method);
        Assert.Equal("{}", post.Body);
        Assert.StartsWith("application/json", post.ContentType);
        Assert.Contains("oauth_token=fresh", post.Cookie);
    }

    [Fact]
    public async Task MissingAssignedStudentFallsBackToAuthenticatedUser()
    {
        var handler = new MockHttp(request => GatewayResponse(request, userInfo: "{}"));
        using var client = new Client(new Token("a:b"), MockHttp.Options, handler);
        Assert.Single((await Grades.GetGradesAsync(client)).Descriptive[0]["Matematyka"]);
        Assert.Contains(handler.Requests, request => request.Method == HttpMethod.Post && request.Uri.AbsolutePath.EndsWith("/Student/user-1", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("{\"data\":[]}")]
    [InlineData("{\"data\":null}")]
    [InlineData("{}")]
    public async Task EmptyGatewayGradesDoNotRequestSubjectsOrTeachers(string items)
    {
        var handler = new MockHttp(request => GatewayResponse(request, items: items));
        using var client = new Client(new Token("a:b"), MockHttp.Options, handler);
        var result = await Grades.GetGradesAsync(client);
        Assert.Empty(result.Descriptive[0]["Matematyka"]);
        Assert.Equal(5, handler.Requests.Count);
        Assert.DoesNotContain(handler.Requests, request => request.Uri.AbsolutePath.EndsWith("/Subjects", StringComparison.Ordinal) || request.Uri.AbsolutePath.EndsWith("/Users", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExistingHtmlDescriptiveGradesArePreserved()
    {
        var html = Fixtures.NewDescriptive.Replace("Przedmiot", "Matematyka") + GatewayHtml;
        var handler = new MockHttp(request => GatewayResponse(request, html));
        using var client = new Client(new Token("a:b"), MockHttp.Options, handler);
        var result = await Grades.GetGradesAsync(client);
        Assert.Equal("6", Assert.Single(result.Descriptive[0]["Matematyka"]).Mark);
        Assert.Equal("OO", Assert.Single(result.Descriptive[1]["Matematyka"]).Mark);
    }

    [Theory]
    [InlineData("{\"data\":[{\"semester\":1,\"subjectId\":\"subject-1\"}]}", "API subject")]
    [InlineData("{\"data\":[{\"semester\":1,\"subjectId\":\"missing-subject\"}]}", "missing-subject")]
    public async Task MissingHtmlNamesFallBackToApiSubjectNameOrIdentifier(string items, string expectedTitle)
    {
        var html = GatewayHtml.Replace("data-subject_id=\"7\"", "data-subject_id=\"99\"");
        var handler = new MockHttp(request => GatewayResponse(request, html, items));
        using var client = new Client(new Token("a:b"), MockHttp.Options, handler);
        Assert.Equal(expectedTitle, Assert.Single((await Grades.GetGradesAsync(client)).Descriptive[0][expectedTitle]).Title);
    }

    [Theory]
    [InlineData("not-json", HttpStatusCode.OK)]
    [InlineData("{}", HttpStatusCode.OK)]
    [InlineData("{}", HttpStatusCode.ServiceUnavailable)]
    public async Task OptionalGatewayFailuresPreserveNumericHtmlGrades(string body, HttpStatusCode status)
    {
        var html = Fixtures.NumericGrades + GatewayHtml;
        var handler = new MockHttp(request => request.Uri.AbsolutePath.EndsWith("TokenInfo", StringComparison.Ordinal)
            ? MockHttp.Response(body, status) : GatewayResponse(request, html));
        using var client = new Client(new Token("a:b"), MockHttp.Options, handler);
        var result = await Grades.GetGradesAsync(client);
        Assert.Equal(5.5, result.Semesters[0]["Matematyka"][0].Value);
        Assert.Empty(result.Descriptive[0]["Matematyka"]);
    }

    [Theory]
    [InlineData("/gateway/api/2.0/Auth/DescriptiveGradingSystem/PartialGrades/Student/student-1", "{}", HttpStatusCode.InternalServerError)]
    [InlineData("/gateway/api/2.0/Auth/DescriptiveGradingSystem/PartialGrades/Student/student-1", "not-json", HttpStatusCode.OK)]
    [InlineData("/gateway/api/3.0/Auth/Subjects", "{\"data\":[\"unexpected\"]}", HttpStatusCode.OK)]
    [InlineData("/gateway/api/2.0/Users", "{\"Users\":{\"unexpected\":1}}", HttpStatusCode.OK)]
    public async Task MalformedOrFailedOptionalGatewayResponsesKeepHtmlData(string path, string body, HttpStatusCode status)
    {
        var handler = new MockHttp(request => request.Uri.AbsolutePath == path
            ? MockHttp.Response(body, status) : GatewayResponse(request, Fixtures.NumericGrades + GatewayHtml));
        using var client = new Client(new Token("a:b"), MockHttp.Options, handler);
        var result = await Grades.GetGradesAsync(client);
        Assert.Equal(5.5, result.Semesters[0]["Matematyka"][0].Value);
        Assert.Empty(result.Descriptive[0]["Matematyka"]);
    }

    [Fact]
    public async Task OptionalGatewayTransportFailureKeepsHtmlData()
    {
        var handler = new MockHttp(request => request.Uri.AbsolutePath.Contains("/PartialGrades/", StringComparison.Ordinal)
            ? throw new HttpRequestException("Synthetic connection failure.")
            : GatewayResponse(request, Fixtures.NumericGrades + GatewayHtml));
        using var client = new Client(new Token("a:b"), MockHttp.Options, handler);
        Assert.Equal(5.5, (await Grades.GetGradesAsync(client)).Semesters[0]["Matematyka"][0].Value);
    }

    [Fact]
    public async Task OptionalGatewayTimeoutWithoutCallerCancellationKeepsHtmlData()
    {
        var handler = new MockHttp(request => request.Uri.AbsolutePath.Contains("/PartialGrades/", StringComparison.Ordinal)
            ? throw new TaskCanceledException("Synthetic HTTP timeout.")
            : GatewayResponse(request, Fixtures.NumericGrades + GatewayHtml));
        using var client = new Client(new Token("a:b"), MockHttp.Options, handler);
        Assert.Equal(5.5, (await Grades.GetGradesAsync(client)).Semesters[0]["Matematyka"][0].Value);
    }

    [Fact]
    public async Task LegacyPagesDoNotRequestGatewayData()
    {
        var handler = new MockHttp(_ => MockHttp.Response(Fixtures.NumericGrades));
        using var client = new Client(new Token("a:b"), MockHttp.Options, handler);
        Assert.Equal(5.5, (await Grades.GetGradesAsync(client)).Semesters[0]["Matematyka"][0].Value);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task CallerCancellationIsNotSwallowedByOptionalGatewayFallback()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new MockHttp(async (request, cancellationToken) =>
        {
            if (request.Uri.AbsolutePath.EndsWith("TokenInfo", StringComparison.Ordinal))
            {
                entered.SetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            return GatewayResponse(request);
        });
        using var client = new Client(new Token("a:b"), MockHttp.Options, handler);
        using var cancellation = new CancellationTokenSource();
        var operation = Grades.GetGradesAsync(client, cancellationToken: cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
    }

    [Fact]
    public async Task JsonTransportSupportsPayloadAndDoesNotChangeFormTransport()
    {
        var handler = new MockHttp(_ => MockHttp.Response("{}"));
        using var client = new Client(new Token("a:b"), MockHttp.Options, handler);
        using var jsonResponse = await client.PostJsonAsync(client.Options.InfoUrl, new { message = "Zażółć", amount = 2 });
        using var formResponse = await client.PostAsync(client.Options.InfoUrl, new Dictionary<string, string> { ["message"] = "Cześć" });
        var requests = handler.Requests.ToArray();
        Assert.StartsWith("application/json", requests[0].ContentType);
        using var json = JsonDocument.Parse(requests[0].Body);
        Assert.Equal("Zażółć", json.RootElement.GetProperty("message").GetString());
        Assert.Equal(2, json.RootElement.GetProperty("amount").GetInt32());
        Assert.Contains("DZIENNIKSID=a", requests[0].Cookie);
        Assert.StartsWith("application/x-www-form-urlencoded", requests[1].ContentType);
        Assert.Equal("message=Cze%C5%9B%C4%87", requests[1].Body);
    }
}
