# Port mapping and maintenance

[Polski](PORTING.md) | [English](PORTING.en.md) | [README](../README.en.md)

Upstream: [RustySnek/librus-apix](https://github.com/RustySnek/librus-apix), originally authored by Pascal Jodłowski and its contributors. Baseline: release **1.6.0**, commit **c54f05f3b483627328852825fff3e46fab27e87d**, 2026-10-10, `fix: replace [attr!=] selectors with :not() for soupsieve 3.0 compat`. The source working tree was clean. The port covers all 15 Python modules. C# port and publication preparation: 2026-10-11, LibrusApixNet contributors.

The port uses `GPL-3.0-only`, following the license file at the baseline commit. See [LICENSE](../LICENSE) for the full text and [NOTICE.md](../NOTICE.md) for attribution and the discrepancy with the older MIT entry in upstream package metadata. Python distribution metadata and generated documentation are not copied into the port.

## File mapping

All C# files in this table are under `src/LibrusApixNet/`. Exact mappings of classes, public methods and helpers are recorded in [upstream-map.json](upstream-map.json).

| Python (`librus_apix/`) | C# | Main entry points | Tests (`tests/LibrusApixNet.Tests/`) |
|---|---|---|---|
| `__init__.py` | `LibraryInfo.cs` | `LibraryInfo.GetVersion`, `Version` | standard assembly version |
| `client.py` | `Client.cs` | `Token`, `Client`, `GetTokenAsync`, `RefreshOAuthAsync`, `GetAsync`, `PostAsync`, `PostJsonAsync`, `NewClient` | `ClientTests.cs`, `GatewayGradeTests.cs` |
| `urls.py` | `Urls.cs` | `ClientOptions` | `ClientTests.cs`, `RequestTests.cs` |
| `helpers.py` | `Helpers.cs` | `Html.Parse`, shared selectors, tooltips and links | `ParserTests.cs` |
| `exceptions.py` | `Exceptions.cs` | `*Exception` types | `ClientTests.cs`, `ParserTests.cs`, `RequestTests.cs` |
| `grades.py` | `Grades.cs` | `Grades.GetGradesAsync`, `Parse`, `Grade`, `GradeDescriptive`, `Gpa`, `GradesResult`, gateway API helper | `ParserTests.cs`, `RequestTests.cs`, `GatewayGradeTests.cs` |
| `attendance.py` | `Attendance.cs` | `Attendances.GetAttendanceAsync`, `GetDetailAsync`, `GetGatewayAttendanceAsync`, `GetSubjectAttendanceAsync`, frequency calculations | `AttendanceTests.cs`, `ParserTests.cs`, `RequestTests.cs` |
| `timetable.py` | `Timetable.cs` | `Timetable.GetTimetableAsync`, `Parse`, `Period`, `PeriodChange` | `ParserTests.cs`, `RequestTests.cs` |
| `announcements.py` | `Announcements.cs` | `Announcements.GetAnnouncementsAsync`, `Parse` | `ParserTests.cs`, `RequestTests.cs` |
| `messages.py` | `Messages.cs` | `Messages.GetReceivedAsync`, `GetSentAsync`, `GetMaxPageNumberAsync`, `MessageContentAsync`, `RecipientGroupsAsync`, `GetRecipientsAsync`, `SendMessageAsync`, parsers | `ParserTests.cs`, `RequestTests.cs` |
| `homework.py` | `Homework.cs` | `HomeworkService.GetHomeworkAsync`, `HomeworkDetailAsync`, parsers | `ParserTests.cs`, `RequestTests.cs` |
| `schedule.py` | `Schedule.cs` | `Schedule.GetScheduleAsync`, `ScheduleDetailAsync`, `GetRecentlyAddedScheduleAsync`, parsers | `ParserTests.cs`, `RequestTests.cs` |
| `student_information.py` | `StudentInformation.cs` | `StudentInformationService.GetStudentInformationAsync`, `Parse` | `ParserTests.cs`, `RequestTests.cs` |
| `completed_lessons.py` | `CompletedLessons.cs` | `CompletedLessons.GetCompletedAsync`, `GetMaxPageNumberAsync`, `Parse` | `ParserTests.cs`, `RequestTests.cs` |
| `notifications.py` | `Notifications.cs` | `Notifications.GetNewTokenNotificationAmountsAsync`, `ParseBasicAmountAsync`, `GetInitialNotificationDataAsync`, `GetNewNotificationDataAsync`, `FilterNew` | `NotificationTests.cs` |

The port preserves Python's modular structure; public static methods still accept a client. Shared helpers are in `Html` in `Helpers.cs`. A model such as `Attendance`, `Homework` or `StudentInformation` cannot share its name with its module in the same C# namespace, hence `Attendances`, `HomeworkService` and `StudentInformationService`.

## Conventions and deliberate differences

| Area | Port decision |
|---|---|
| Naming and I/O | PascalCase, `Task<T>`, `Async` suffix and `CancellationToken`. One `HttpClient`, `CookieContainer`, configurable handler and proxy replace `requests`/`aiohttp`. |
| Models | C# records. `grade` → `Mark`, `desc` → `Description`, `gpa` → `Average`, `API_Key` → `ApiKey`; other fields generally use PascalCase. |
| Composite results | `GradesResult`, `SendMessageResult`, `AttendanceFrequency` and `NotificationResult` replace tuples of grades, send results, frequency and notifications. |
| Number/text unions | `Grade.Value` is `double?`, with `null` for excluded or nonnumeric grades; `Grade.Mark` preserves the original text. Multi-digit numbers and decimal dots/commas are supported. `Gpa.Average` preserves text and adds `NumericValue`; `StudentInformation.LuckyNumber` is `int?` instead of `"?"`; `Event.Number` is `int?` instead of `"unknown"`; `Lesson.LessonNumber` is parsed as `int` instead of the actual upstream string. |
| Timetable | `DateOnly`; Monday validation is independent of system language. Seven lists include empty periods. `Period.Info` uses `PeriodChange?`, where `null` corresponds to upstream's `""`. `Weekday` uses English names, as upstream does. |
| Model dates | Text is preserved to retain page formatting. Date-range arguments use `yyyy-MM-dd` and are validated before requests; schedule month/year are validated numbers. |
| HTML parser | AngleSharp inserts `tbody` according to HTML5; timetable selectors support it explicitly. Nonstandard BS4 selectors such as `[class!='…']` are replaced with standard selectors or element filtering. |
| Grades | Supports both upstream descriptive schemas and the first-semester summary. Outer columns use direct row children so nested tables do not shift semesters. Grade text is trimmed; descriptions preserve upstream's handling of `<br/>`. The unknown second-semester summary schema remains unimplemented. |
| Gateway descriptive grades (1.6.0) | `newSchemaGradesByStudentTable` triggers OAuth refresh, token/user resolution and a JSON POST for grades. An assigned student takes precedence over the user ID. Subject names come from HTML, then the API; teachers come from the users API. Gateway data fills missing HTML results. Optional API failures preserve HTML data; caller cancellation is propagated. `Grades.Parse` remains an offline parser. |
| Sending messages | Status comes from the HTTP response, rather than the HTML object used by an upstream bug. Both `nie zostala` and `nie została` are supported. Recipients use repeated `DoKogo` keys, as in `requests`. |
| Sent messages | `Unread` is read from the cell text `NIE`; Python compared a `Tag` to a string and always returned `False`. |
| Schedule | `HH:mm` is recognized before lesson numbers; Python could interpret minutes as a lesson number. `Hour == "unknown"` is retained when no time exists. |
| Empty data | Empty lists of messages, announcements and homework are valid. Notification initialization does not index an empty announcement list. |
| Cookies | Separate containers per client, with URL-compatible domains. Replacing a token updates existing domain cookies and removes previous OAuth when the new token lacks it. Refreshing OAuth requires a new `Set-Cookie`, rather than accepting an old container value. |
| HTTP requests | Module methods validate HTTP success before parsing; sending returns a success result. Client transport methods return raw responses. Maintenance also supports a non-JSON 503 response. |
| Attendance frequency | Overall frequency uses fractions 0–1; subject frequency uses percentages 0–100. Different handling of excursions in these algorithms is preserved. Unknown JSON types become `unknown` instead of causing dictionary lookup failure. |
| Subjects from the API | Shared client/proxy; up to eight concurrent requests, cached lesson/subject tasks and at most five attempts with 0.5/1/2/4 second delays for transient HTTP/transport failures. Authorization errors and cancellation are not retried. |
| Notifications | Menu route determines category independently of item order. Missing categories return empty lists. No shared default lists; input `NotificationIds` is copied. Saved schedule IDs prevent repeat reporting of the initial batch. Read messages also establish the baseline. |
| Notification IDs | Preserves hrefs, `title + date` for announcements and MD5 of schedule text. Announcement, homework and message reads stop at the first seen ID, so input order still matters. Descriptive grades are excluded from notifications, as upstream does. |
| Library version | `<UpstreamVersion>-beta<BetaNumber>` from `Version.props`; `LibraryInfo.Version` may also include assembly commit metadata. The map records the upstream version and SHA. |

## Updating the port

1. Update your local Python checkout. From the port's root, run `pwsh -NoProfile -File ./tools/Compare-Upstream.ps1 -SourcePath ../librus-apix -ShowDiff`, replacing `../librus-apix` with your checkout's location. The script compares module/test contents to the map's SHA-256 hashes and normalizes CRLF to LF. It also lists added/deleted files. New modules are marked `UNMAPPED`.
2. For changed files, inspect `modules[].symbols` and the table above. Tooltip, popup and filter changes may also affect `Helpers.cs`.
3. Port new behavior and upstream regression cases to the corresponding C# files. Preserve deliberate port fixes, or explicitly update their descriptions. Add new functions to the symbol map.
4. Run `dotnet test -c Release` and `dotnet build -c Release`. Public `Parse*` methods support testing saved HTML without an account. Capture new layouts in `Fixtures.cs` or sanitized test files.
5. After all changes are ported and verified, update the version, commit, date, subject and hashes in `upstream-map.json`, and set the matching `UpstreamVersion` in `Version.props`. Reset `BetaNumber` to `1` for a new upstream release. Do not move the baseline commit while some changes remain unported. A hash is SHA-256 of UTF-8 text after `.Replace("\r\n", "\n")`; see `Get-NormalizedHash` in the script.
6. Run the comparison again. No changes confirms correspondence with the saved source baseline, not correctness of the port by itself. `-FailOnChanges` returns exit code 1 when changes exist. `-PassThru` returns objects instead of a table.

The port and its .NET tests require only the .NET 10 SDK. The comparison tool uses PowerShell 7 on Windows, Linux and macOS; optional `-ShowDiff` requires a Git repository containing the baseline commit. `-SourcePath` is required, can be relative or absolute and must be quoted if it contains spaces. Missing arguments produce an error without an interactive prompt. The script does not clone upstream.

`pwsh -NoProfile -File ./tools/Test-CompareUpstream.ps1` verifies the tool using a temporary synthetic repository without a Librus account or upstream checkout. It covers paths with spaces, CRLF/LF, changed/added/deleted files, `__pycache__` exclusion, `-PassThru`, `-ShowDiff` and exit codes. `pwsh -NoProfile -File ./tools/Test-Package.ps1 -PackagePath ./artifacts/packages/LibrusApixNet.1.6.0-beta1.nupkg` checks package metadata and installation into an isolated consumer.

CI runs the checks on Windows, Linux and macOS. A successful job for a platform confirms the tested compatibility with that platform. Automated tests do not log into live Librus or cover every school's page layout. The 2FA scope remains the same as upstream: calling the existing flow's endpoint, without interactive code entry. See [CONTRIBUTING.md](../CONTRIBUTING.md) for fixture sanitization rules.
