using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace LibrusApixNet;

public sealed record NotificationAmount(string Name, string Destination, int Amount);
public sealed record NotificationData
{
    public IReadOnlyList<Grade> Grades { get; init; } = [];
    public IReadOnlyList<Attendance> Attendance { get; init; } = [];
    public IReadOnlyList<Message> Messages { get; init; } = [];
    public IReadOnlyList<Announcement> Announcements { get; init; } = [];
    public IReadOnlyList<RecentEvent> Schedule { get; init; } = [];
    public IReadOnlyList<Homework> Homework { get; init; } = [];
}

/// <summary>Serializable state. Each result owns separate ID lists; input state is never mutated.</summary>
public sealed record NotificationIds
{
    public List<string> Grades { get; init; } = [];
    public List<string> Attendance { get; init; } = [];
    public List<string> Messages { get; init; } = [];
    public List<string> Announcements { get; init; } = [];
    public List<string> Schedule { get; init; } = [];
    public List<string> Homework { get; init; } = [];
}

public sealed record NotificationResult(NotificationData Data, NotificationIds Seen);

public static class Notifications
{
    private static readonly HashSet<string> Destinations = ["/ogloszenia", "/moje_zadania", "/wiadomosci", "/przegladaj_oceny/uczen", "/przegladaj_nb/uczen", "/terminarz"];

    public static async Task<List<NotificationAmount>> GetNewTokenNotificationAmountsAsync(Client client, CancellationToken cancellationToken = default) =>
        ParseAmounts(await client.GetTextAsync(client.Options.IndexUrl, cancellationToken));

    public static List<NotificationAmount> ParseAmounts(string html)
    {
        using var document = Html.Parse(html);
        var amounts = new List<NotificationAmount>();
        foreach (var anchor in document.QuerySelectorAll("div#graphic-menu > ul > li > a:not(.counter)"))
        {
            var destination = Html.Attr(anchor, "href", "/");
            var name = Html.NoNewlines(anchor.TextContent).Trim();
            if (!Destinations.Contains(destination) || name == "Widok alternatywny") continue;
            var counter = anchor.ParentElement?.QuerySelector("a.button.counter");
            var amount = int.TryParse(Html.Text(counter), out var parsed) ? parsed : 0;
            amounts.Add(new NotificationAmount(name, destination, amount));
        }
        return amounts;
    }

    public static async Task<NotificationResult> ParseBasicAmountAsync(Client client, NotificationAmount amount, CancellationToken cancellationToken = default)
    {
        var data = new NotificationData();
        var seen = new NotificationIds();
        if (amount.Amount == 0 && amount.Destination is not ("/ogloszenia" or "/moje_zadania" or "/wiadomosci")) return new(data, seen);
        switch (amount.Destination)
        {
            case "/przegladaj_oceny/uczen":
                var grades = await Grades.GetGradesAsync(client, "last_login", cancellationToken);
                data = data with { Grades = grades.Semesters.SelectMany(s => s.Values).SelectMany(g => g).ToList() };
                break;
            case "/przegladaj_nb/uczen":
                data = data with { Attendance = (await Attendances.GetAttendanceAsync(client, "last_login", cancellationToken)).SelectMany(s => s).ToList() };
                break;
            case "/wiadomosci":
                data = data with { Messages = (await Messages.GetReceivedAsync(client, 0, cancellationToken)).Take(2).ToList() };
                break;
            case "/ogloszenia":
                var announcements = await Announcements.GetAnnouncementsAsync(client, cancellationToken);
                if (amount.Amount == 0)
                {
                    if (announcements.Count > 0) seen.Announcements.Add(AnnouncementId(announcements[0]));
                }
                else data = data with { Announcements = announcements.Take(amount.Amount).ToList() };
                break;
            case "/terminarz":
                data = data with { Schedule = await Schedule.GetRecentlyAddedScheduleAsync(client, cancellationToken) };
                break;
            case "/moje_zadania":
                var (from, to) = HomeworkDateRange(client);
                var homework = await HomeworkService.GetHomeworkAsync(client, from, to, cancellationToken);
                var newest = homework.TakeLast(Math.Max(1, amount.Amount)).ToList();
                if (amount.Amount == 0) seen.Homework.AddRange(newest.Select(h => h.Href));
                else data = data with { Homework = newest };
                break;
        }
        return FilterNew(data, seen);
    }

    /// <summary>Call once after acquiring a new Token. Menu counters are bound to that token.</summary>
    public static async Task<NotificationResult> GetInitialNotificationDataAsync(Client client, CancellationToken cancellationToken = default)
    {
        var amounts = await GetNewTokenNotificationAmountsAsync(client, cancellationToken);
        var batches = new List<NotificationResult>();
        foreach (var amount in amounts) batches.Add(await ParseBasicAmountAsync(client, amount, cancellationToken));
        return new NotificationResult(new NotificationData
        {
            Grades = batches.SelectMany(b => b.Data.Grades).ToList(), Attendance = batches.SelectMany(b => b.Data.Attendance).ToList(),
            Messages = batches.SelectMany(b => b.Data.Messages).ToList(), Announcements = batches.SelectMany(b => b.Data.Announcements).ToList(),
            Schedule = batches.SelectMany(b => b.Data.Schedule).ToList(), Homework = batches.SelectMany(b => b.Data.Homework).ToList()
        }, new NotificationIds
        {
            Grades = batches.SelectMany(b => b.Seen.Grades).ToList(), Attendance = batches.SelectMany(b => b.Seen.Attendance).ToList(),
            Messages = batches.SelectMany(b => b.Seen.Messages).ToList(), Announcements = batches.SelectMany(b => b.Seen.Announcements).ToList(),
            Schedule = batches.SelectMany(b => b.Seen.Schedule).ToList(), Homework = batches.SelectMany(b => b.Seen.Homework).ToList()
        });
    }

    public static async Task<NotificationResult> GetNewNotificationDataAsync(Client client, NotificationIds seenNotifications, CancellationToken cancellationToken = default)
    {
        var grades = await Grades.GetGradesAsync(client, "last_login", cancellationToken);
        var attendance = await Attendances.GetAttendanceAsync(client, "last_login", cancellationToken);
        var messages = await Messages.GetReceivedAsync(client, 0, cancellationToken);
        var announcements = await Announcements.GetAnnouncementsAsync(client, cancellationToken);
        var (from, to) = HomeworkDateRange(client);
        var homework = await HomeworkService.GetHomeworkAsync(client, from, to, cancellationToken);
        var schedule = await Schedule.GetRecentlyAddedScheduleAsync(client, cancellationToken);
        return FilterNew(new NotificationData
        {
            Grades = grades.Semesters.SelectMany(s => s.Values).SelectMany(g => g).ToList(), Attendance = attendance.SelectMany(s => s).ToList(),
            Messages = messages, Announcements = announcements, Homework = homework.AsEnumerable().Reverse().ToList(), Schedule = schedule
        }, seenNotifications);
    }

    public static NotificationResult FilterNew(NotificationData data, NotificationIds seen)
    {
        var ids = new NotificationIds
        {
            Grades = [.. seen.Grades], Attendance = [.. seen.Attendance], Messages = [.. seen.Messages],
            Announcements = [.. seen.Announcements], Schedule = [.. seen.Schedule], Homework = [.. seen.Homework]
        };
        var result = new NotificationData
        {
            Grades = Filter(data.Grades, ids.Grades, g => g.Href), Attendance = Filter(data.Attendance, ids.Attendance, a => a.Href),
            Messages = Filter(data.Messages, ids.Messages, m => m.Href, stopAtSeen: true).Where(m => m.Unread).ToList(),
            Announcements = Filter(data.Announcements, ids.Announcements, AnnouncementId, stopAtSeen: true),
            Homework = Filter(data.Homework, ids.Homework, h => h.Href, stopAtSeen: true),
            Schedule = Filter(data.Schedule, ids.Schedule, e => Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(e.Data))))
        };
        return new NotificationResult(result, ids);
    }

    private static List<T> Filter<T>(IReadOnlyList<T> items, List<string> seen, Func<T, string> id, bool stopAtSeen = false)
    {
        var known = seen.ToHashSet(StringComparer.Ordinal);
        var result = new List<T>();
        foreach (var item in items)
        {
            var key = id(item);
            if (!known.Add(key))
            {
                if (stopAtSeen) break;
                continue;
            }
            seen.Add(key); result.Add(item);
        }
        return result;
    }

    private static string AnnouncementId(Announcement announcement) => announcement.Title + announcement.Date;
    private static (string From, string To) HomeworkDateRange(Client client)
    {
        var today = DateOnly.FromDateTime(client.Options.TimeProvider.GetLocalNow().DateTime);
        return (today.AddDays(-7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
}
