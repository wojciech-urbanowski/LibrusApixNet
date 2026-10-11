# User guide

[Polski](USAGE.md) | [English](USAGE.en.md) | [README](../README.en.md)

See the [README](../README.en.md#quick-start) for NuGet installation and basic authentication. The examples below use a `client` with an active session.

## Session and client configuration

Load an existing session key without logging in:

```csharp
using var client = new Client(new Token(apiKey)); // DZIENNIKSID:SDZIENNIKSID
```

Every network operation accepts a `CancellationToken`. `Client` maintains one HTTP session and implements `IDisposable`. Callers must dispose responses returned directly by `Client.GetAsync`, `PostAsync` and `PostJsonAsync`; module methods handle this internally. An optional `HttpMessageHandler` supports controlled test transport and is disposed with the client. When supplying a handler, configure its redirect behavior yourself; the default handler manages redirects and cookies automatically.

Configure endpoints and a proxy before creating the client:

```csharp
using System.Net;

var options = new ClientOptions
{
    Proxy = new WebProxy("http://localhost:8080"),
    Timeout = TimeSpan.FromSeconds(30)
};
using var client = new Client(new Token(apiKey), options);
```

`BaseUrl` and `ApiUrl` determine default endpoints; each `*Url` can be overridden independently. `Client.Cookies` allows additional cookies with the appropriate domain. Authentication cookies belong to `BaseUrl`.

## Other operations

```csharp
var week = await Timetable.GetTimetableAsync(client, new DateOnly(2026, 10, 5));
var attendance = await Attendances.GetAttendanceAsync(client, "week");
var frequency = await Attendances.GetAttendanceFrequencyAsync(client); // fractions 0–1
var subjects = await Attendances.GetSubjectFrequencyAsync(client);    // percentages 0–100
var homework = await HomeworkService.GetHomeworkAsync(client, "2026-10-01", "2026-10-31");
var schedule = await Schedule.GetScheduleAsync(client, month: 10, year: 2026, includeEmpty: true);
var lessons = await CompletedLessons.GetCompletedAsync(client, "2026-10-01", "2026-10-31");
var received = await Messages.GetReceivedAsync(client, page: 0);
var groups = await Messages.RecipientGroupsAsync(client);
var recipients = await Messages.GetRecipientsAsync(client, groups[0]);
// Sending a message:
// var sent = await Messages.SendMessageAsync(client, "Subject", "Content", [recipientId]);
```

`GradesResult` contains `Semesters`, `Averages` and `Descriptive`. The two semesters use indices 0 and 1. `Gpa.Semester == 0` denotes the annual average. `Grade.Value` returns `double?`: integer and decimal values (including decimal commas), `+0.5` for a plus and `-0.25` for a minus. It returns `null` for excluded or nonnumeric grades such as `85%`, `85/100` and `A+`; `Grade.Mark` retains the original text.

`Grades.GetGradesAsync` also supports descriptive grades loaded by the gateway API: it refreshes OAuth, resolves the student ID and adds API data where HTML grades are missing. Optional API failures preserve the HTML result; caller cancellation is propagated. The standalone `Grades.Parse` parser does not make network requests. `Client.PostJsonAsync` sends JSON; the caller disposes its response.

Preserved upstream conventions: `Attendance.Semester` is a zero-based HTML block index, and the returned list is reversed when two semesters are present. `Messages.GetMaxPageNumberAsync` returns the last page index, whereas the corresponding `CompletedLessons` method returns the page count. See the [porting guide](PORTING.en.md) for all differences.

## Notifications

```csharp
var initial = await Notifications.GetInitialNotificationDataAsync(client); // once per new token
var next = await Notifications.GetNewNotificationDataAsync(client, initial.Seen);

var stateJson = System.Text.Json.JsonSerializer.Serialize(next.Seen);
// Persist next.Seen and pass it to the next poll.
```

The returned state contains IDs already seen; the input state is not modified. Dashboard counters belong to the token, so subsequent polls use individual modules. The homework date range uses `ClientOptions.TimeProvider` and its local time zone.

## Console example

The example is available in [examples/QuickStart](../examples/QuickStart). Run these commands from the root of a cloned repository with the .NET 10 SDK installed. Help does not require an account:

```sh
dotnet run --project examples/QuickStart -- --help
```

To read live data, set a session key or a username and password. Substitute your own values below; do not store credentials in source code or bug reports.

```powershell
# PowerShell
$env:LIBRUS_USERNAME = 'your-username'
$env:LIBRUS_PASSWORD = 'your-password'
dotnet run --project examples/QuickStart -c Release
# Alternatively, in a fresh session: $env:LIBRUS_API_KEY = 'DZIENNIKSID:SDZIENNIKSID'
```

```bash
# Bash
export LIBRUS_USERNAME='your-username'
export LIBRUS_PASSWORD='your-password'
dotnet run --project examples/QuickStart -c Release
# Alternatively, in a fresh session: export LIBRUS_API_KEY='DZIENNIKSID:SDZIENNIKSID'
```

If `LIBRUS_API_KEY` is set, the example uses it instead of username/password authentication. It reads student information, grades and announcements, and writes personal data to the console.
