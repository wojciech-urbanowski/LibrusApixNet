using System.Net;

namespace LibrusApixNet.Tests;

public sealed class RequestTests
{
    [Theory]
    [InlineData("all", "zmiany_logowanie_wszystkie")]
    [InlineData("week", "zmiany_logowanie_tydzien")]
    [InlineData("last_login", "zmiany_logowanie")]
    public async Task GradeAndAttendanceFiltersSendSourcePayloads(string sortBy, string key)
    {
        var handler = new MockHttp(request => MockHttp.Response(request.Uri.AbsolutePath.Contains("oceny") ? Fixtures.NumericGrades : Fixtures.Attendance));
        using var client = new Client(new Token("a:b"), MockHttp.Options, handler);
        await Grades.GetGradesAsync(client, sortBy); await Attendances.GetAttendanceAsync(client, sortBy);
        Assert.Equal(key + "=1", handler.Requests.First().Body);
        Assert.Equal(key + "=" + (sortBy == "all" ? "" : key), handler.Requests.Last().Body);
        Assert.All(handler.Requests, r => Assert.Equal(HttpMethod.Post, r.Method));
    }

    [Fact]
    public async Task InvalidInputIsRejectedBeforeNetwork()
    {
        var handler = new MockHttp(_ => throw new InvalidOperationException("Unexpected network call"));
        using var client = new Client(new Token("a:b"), MockHttp.Options, handler);
        await Assert.ThrowsAsync<LibrusArgumentException>(() => Grades.GetGradesAsync(client, "invalid"));
        await Assert.ThrowsAsync<LibrusArgumentException>(() => Attendances.GetAttendanceAsync(client, "invalid"));
        await Assert.ThrowsAsync<DateException>(() => Timetable.GetTimetableAsync(client, new DateOnly(2026, 10, 6)));
        await Assert.ThrowsAsync<DateException>(() => HomeworkService.GetHomeworkAsync(client, "bad", "2026-10-09"));
        await Assert.ThrowsAsync<DateException>(() => Schedule.GetScheduleAsync(client, 13, 2026));
        await Assert.ThrowsAsync<LibrusArgumentException>(() => Messages.GetReceivedAsync(client, -1));
        await Assert.ThrowsAsync<DateException>(() => CompletedLessons.GetCompletedAsync(client, "2026-10-10", "2026-10-09"));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task TimetableAndScheduleSendWeekMonthAndYear()
    {
        var handler = new MockHttp(request => MockHttp.Response(request.Uri.AbsolutePath.Contains("plan_lekcji") ? Fixtures.Timetable() : Fixtures.Schedule));
        using var client = new Client(new Token("a:b"), MockHttp.Options, handler);
        await Timetable.GetTimetableAsync(client, new DateOnly(2026, 10, 5)); await Schedule.GetScheduleAsync(client, 10, 2026);
        Assert.Equal("tydzien=2026-10-05_2026-10-11", handler.Requests.First().Body);
        Assert.Equal("rok=2026&miesiac=10", handler.Requests.Last().Body);
    }

    [Fact]
    public async Task HomeworkAndCompletedLessonsSendDateFiltersAndPagination()
    {
        var handler = new MockHttp(request => MockHttp.Response(request.Uri.AbsolutePath.Contains("moje_zadania") ? Fixtures.Homework : Fixtures.Completed));
        using var client = new Client(new Token("a:b"), MockHttp.Options, handler);
        await HomeworkService.GetHomeworkAsync(client, "2026-10-01", "2026-10-09");
        await CompletedLessons.GetCompletedAsync(client, "2026-10-01", "2026-10-09", 2);
        Assert.Equal("dataOd=2026-10-01&dataDo=2026-10-09&przedmiot=-1&status=-1", handler.Requests.First().Body);
        Assert.Equal("data1=2026-10-01&data2=2026-10-09&filtruj_id_przedmiotu=-1&numer_strony1001=2&porcjowanie_pojemnik1001=1001", handler.Requests.Last().Body);
    }

    [Fact]
    public async Task MessagePagesAndContentUseCorrectRoutes()
    {
        var handler = new MockHttp(request => MockHttp.Response(request.Uri.AbsolutePath.EndsWith("/42") ? Fixtures.Content :
            request.Uri.AbsolutePath.EndsWith("/6") ? Fixtures.Sent : Fixtures.Received));
        using var client = new Client(new Token("a:b"), MockHttp.Options, handler);
        Assert.Equal(2, (await Messages.GetReceivedAsync(client, 2)).Count);
        Assert.Single(await Messages.GetSentAsync(client, 1));
        Assert.Equal("Spotkanie", (await Messages.MessageContentAsync(client, "42")).Title);
        Assert.Equal("numer_strony105=2&porcjowanie_pojemnik105=105", handler.Requests.First().Body);
        Assert.Equal("/wiadomosci/1/5/42", handler.Requests.Last().Uri.AbsolutePath);
    }

    [Fact]
    public async Task PaginationPreservesDifferentSourceConventions()
    {
        using var client = MockHttp.Create(_ => MockHttp.Response("<div class='pagination'><span>1&nbsp;z&nbsp;3</span></div>"));
        Assert.Equal(2, await Messages.GetMaxPageNumberAsync(client));
        Assert.Equal(3, await CompletedLessons.GetMaxPageNumberAsync(client, "2026-10-01", "2026-10-09"));
    }

    [Fact]
    public async Task RecipientGroupsAndRecipientsUseSourceNamesAndValues()
    {
        var handler = new MockHttp(request => MockHttp.Response(request.Uri.AbsolutePath == "/getRecipients" ?
            "<label for='recipient_123'>Anna&nbsp;Kowalska</label>" : "<table class='message-recipients'><tr><td><input class='recipiantTypeRadio' value='teachers'></td></tr></table>"));
        using var client = new Client(new Token("a:b"), MockHttp.Options, handler);
        Assert.Equal("teachers", Assert.Single(await Messages.RecipientGroupsAsync(client)));
        Assert.Equal("123", (await Messages.GetRecipientsAsync(client, "teachers"))["AnnaKowalska"]);
        Assert.Contains("czyWirtualneKlasy=False", handler.Requests.Last().Body);
        Assert.Contains("typAdresata=teachers", handler.Requests.Last().Body);
    }

    [Theory]
    [InlineData("Wiadomość została wysłana", HttpStatusCode.OK, true)]
    [InlineData("Wiadomość nie zostala wysłana", HttpStatusCode.OK, false)]
    [InlineData("Wiadomość nie została wysłana", HttpStatusCode.OK, false)]
    [InlineData("Błąd", HttpStatusCode.BadRequest, false)]
    public async Task SendingMessagesUsesRepeatedRecipientKeysAndHttpResult(string text, HttpStatusCode status, bool success)
    {
        var handler = new MockHttp(_ => MockHttp.Response("<div class='container-background'><p>" + text + "</p></div>", status));
        using var client = new Client(new Token("a:b"), MockHttp.Options, handler);
        var result = await Messages.SendMessageAsync(client, "Zażółć", "Cześć & +", ["10", "20"]);
        Assert.Equal(success, result.Success); Assert.Equal(text, result.Message);
        var body = Assert.Single(handler.Requests).Body;
        Assert.Contains("DoKogo=10&DoKogo=20", body); Assert.Contains("tresc=Cze%C5%9B%C4%87+%26+%2B", body);
    }

    [Fact]
    public async Task DetailAndSimpleGetMethodsUseConfiguredEndpoints()
    {
        var handler = new MockHttp(request => MockHttp.Response(request.Uri.AbsolutePath switch
        {
            "/ogloszenia" => Fixtures.Announcements, "/informacja" => Fixtures.Student,
            "/moje_zadania/podglad/77" => Fixtures.HomeworkDetails,
            "/terminarz/dodane_od_ostatniego_logowania" => Fixtures.Recent, _ => Fixtures.Details
        }));
        using var client = new Client(new Token("a:b"), MockHttp.Options, handler);
        Assert.Single(await Announcements.GetAnnouncementsAsync(client));
        Assert.Equal("Jan Nowak", (await StudentInformationService.GetStudentInformationAsync(client)).Name);
        Assert.Equal("Anna", (await Attendances.GetDetailAsync(client, "22"))["Nauczyciel"]);
        Assert.Equal("Ćwiczenia 1–5", (await HomeworkService.HomeworkDetailAsync(client, "77"))["Opis zadania"]);
        Assert.Equal("Ułamki", (await Schedule.ScheduleDetailAsync(client, "sprawdzian", "99"))["Opis"]);
        Assert.Single(await Schedule.GetRecentlyAddedScheduleAsync(client));
        Assert.Contains(handler.Requests, r => r.Uri.AbsolutePath == "/terminarz/sprawdzian/99");
        Assert.All(handler.Requests, r => Assert.Equal(HttpMethod.Get, r.Method));
    }
}
