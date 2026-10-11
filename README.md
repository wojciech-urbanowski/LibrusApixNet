# LibrusApixNet

[Polski](README.md) | [English](README.en.md) | [NuGet](https://www.nuget.org/packages/LibrusApixNet)

Biblioteka C# dla .NET 10 umożliwiająca korzystanie z danych Librus Synergia. Jest portem [librus-apix](https://github.com/RustySnek/librus-apix) i działa na Windows, Linux oraz macOS.

Autorem oryginalnej biblioteki **librus-apix** jest **Pascal Jodłowski**; projekt rozwijają również jego współautorzy. Port zachowuje licencję **GNU GPL v3** (`GPL-3.0-only`) z [pliku LICENSE oryginału](https://github.com/RustySnek/librus-apix/blob/c54f05f3b483627328852825fff3e46fab27e87d/LICENSE). Autorstwo i pochodzenie kodu opisuje [NOTICE.md](NOTICE.md).

## Możliwości

- Logowanie i korzystanie z zapisanej sesji.
- Oceny numeryczne i opisowe, średnie oraz frekwencja.
- Plan lekcji, terminarz, zadania domowe i zrealizowane lekcje.
- Ogłoszenia, odczytywanie i wysyłanie wiadomości.
- Informacje o uczniu i wykrywanie nowych danych.

## Instalacja

Dodaj [paczkę NuGet](https://www.nuget.org/packages/LibrusApixNet/1.6.0-beta2) do projektu aplikacji:

```sh
dotnet add package LibrusApixNet --version 1.6.0-beta2
```

Biblioteka wymaga **.NET 10**. Aktualne wydanie `1.6.0-beta2` jest wersją beta portu `librus-apix` 1.6.0. Budowanie, testy i instalacja paczki są sprawdzane na wszystkich trzech systemach w [GitHub Actions](https://github.com/wojciech-urbanowski/LibrusApixNet/actions/workflows/ci.yml).

## Szybki start

Przykład loguje się i odczytuje oceny z pierwszego semestru. Dane logowania pobiera ze zmiennych środowiskowych `LIBRUS_USERNAME` i `LIBRUS_PASSWORD`.

```csharp
using LibrusApixNet;

var username = Environment.GetEnvironmentVariable("LIBRUS_USERNAME")
    ?? throw new InvalidOperationException("Ustaw LIBRUS_USERNAME.");
var password = Environment.GetEnvironmentVariable("LIBRUS_PASSWORD")
    ?? throw new InvalidOperationException("Ustaw LIBRUS_PASSWORD.");

using var client = new Client();
await client.GetTokenAsync(username, password);

var result = await Grades.GetGradesAsync(client);
foreach (var (subject, grades) in result.Semesters[0])
    foreach (var grade in grades)
        Console.WriteLine($"{subject}: {grade.Mark} ({grade.Date})");
```

Można też użyć zapisanej sesji przez `new Client(new Token(apiKey))`. Każda operacja sieciowa obsługuje `CancellationToken`, a klient powinien być zwalniany przez `using`.

[Przewodnik użytkownika](docs/USAGE.md) zawiera konfigurację sesji i proxy, pozostałe operacje, powiadomienia oraz przykład konsolowy z poleceniami dla PowerShell i Bash.

## Status projektu

Projekt jest niezależny i nieoficjalny; nie jest powiązany z firmą Librus ani wspierany przez autorów oryginału. Testy korzystają z syntetycznych HTML/JSON i kontrolowanego transportu HTTP. Nie obejmują weryfikacji na rzeczywistym koncie Librusa ani wszystkich wariantów stron szkolnych.

## Zgłaszanie błędów i współpraca

Błędy i propozycje zmian można zgłaszać w [Issues](https://github.com/wojciech-urbanowski/LibrusApixNet/issues). Zgłoszenia powinny zawierać minimalny przykład i zanonimizowane dane, bez haseł, tokenów ani ciasteczek sesji. Szczegóły opisuje [CONTRIBUTING.md](CONTRIBUTING.md).

Instrukcje budowania i testowania są w [przewodniku dla współtwórców](docs/DEVELOPMENT.md), a zgodność z oryginałem opisuje [dokumentacja portu](docs/PORTING.md).

## Licencja i pochodzenie

LibrusApixNet jest udostępniany na licencji **GPL-3.0-only**, bez gwarancji. Oryginalną bibliotekę `librus-apix` stworzył Pascal Jodłowski wraz ze współautorami; port C# rozwijają współtwórcy LibrusApixNet. Bazą tego wydania jest `librus-apix` 1.6.0.

Pełny tekst licencji znajduje się w [LICENSE](LICENSE). [NOTICE.md](NOTICE.md) zawiera bazowy commit, datę zmian portu, informacje o autorstwie i rozbieżności licencyjnej w metadanych oryginału. Zależność [AngleSharp](https://www.nuget.org/packages/AngleSharp/1.8.4) jest objęta licencją MIT.
