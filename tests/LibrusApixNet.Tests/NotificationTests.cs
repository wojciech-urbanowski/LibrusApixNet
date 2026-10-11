using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LibrusApixNet.Tests;

public sealed class NotificationTests
{
    // Different order from Python NotificationData fields, to exercise destination-based routing.
    private const string Menu = """
        <div id="graphic-menu"><ul>
        <li><a href="/wiadomosci">Wiadomości</a><a class="button counter">1</a></li>
        <li><a href="/moje_zadania">Zadania</a><a class="button counter">0</a></li>
        <li><a href="/przegladaj_oceny/uczen">Oceny</a><a class="button counter">1</a></li>
        <li><a href="/terminarz">Terminarz</a><a class="button counter">1</a></li>
        <li><a href="/ogloszenia">Ogłoszenia</a><a class="button counter">0</a></li>
        <li><a href="/przegladaj_nb/uczen">Nieobecności</a><a class="button counter">2</a></li>
        <li><a href="javascript:doSomething()">Inne</a></li>
        </ul></div>
        """;

    [Fact]
    public void AmountsIncludeOnlySupportedDestinations()
    {
        var amounts = Notifications.ParseAmounts(Menu);
        Assert.Equal(6, amounts.Count); Assert.Equal("/wiadomosci", amounts[0].Destination); Assert.Equal(2, amounts[^1].Amount);
        Assert.Equal(0, Notifications.ParseAmounts("<div id='graphic-menu'><ul><li><a href='/wiadomosci'>Wiadomości</a><a class='button counter'>?</a></li></ul></div>")[0].Amount);
    }

    [Fact]
    public void FilteringIsIdempotentAndDoesNotMutateInputState()
    {
        var grade = Grades.Parse(Fixtures.NumericGrades).Semesters[0]["Matematyka"][0];
        var attendance = Attendances.Parse(Fixtures.Attendance)[0][0];
        var message = Messages.Parse(Fixtures.Received)[0];
        var announcement = Announcements.Parse(Fixtures.Announcements)[0];
        var schedule = Schedule.ParseRecentlyAdded(Fixtures.Recent)[0];
        var homework = HomeworkService.Parse(Fixtures.Homework)[0];
        var data = new NotificationData
        {
            Grades = [grade, grade], Attendance = [attendance], Messages = [message], Announcements = [announcement], Schedule = [schedule], Homework = [homework]
        };
        var seen = new NotificationIds();
        var first = Notifications.FilterNew(data, seen);
        Assert.Single(first.Data.Grades); Assert.Single(first.Data.Attendance); Assert.Single(first.Data.Messages);
        Assert.Single(first.Data.Announcements); Assert.Single(first.Data.Schedule); Assert.Single(first.Data.Homework);
        Assert.Empty(seen.Grades); Assert.Empty(seen.Messages);
        Assert.Equal(Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(schedule.Data))), Assert.Single(first.Seen.Schedule));
        var again = Notifications.FilterNew(data, first.Seen);
        Assert.Empty(again.Data.Grades); Assert.Empty(again.Data.Attendance); Assert.Empty(again.Data.Messages);
        Assert.Empty(again.Data.Announcements); Assert.Empty(again.Data.Schedule); Assert.Empty(again.Data.Homework);
    }

    [Fact]
    public void ReadMessagesSeedStateAndPrefixStopsAtPreviouslySeenMessage()
    {
        var messages = Messages.Parse(Fixtures.Received);
        var baseline = Notifications.FilterNew(new NotificationData { Messages = messages }, new NotificationIds());
        Assert.Single(baseline.Data.Messages); Assert.Equal(new[] { "42", "41" }, baseline.Seen.Messages);
        var newest = messages[0] with { Href = "43" };
        var update = Notifications.FilterNew(new NotificationData { Messages = [newest, .. messages] }, baseline.Seen);
        Assert.Equal("43", Assert.Single(update.Data.Messages).Href);
        Assert.Equal(new[] { "42", "41" }, baseline.Seen.Messages);
    }

    [Fact]
    public void SeenStateCanBeSavedAsJsonAndReloaded()
    {
        var original = new NotificationIds { Messages = ["42"], Schedule = ["abc"] };
        var loaded = JsonSerializer.Deserialize<NotificationIds>(JsonSerializer.Serialize(original))!;
        Assert.Equal(original.Messages, loaded.Messages); Assert.Equal(original.Schedule, loaded.Schedule);
        loaded.Messages.Add("43"); Assert.Single(original.Messages);
    }

    [Fact]
    public async Task InitialRoutingDoesNotDependOnMenuOrderAndSeedsZeroCounters()
    {
        var handler = Handler();
        using var client = new Client(new Token("a:b"), Options, handler);
        var initial = await Notifications.GetInitialNotificationDataAsync(client);
        Assert.Equal(3, initial.Data.Grades.Count); Assert.Equal(2, initial.Data.Attendance.Count);
        Assert.Single(initial.Data.Messages); Assert.Single(initial.Data.Schedule);
        Assert.Empty(initial.Data.Announcements); Assert.Empty(initial.Data.Homework);
        Assert.Single(initial.Seen.Announcements); Assert.Equal("77", Assert.Single(initial.Seen.Homework));
        var homeworkRequest = Assert.Single(handler.Requests, r => r.Uri.AbsolutePath == "/moje_zadania");
        Assert.Contains("dataOd=2026-10-02&dataDo=2026-10-09", homeworkRequest.Body);
        var update = await Notifications.GetNewNotificationDataAsync(client, initial.Seen);
        Assert.Empty(update.Data.Grades); Assert.Empty(update.Data.Attendance); Assert.Empty(update.Data.Messages);
        Assert.Empty(update.Data.Announcements); Assert.Empty(update.Data.Schedule); Assert.Empty(update.Data.Homework);
    }

    [Fact]
    public async Task EmptyAnnouncementBaselineDoesNotIndexEmptyList()
    {
        using var client = MockHttp.Create(_ => MockHttp.Response(Fixtures.EmptyAnnouncements));
        var result = await Notifications.ParseBasicAmountAsync(client, new NotificationAmount("Ogłoszenia", "/ogloszenia", 0));
        Assert.Empty(result.Data.Announcements); Assert.Empty(result.Seen.Announcements);
    }

    [Fact]
    public async Task MissingMenuCategoriesProduceTypedEmptyCollections()
    {
        using var client = MockHttp.Create(_ => MockHttp.Response("<div id='graphic-menu'></div>"));
        var result = await Notifications.GetInitialNotificationDataAsync(client);
        Assert.Empty(result.Data.Grades); Assert.Empty(result.Data.Messages); Assert.Empty(result.Seen.Schedule);
    }

    [Fact]
    public async Task ZeroGradeCounterAvoidsFetchingGrades()
    {
        using var client = MockHttp.Create(_ => throw new InvalidOperationException("Unexpected network call"));
        var result = await Notifications.ParseBasicAmountAsync(client, new NotificationAmount("Oceny", "/przegladaj_oceny/uczen", 0));
        Assert.Empty(result.Data.Grades);
    }

    private static MockHttp Handler() => new(request => MockHttp.Response(request.Uri.AbsolutePath switch
    {
        "/uczen/index" => Menu, "/przegladaj_oceny/uczen" => Fixtures.NumericGrades, "/przegladaj_nb/uczen" => Fixtures.Attendance,
        "/wiadomosci/1/5" => Fixtures.Received, "/ogloszenia" => Fixtures.Announcements, "/moje_zadania" => Fixtures.Homework,
        "/terminarz/dodane_od_ostatniego_logowania" => Fixtures.Recent, _ => throw new InvalidOperationException("Unexpected endpoint")
    }));

    private static ClientOptions Options => MockHttp.Options with { TimeProvider = new FixedTimeProvider() };
    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
