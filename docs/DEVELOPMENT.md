# Budowanie i rozwój projektu

[Polski](DEVELOPMENT.md) | [English](DEVELOPMENT.en.md) | [README](../README.md)

Ten przewodnik opisuje pracę ze źródłami biblioteki. Instalację gotowej paczki w aplikacji opisuje [README](../README.md#instalacja).

## Wymagania

- SDK .NET 10, co najmniej 10.0.100; wybór wersji opisuje [global.json](../global.json).
- PowerShell 7 (`pwsh`) do skryptów utrzymania i weryfikacji.
- Git do klonowania repozytorium, testu porównania i opcji `-ShowDiff`.

Pliki są zapisywane jako UTF-8 z zakończeniami linii LF. Projekt korzysta z `net10.0` na Windows, Linux i macOS.

## Budowanie i testy

```sh
git clone https://github.com/wojciech-urbanowski/LibrusApixNet.git
cd LibrusApixNet
dotnet restore LibrusApixNet.slnx
dotnet build LibrusApixNet.slnx -c Release --no-restore
dotnet test LibrusApixNet.slnx -c Release --no-build --no-restore
dotnet run --project examples/QuickStart -c Release --no-build -- --help
```

Testy używają syntetycznych HTML/JSON i kontrolowanego transportu HTTP. Nie wymagają konta Librusa i nie łączą się z serwisem. GitHub Actions wykonuje sprawdzenia na Windows, Linux i macOS.

Uruchomienie przykładu z rzeczywistym kontem opisuje [przewodnik użytkownika](USAGE.md#przykład-konsolowy).

## Lokalna paczka NuGet

```sh
dotnet pack src/LibrusApixNet/LibrusApixNet.csproj -c Release --no-build --no-restore
```

Paczka powstaje w `artifacts/packages`. Skrypt weryfikuje metadane, zawartość oraz instalację w tymczasowym projekcie z osobnym cache NuGet:

```powershell
$version = ./tools/Get-PackageVersion.ps1
./tools/Test-Package.ps1 -PackagePath "./artifacts/packages/LibrusApixNet.$version.nupkg"
```

Lokalną paczkę można też zainstalować we własnej aplikacji. Z katalogu źródeł dodaj źródło NuGet:

```powershell
# PowerShell
dotnet nuget add source (Resolve-Path ./artifacts/packages).Path --name LibrusApixNet-local
```

```bash
# Bash: podaj absolutną ścieżkę do katalogu paczek
dotnet nuget add source "/absolute/path/to/artifacts/packages" --name LibrusApixNet-local
```

Następnie w katalogu aplikacji wykonaj `dotnet add package LibrusApixNet --version 1.6.0-beta1`. NuGet.org pozostaje źródłem zależności AngleSharp. Alternatywnie można użyć odwołania projektowego do `src/LibrusApixNet/LibrusApixNet.csproj`.

## Porównanie z upstreamem

```sh
pwsh -NoProfile -File ./tools/Test-CompareUpstream.ps1
pwsh -NoProfile -File ./tools/Compare-Upstream.ps1 -SourcePath ../librus-apix -ShowDiff
pwsh -NoProfile -File ./tools/Compare-Upstream.ps1 -SourcePath ../librus-apix -FailOnChanges
```

`-SourcePath` wskazuje lokalną kopię Pythona; ścieżki ze spacjami wymagają cudzysłowów. Skrypt nie klonuje upstreamu. Procedurę przenoszenia zmian, mapę symboli i skróty plików opisuje [dokumentacja portu](PORTING.md).

## Współpraca i wydania

Zasady zgłaszania błędów i anonimizowania danych są w [CONTRIBUTING.md](../CONTRIBUTING.md). Konfigurację automatycznych publikacji, Trusted Publishing i numerację wersji opisuje [przewodnik publikacji](PUBLISHING.md).
