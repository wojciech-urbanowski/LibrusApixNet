# Building and developing the project

[Polski](DEVELOPMENT.md) | [English](DEVELOPMENT.en.md) | [README](../README.en.md)

This guide covers working with the library's source code. See the [README](../README.en.md#installation) to install the published package in an application.

## Requirements

- .NET 10 SDK, at least 10.0.100; [global.json](../global.json) defines SDK selection.
- PowerShell 7 (`pwsh`) for maintenance and verification scripts.
- Git to clone the repository, run the comparison test and use `-ShowDiff`.

Files use UTF-8 and LF line endings. The project targets `net10.0` on Windows, Linux and macOS.

## Build and tests

```sh
git clone https://github.com/wojciech-urbanowski/LibrusApixNet.git
cd LibrusApixNet
dotnet restore LibrusApixNet.slnx
dotnet build LibrusApixNet.slnx -c Release --no-restore
dotnet test LibrusApixNet.slnx -c Release --no-build --no-restore
dotnet run --project examples/QuickStart -c Release --no-build -- --help
```

Tests use synthetic HTML/JSON and controlled HTTP transport. They do not require a Librus account or connect to the service. GitHub Actions runs the checks on Windows, Linux and macOS.

See the [user guide](USAGE.en.md#console-example) to run the example with a live account.

## Local NuGet package

```sh
dotnet pack src/LibrusApixNet/LibrusApixNet.csproj -c Release --no-build --no-restore
```

The package is written to `artifacts/packages`. The verification script checks metadata, contents and installation in a temporary project with a separate NuGet cache:

```powershell
$version = ./tools/Get-PackageVersion.ps1
./tools/Test-Package.ps1 -PackagePath "./artifacts/packages/LibrusApixNet.$version.nupkg"
```

You can also install the local package in an application. Add a NuGet source from the source directory:

```powershell
# PowerShell
dotnet nuget add source (Resolve-Path ./artifacts/packages).Path --name LibrusApixNet-local
```

```bash
# Bash: provide the absolute path to the packages directory
dotnet nuget add source "/absolute/path/to/artifacts/packages" --name LibrusApixNet-local
```

Then run `dotnet add package LibrusApixNet --version 1.6.0-beta2` in your application's directory. Keep NuGet.org enabled for the AngleSharp dependency. Alternatively, use a project reference to `src/LibrusApixNet/LibrusApixNet.csproj`.

## Upstream comparison

```sh
pwsh -NoProfile -File ./tools/Test-CompareUpstream.ps1
pwsh -NoProfile -File ./tools/Compare-Upstream.ps1 -SourcePath ../librus-apix -ShowDiff
pwsh -NoProfile -File ./tools/Compare-Upstream.ps1 -SourcePath ../librus-apix -FailOnChanges
```

`-SourcePath` points to a local Python checkout; quote paths containing spaces. The script does not clone upstream. The [porting guide](PORTING.en.md) describes the update procedure, symbol map and file hashes.

## Contributions and releases

See [CONTRIBUTING.md](../CONTRIBUTING.md) for bug reports and data sanitization. The [publishing guide](PUBLISHING.en.md) covers automated releases, Trusted Publishing and version numbers.
