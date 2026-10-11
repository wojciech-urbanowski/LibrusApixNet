using System.Net;

namespace LibrusApixNet.Tests;

public sealed class AttendanceTests
{
    private const string Gateway = """
        {"Attendances":[
          {"Type":{"Id":100},"LessonNo":1,"Semester":1},
          {"Type":{"Id":"2"},"LessonNo":2,"Semester":1},
          {"Type":{"Id":3},"LessonNo":3,"Semester":1},
          {"Type":{"Id":1266},"LessonNo":1,"Semester":2},
          {"Type":{"Id":1},"LessonNo":2,"Semester":2},
          {"Type":{"Id":4},"LessonNo":3,"Semester":2}]}
        """;

    [Fact]
    public async Task GatewayRefreshesOAuthOnceAndCalculatesFractions()
    {
        var handler = new MockHttp(request =>
        {
            var response = MockHttp.Response(Gateway);
            if (request.Uri.AbsolutePath == "/refreshToken") response.Headers.Add("Set-Cookie", "oauth_token=fresh; Path=/; Secure");
            return response;
        });
        using var client = new Client(new Token("a:b"), MockHttp.Options, handler);
        var attendance = await Attendances.GetGatewayAttendanceAsync(client);
        Assert.Equal("ob", attendance[0].Type.Short); Assert.Equal("Obecność", attendance[0].Type.Name);
        Assert.Equal(3, attendance[2].LessonNumber);
        var frequency = await Attendances.GetAttendanceFrequencyAsync(client);
        Assert.Equal(2d / 3, frequency.FirstSemester); Assert.Equal(1d / 3, frequency.SecondSemester); Assert.Equal(0.5, frequency.Overall);
        Assert.Single(handler.Requests, r => r.Uri.AbsolutePath == "/refreshToken");
        Assert.All(handler.Requests.Where(r => r.Uri.AbsolutePath != "/refreshToken"), r => Assert.Contains("oauth_token=fresh", r.Cookie));
    }

    [Fact]
    public void EmptyGatewayAttendanceMeansFullFrequency() =>
        Assert.Equal(new AttendanceFrequency(1, 1, 1), Attendances.CalculateFrequency([]));

    [Fact]
    public async Task SuppliedSubjectCountsUsePercentagesWithoutNetwork()
    {
        using var client = MockHttp.Create(_ => throw new InvalidOperationException("Unexpected network call"));
        var counts = new Dictionary<string, Dictionary<string, int>>
        {
            ["Math"] = new() { ["ob"] = 2, ["sp"] = 1, ["nb"] = 1, ["u"] = 1, ["zw"] = 1, ["wy"] = 9 },
            ["History"] = new() { ["ob"] = 1, ["nb"] = 2 }, ["Empty"] = []
        };
        var frequency = await Attendances.GetSubjectFrequencyAsync(client, counts);
        Assert.Equal(50, frequency["Math"]); Assert.Equal(33.33, frequency["History"]); Assert.Equal(100, frequency["Empty"]);
    }

    [Fact]
    public async Task SubjectAttendanceFiltersDatesAndCachesLessonsAndSubjects()
    {
        const string records = """
            {"Attendances":[
             {"Date":"2026-10-01","Lesson":{"Id":10},"Type":{"Id":100}},
             {"Date":"2026-10-09","Lesson":{"Id":10},"Type":{"Id":2}},
             {"Date":"2026-10-09","Lesson":{"Id":20},"Type":{"Id":1}},
             {"Date":"2026-10-10","Lesson":{"Id":30},"Type":{"Id":1}}]}
            """;
        var handler = new MockHttp(request =>
        {
            var response = MockHttp.Response(request.Uri.AbsolutePath switch
            {
                "/gateway/api/2.0/Attendances" => records,
                "/gateway/api/2.0/Lessons/10" or "/gateway/api/2.0/Lessons/20" => "{\"Lesson\":{\"Subject\":{\"Id\":100}}}",
                "/gateway/api/2.0/Subjects/100" => "{\"Subject\":{\"Name\":\"Matematyka\"}}",
                "/refreshToken" => "{}", _ => throw new InvalidOperationException("Unexpected endpoint: " + request.Uri)
            });
            if (request.Uri.AbsolutePath == "/refreshToken") response.Headers.Add("Set-Cookie", "oauth_token=fresh; Path=/");
            return response;
        });
        using var client = new Client(new Token("a:b"), MockHttp.Options, handler);
        var counts = await Attendances.GetSubjectAttendanceAsync(client, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 9));
        Assert.Equal(1, counts["Matematyka"]["ob"]); Assert.Equal(1, counts["Matematyka"]["sp"]); Assert.Equal(1, counts["Matematyka"]["nb"]);
        Assert.Single(handler.Requests, r => r.Uri.AbsolutePath.EndsWith("Lessons/10"));
        Assert.Single(handler.Requests, r => r.Uri.AbsolutePath.Contains("Subjects/"));
        Assert.DoesNotContain(handler.Requests, r => r.Uri.AbsolutePath.EndsWith("Lessons/30"));
        Assert.Equal(66.67, Attendances.CalculateSubjectFrequency(counts)["Matematyka"]);
    }

    [Fact]
    public async Task SubjectRequestsRetryTransientFailure()
    {
        var attempts = 0;
        var handler = new MockHttp(request =>
        {
            var response = MockHttp.Response(request.Uri.AbsolutePath switch
            {
                "/refreshToken" => "{}",
                "/gateway/api/2.0/Attendances" => "{\"Attendances\":[{\"Lesson\":{\"Id\":1},\"Type\":{\"Id\":100}}]}",
                "/gateway/api/2.0/Lessons/1" => "{\"Lesson\":{\"Subject\":{\"Id\":1}}}",
                _ => "{\"Subject\":{\"Name\":\"Math\"}}"
            });
            if (request.Uri.AbsolutePath == "/refreshToken") response.Headers.Add("Set-Cookie", "oauth_token=fresh; Path=/");
            if (request.Uri.AbsolutePath.Contains("Lessons/") && Interlocked.Increment(ref attempts) == 1) response.StatusCode = HttpStatusCode.ServiceUnavailable;
            return response;
        });
        using var client = new Client(new Token("a:b"), MockHttp.Options, handler);
        Assert.Equal(100, (await Attendances.GetSubjectFrequencyAsync(client))["Math"]);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task AuthorizationFailureIsNotRetried()
    {
        var handler = new MockHttp(request =>
        {
            var response = MockHttp.Response("{}");
            if (request.Uri.AbsolutePath == "/refreshToken") response.Headers.Add("Set-Cookie", "oauth_token=fresh; Path=/");
            else if (request.Uri.AbsolutePath.EndsWith("Attendances")) response.Content = MockHttp.Response("{\"Attendances\":[{\"Lesson\":{\"Id\":1},\"Type\":{\"Id\":100}}]}").Content;
            else response.StatusCode = HttpStatusCode.Unauthorized;
            return response;
        });
        using var client = new Client(new Token("a:b"), MockHttp.Options, handler);
        await Assert.ThrowsAsync<HttpRequestException>(() => Attendances.GetSubjectAttendanceAsync(client));
        Assert.Single(handler.Requests, r => r.Uri.AbsolutePath.Contains("Lessons/"));
    }
}
