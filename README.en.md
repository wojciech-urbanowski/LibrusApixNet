# LibrusApixNet

[Polski](README.md) | [English](README.en.md)

A C# port of [librus-apix by Pascal Jodłowski and its contributors](https://github.com/RustySnek/librus-apix), targeting **.NET 10**. Provides authentication and sessions, numeric and descriptive grades, attendance, timetables, announcements, messages and recipients, homework, schedules, student information, completed lessons and notifications.

This is an independent, unofficial project, unaffiliated with Librus and not endorsed by the upstream authors. The port is maintained by LibrusApixNet contributors under **GPL-3.0-only**. See [LICENSE](LICENSE) for the full terms and [NOTICE.md](NOTICE.md) for provenance, modification dates and dependency notices.

## Requirements and platforms

- .NET 10 SDK to build and test; .NET 10 in consuming applications.
- Windows, Linux and macOS. The platform-neutral `net10.0` target has no Windows API dependencies or fixed runtime identifier.
- PowerShell 7 (`pwsh`) for optional maintenance/package verification scripts; Git is also required for `-ShowDiff` and the comparison script test.

GitHub Actions builds, tests, packs and installs the package on all three systems. Linux and macOS support is confirmed by successful jobs for those systems; a local Windows check does not replace them.

## Build and installation

Run these commands from the source root in either PowerShell or Bash:

```sh
dotnet restore LibrusApixNet.slnx
dotnet build LibrusApixNet.slnx -c Release --no-restore
dotnet test LibrusApixNet.slnx -c Release --no-build --no-restore
dotnet pack src/LibrusApixNet/LibrusApixNet.csproj -c Release --no-build --no-restore
```

The package `LibrusApixNet.1.6.0-beta1.nupkg` is written to `artifacts/packages`. Running `dotnet pack` alone does not publish it to NuGet.org. After publishing is enabled in GitHub Actions, each new version is published following successful CI on `main`; see the [publishing guide](docs/PUBLISHING.en.md).

[Version.props](Version.props) defines the version: `UpstreamVersion` + `-beta` + `BetaNumber`. This is currently the port of `librus-apix` **1.6.0**, beta **1**. Update the upstream version only after porting its changes. NuGet does not allow replacing a published version; reruns skip duplicates. Fixes for the same upstream version require incrementing `BetaNumber`.

After publication, install with `dotnet add package LibrusApixNet --version 1.6.0-beta1`. For a local installation, add the packages directory as a source and leave NuGet.org enabled for AngleSharp. Substitute your own absolute path:

```powershell
# PowerShell: run from the source root
dotnet nuget add source (Resolve-Path ./artifacts/packages).Path --name LibrusApixNet-local
```

```bash
# Bash: substitute your local path
dotnet nuget add source "/absolute/path/to/artifacts/packages" --name LibrusApixNet-local
```

Then run from your application's directory:

```sh
dotnet add package LibrusApixNet --version 1.6.0-beta1
```

## Usage

Alternatively, add a project reference to `src/LibrusApixNet/LibrusApixNet.csproj`.

```csharp
using LibrusApixNet;

using var client = new Client();
await client.GetTokenAsync(username, password);

var result = await Grades.GetGradesAsync(client);
foreach (var (subject, grades) in result.Semesters[0])
    foreach (var grade in grades)
        Console.WriteLine($"{subject}: {grade.Mark} ({grade.Date})");

var announcements = await Announcements.GetAnnouncementsAsync(client);
var student = await StudentInformationService.GetStudentInformationAsync(client);
```

Load an existing session key without logging in:

```csharp
using var client = new Client(new Token(apiKey)); // DZIENNIKSID:SDZIENNIKSID
```

Every network operation accepts a `CancellationToken`. `Client` maintains one HTTP session and implements `IDisposable`. Callers must dispose responses returned directly by `Client.GetAsync` and `PostAsync`; module methods handle this internally. An optional `HttpMessageHandler` supports controlled test transport and is disposed with the client. When supplying a handler, configure its redirect behavior yourself; the default handler manages redirects and cookies automatically.

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
// Sending a message is an explicit operation:
// var sent = await Messages.SendMessageAsync(client, "Subject", "Content", [recipientId]);
```

`GradesResult` contains `Semesters`, `Averages` and `Descriptive`. The two semesters use indices 0 and 1. `Gpa.Semester == 0` denotes the annual average. `Grade.Value` returns `double?`: integer and decimal values (including decimal commas), `+0.5` for a plus and `-0.25` for a minus. It returns `null` for excluded or nonnumeric grades such as `85%`, `85/100` and `A+`; `Grade.Mark` retains the original text.

`Grades.GetGradesAsync` also supports descriptive grades loaded by the gateway API: it refreshes OAuth, resolves the student ID and adds API data where HTML grades are missing. Optional API failures preserve the HTML result; caller cancellation is propagated. The standalone `Grades.Parse` parser does not make network requests. `Client.PostJsonAsync` sends JSON; the caller disposes its response.

Preserved upstream conventions: `Attendance.Semester` is a zero-based HTML block index, and the returned list is reversed when two semesters are present. `Messages.GetMaxPageNumberAsync` returns the last page index, whereas the corresponding `CompletedLessons` method returns the page count. See the [porting guide](docs/PORTING.en.md) for all differences.

## Notifications

```csharp
var initial = await Notifications.GetInitialNotificationDataAsync(client); // once per new token
var next = await Notifications.GetNewNotificationDataAsync(client, initial.Seen);

var stateJson = System.Text.Json.JsonSerializer.Serialize(next.Seen);
// Persist next.Seen and pass it to the next poll.
```

The returned state contains IDs already seen; the input state is not modified. Dashboard counters belong to the token, so subsequent polls use individual modules. The homework date range uses `ClientOptions.TimeProvider` and its local time zone.

## Tests and upstream updates

Tests use representative HTML/JSON and controlled HTTP transport, without an account or a connection to Librus. Two descriptive-grade cases originate from upstream tests. These tests do not confirm operation on a current account or every school's page layout.

The port is based on commit `c54f05f3b483627328852825fff3e46fab27e87d` from 2026-10-10. The [symbol map and file hashes](docs/upstream-map.json) and [update procedure](docs/PORTING.en.md) allow comparison with later revisions:

```sh
pwsh -NoProfile -File ./tools/Compare-Upstream.ps1 -SourcePath ../librus-apix
pwsh -NoProfile -File ./tools/Compare-Upstream.ps1 -SourcePath ../librus-apix -ShowDiff
pwsh -NoProfile -File ./tools/Compare-Upstream.ps1 -SourcePath ../librus-apix -FailOnChanges
```

`-SourcePath` must point to your local Python checkout. The script does not download it or rely on the author's private paths. Verify the tools without that checkout:

```sh
pwsh -NoProfile -File ./tools/Test-CompareUpstream.ps1
pwsh -NoProfile -File ./tools/Test-Package.ps1 -PackagePath ./artifacts/packages/LibrusApixNet.1.6.0-beta1.nupkg
```

The package check creates a temporary consumer with a separate NuGet cache, forces installation of LibrusApixNet from the local package and downloads AngleSharp from NuGet.org. It does not connect to Librus.

Source code and build files are available in the [public GitHub repository](https://github.com/wojciech-urbanowski/LibrusApixNet). Package metadata identifies the repository and commit used to build it.

## Console example

Help does not require an account:

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

## License and contributions

The port follows the upstream GPLv3 license file; the inconsistent MIT label in upstream metadata is recorded in [NOTICE.md](NOTICE.md). The library is provided without warranty. The parser dependency, [AngleSharp 1.8.4](https://www.nuget.org/packages/AngleSharp/1.8.4), retains its own MIT license and uses CSS selectors corresponding to the BeautifulSoup parsers.

For bug reports, HTML sanitization and contribution instructions, see [CONTRIBUTING.md](CONTRIBUTING.md).
