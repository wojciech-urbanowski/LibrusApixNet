# LibrusApixNet

An unofficial C#/.NET 10 port of [RustySnek/librus-apix](https://github.com/RustySnek/librus-apix) for Librus Synergia. Supports Windows, Linux and macOS through the platform-neutral `net10.0` target.

Provides authentication and sessions, numeric and descriptive grades, attendance, timetables, announcements, messages and recipients, homework, schedules, student information, completed lessons and notifications. This independent project is not affiliated with or endorsed by Librus or the upstream project.

Source code and documentation: [wojciech-urbanowski/LibrusApixNet](https://github.com/wojciech-urbanowski/LibrusApixNet).

## Local installation

Version `0.1.0` is prepared as a local package. No publication on NuGet.org is implied. Build the package from the source directory:

```sh
dotnet pack src/LibrusApixNet/LibrusApixNet.csproj -c Release
```

Add the generated `artifacts/packages` directory as a local NuGet source in your application. Keep NuGet.org enabled to restore the AngleSharp dependency:

```sh
dotnet nuget add source /absolute/path/to/artifacts/packages --name LibrusApixNet-local
dotnet add package LibrusApixNet --version 0.1.0
```

On Windows, substitute the absolute path to your local packages directory and quote it if it contains spaces. The package requires .NET 10; building it requires the .NET 10 SDK. PowerShell 7 is only needed for the repository's maintenance and verification scripts.

## Usage

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

An existing session can be loaded with `new Client(new Token(apiKey))`. Every network operation accepts a `CancellationToken`. Dispose the client when finished; callers also own responses returned directly by `Client.GetAsync` and `PostAsync`.

Tests use synthetic HTML/JSON and a controlled HTTP handler without connecting to Librus. They do not establish compatibility with a live account or every school's page layout. Windows can be checked locally; Linux and macOS compatibility is confirmed by successful jobs in the repository's CI matrix.

## License and provenance

Copyright (C) 2026 LibrusApixNet contributors. Distributed under **GPL-3.0-only**, without warranty. The package includes the full `LICENSE` and `NOTICE.md` with upstream attribution and dependency notices. The original project was authored by Pascal Jodłowski and maintained with the librus-apix contributors.

The port's baseline is [commit 2fedfe8](https://github.com/RustySnek/librus-apix/commit/2fedfe8ffa4933abb884929716519ddbeb8eb32d) from 2026-09-28. The pinned upstream license is GPLv3 despite an inconsistent MIT entry in its package metadata. AngleSharp 1.8.4 is a separate dependency under MIT.
