# LibrusApixNet

[Polski](README.md) | [English](README.en.md) | [NuGet](https://www.nuget.org/packages/LibrusApixNet)

A C# library for .NET 10 that provides access to Librus Synergia data. It is a port of [librus-apix](https://github.com/RustySnek/librus-apix) and supports Windows, Linux and macOS.

The original **librus-apix** library was authored by **Pascal Jodłowski** and is developed with its contributors. This port retains **GNU GPL v3** (`GPL-3.0-only`) from the [upstream LICENSE](https://github.com/RustySnek/librus-apix/blob/c54f05f3b483627328852825fff3e46fab27e87d/LICENSE). See [NOTICE.md](NOTICE.md) for attribution and provenance.

## Features

- Authentication and saved sessions.
- Numeric and descriptive grades, averages and attendance.
- Timetables, schedules, homework and completed lessons.
- Announcements, reading and sending messages.
- Student information and detection of new data.

## Installation

Add the [NuGet package](https://www.nuget.org/packages/LibrusApixNet/1.6.0-beta1) to your application:

```sh
dotnet add package LibrusApixNet --version 1.6.0-beta1
```

The library requires **.NET 10**. The current release, `1.6.0-beta1`, is a beta port of `librus-apix` 1.6.0. Builds, tests and package installation are checked on all three operating systems in [GitHub Actions](https://github.com/wojciech-urbanowski/LibrusApixNet/actions/workflows/ci.yml).

## Quick start

This example signs in and reads first-semester grades. Credentials are loaded from the `LIBRUS_USERNAME` and `LIBRUS_PASSWORD` environment variables.

```csharp
using LibrusApixNet;

var username = Environment.GetEnvironmentVariable("LIBRUS_USERNAME")
    ?? throw new InvalidOperationException("Set LIBRUS_USERNAME.");
var password = Environment.GetEnvironmentVariable("LIBRUS_PASSWORD")
    ?? throw new InvalidOperationException("Set LIBRUS_PASSWORD.");

using var client = new Client();
await client.GetTokenAsync(username, password);

var result = await Grades.GetGradesAsync(client);
foreach (var (subject, grades) in result.Semesters[0])
    foreach (var grade in grades)
        Console.WriteLine($"{subject}: {grade.Mark} ({grade.Date})");
```

A saved session can be loaded with `new Client(new Token(apiKey))`. Every network operation accepts a `CancellationToken`; dispose the client with `using`.

The [user guide](docs/USAGE.en.md) covers sessions and proxies, additional operations, notifications and a console example with PowerShell and Bash commands.

## Project status

This is an independent, unofficial project. It is not affiliated with Librus or endorsed by the upstream authors. Tests use synthetic HTML/JSON and controlled HTTP transport. They do not verify a live Librus account or every school's page layout.

## Issues and contributions

Report bugs and suggest changes through [Issues](https://github.com/wojciech-urbanowski/LibrusApixNet/issues). Include a minimal example and sanitized data, without passwords, tokens or session cookies. See [CONTRIBUTING.md](CONTRIBUTING.md) for details.

Build and test instructions are in the [development guide](docs/DEVELOPMENT.en.md). The [porting guide](docs/PORTING.en.md) documents compatibility with the original project.

## License and provenance

LibrusApixNet is distributed under **GPL-3.0-only**, without warranty. The original `librus-apix` library was created by Pascal Jodłowski and its contributors; the C# port is maintained by LibrusApixNet contributors. This release is based on `librus-apix` 1.6.0.

See [LICENSE](LICENSE) for the full terms. [NOTICE.md](NOTICE.md) records the upstream commit, port modification date, attribution and the conflicting license label in upstream metadata. The [AngleSharp](https://www.nuget.org/packages/AngleSharp/1.8.4) dependency is licensed under MIT.
