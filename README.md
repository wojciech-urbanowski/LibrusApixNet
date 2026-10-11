# LibrusApixNet

[Polski](README.md) | [English](README.en.md)

Port biblioteki [librus-apix autorstwa Pascala Jodłowskiego i współautorów](https://github.com/RustySnek/librus-apix) do C# na **.NET 10**. Zawiera logowanie i sesję, oceny numeryczne i opisowe, frekwencję, plan lekcji, ogłoszenia, wiadomości i odbiorców, zadania domowe, terminarz, informacje o uczniu, zrealizowane lekcje oraz powiadomienia.

Projekt jest niezależny i nieoficjalny, bez powiązania z firmą Librus ani poparcia ze strony autorów upstreamu. Port udostępniają współtwórcy LibrusApixNet na licencji **GPL-3.0-only**. Pełny tekst zawiera [LICENSE](LICENSE), a pochodzenie, datę zmian i informacje o zależnościach opisuje [NOTICE.md](NOTICE.md).

## Wymagania i platformy

- SDK .NET 10 do budowania i testowania; .NET 10 w aplikacji korzystającej z biblioteki.
- Windows, Linux i macOS. Target `net10.0` nie zawiera zależności od API Windows ani wymuszonego RID.
- PowerShell 7 (`pwsh`) do opcjonalnych narzędzi utrzymania i sprawdzania paczki; Git dodatkowo do `-ShowDiff` i testu skryptu porównania.

GitHub Actions wykonuje kompilację, testy, pakowanie i instalację paczki na wszystkich trzech systemach. Działanie na Linux i macOS jest potwierdzone dopiero po pomyślnym wykonaniu odpowiednich zadań CI; lokalne sprawdzenie Windows ich nie zastępuje.

## Budowanie i instalacja

Poniższe polecenia działają w PowerShell i Bash, z katalogu głównego projektu:

```sh
dotnet restore LibrusApixNet.slnx
dotnet build LibrusApixNet.slnx -c Release --no-restore
dotnet test LibrusApixNet.slnx -c Release --no-build --no-restore
dotnet pack src/LibrusApixNet/LibrusApixNet.csproj -c Release --no-build --no-restore
```

Paczka `LibrusApixNet.1.6.0-beta1.nupkg` powstaje w `artifacts/packages`. Samo `dotnet pack` nie wysyła jej na NuGet.org. Po włączeniu publikacji w GitHub Actions każda nowa wersja jest publikowana po pomyślnym CI na `main`; opis konfiguracji znajduje się w [przewodniku publikacji](docs/PUBLISHING.md).

Wersję ustala [Version.props](Version.props): `UpstreamVersion` + `-beta` + `BetaNumber`. Obecnie jest to port `librus-apix` **1.6.0**, beta **1**. Wersję upstreamu zmieniaj dopiero po przeniesieniu jego zmian. NuGet nie pozwala nadpisać wydanej wersji; ponowne uruchomienia ją pomijają. Poprawki tej samej wersji upstreamu wymagają zwiększenia `BetaNumber`.

Po publikacji zainstalujesz bibliotekę przez `dotnet add package LibrusApixNet --version 1.6.0-beta1`. Do instalacji lokalnej dodaj katalog paczek jako źródło, pozostawiając aktywne NuGet.org dla AngleSharp. Zastąp ścieżkę przykładową własną ścieżką absolutną:

```powershell
# PowerShell: uruchom z katalogu źródeł
dotnet nuget add source (Resolve-Path ./artifacts/packages).Path --name LibrusApixNet-local
```

```bash
# Bash: zastąp ścieżkę własną lokalizacją
dotnet nuget add source "/absolute/path/to/artifacts/packages" --name LibrusApixNet-local
```

Następnie, w katalogu aplikacji:

```sh
dotnet add package LibrusApixNet --version 1.6.0-beta1
```

## Użycie

Zamiast paczki możesz dodać odwołanie projektowe do `src/LibrusApixNet/LibrusApixNet.csproj`.

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

Wcześniej zapisany klucz sesji można przekazać bez logowania:

```csharp
using var client = new Client(new Token(apiKey)); // DZIENNIKSID:SDZIENNIKSID
```

Każda operacja sieciowa przyjmuje `CancellationToken`. `Client` utrzymuje jedną sesję HTTP i implementuje `IDisposable`. Odpowiedzi zwracane bezpośrednio przez `Client.GetAsync` i `PostAsync` trzeba zwalniać; pozostałe metody robią to wewnętrznie. Opcjonalny `HttpMessageHandler` pozwala podpiąć testowy transport i jest zwalniany wraz z klientem. Przy własnym handlerze konfiguracja przekierowań należy do niego; domyślny handler obsługuje przekierowania i ciasteczka automatycznie.

Adresy i proxy konfiguruje się przed utworzeniem klienta:

```csharp
using System.Net;

var options = new ClientOptions
{
    Proxy = new WebProxy("http://localhost:8080"),
    Timeout = TimeSpan.FromSeconds(30)
};
using var client = new Client(new Token(apiKey), options);
```

`BaseUrl` i `ApiUrl` wyznaczają domyślne endpointy; każdy `*Url` można nadpisać osobno. `Client.Cookies` pozwala dodać dodatkowe ciasteczka z właściwą domeną. Ciasteczka autoryzacyjne są przypisane do `BaseUrl`.

## Pozostałe operacje

```csharp
var week = await Timetable.GetTimetableAsync(client, new DateOnly(2026, 10, 5));
var attendance = await Attendances.GetAttendanceAsync(client, "week");
var frequency = await Attendances.GetAttendanceFrequencyAsync(client); // ułamki 0–1
var subjects = await Attendances.GetSubjectFrequencyAsync(client);    // procenty 0–100
var homework = await HomeworkService.GetHomeworkAsync(client, "2026-10-01", "2026-10-31");
var schedule = await Schedule.GetScheduleAsync(client, month: 10, year: 2026, includeEmpty: true);
var lessons = await CompletedLessons.GetCompletedAsync(client, "2026-10-01", "2026-10-31");
var received = await Messages.GetReceivedAsync(client, page: 0);
var groups = await Messages.RecipientGroupsAsync(client);
var recipients = await Messages.GetRecipientsAsync(client, groups[0]);
// Wysyłanie jest jawną operacją:
// var sent = await Messages.SendMessageAsync(client, "Temat", "Treść", [recipientId]);
```

`GradesResult` zawiera `Semesters`, `Averages` i `Descriptive`. Dwa semestry zajmują indeksy 0 i 1. `Gpa.Semester == 0` oznacza średnią roczną. `Grade.Value` zwraca `double?`: liczby całkowite i dziesiętne (także z przecinkiem), `+0.5` dla plusa i `-0.25` dla minusa. Zwraca `null` dla ocen nieliczonych do średniej oraz nienumerycznych, takich jak `85%`, `85/100` czy `A+`; oryginalny tekst pozostaje w `Grade.Mark`.

`Grades.GetGradesAsync` obsługuje także schemat, w którym oceny opisowe są pobierane z gateway API: odświeża OAuth, ustala identyfikator ucznia i dopisuje dane API tam, gdzie HTML nie zawiera ocen. Błędy opcjonalnego API pozostawiają wynik HTML; anulowanie wywołania jest propagowane. Sam parser `Grades.Parse` nie wykonuje żądań sieciowych. `Client.PostJsonAsync` umożliwia wysyłanie JSON; odpowiedź zwalnia wywołujący.

Zachowane osobliwości źródła: `Attendance.Semester` jest indeksem bloku HTML, liczonym od zera; wynik listy jest odwracany, gdy strona zawiera dwa semestry. `Messages.GetMaxPageNumberAsync` zwraca ostatni indeks strony, a analogiczna metoda `CompletedLessons` zwraca liczbę stron. Szczegóły i wszystkie różnice opisuje [mapa portu](docs/PORTING.md).

## Powiadomienia

```csharp
var initial = await Notifications.GetInitialNotificationDataAsync(client); // raz na nowy token
var next = await Notifications.GetNewNotificationDataAsync(client, initial.Seen);

var stateJson = System.Text.Json.JsonSerializer.Serialize(next.Seen);
// Zapisz next.Seen i użyj go przy następnym odczycie.
```

Zwrócony stan zawiera identyfikatory już widzianych danych. Stan wejściowy nie jest modyfikowany. Liczniki na pulpicie są przypisane do tokena, więc kolejne odczyty korzystają z poszczególnych modułów. Datę zakresu zadań wyznacza `ClientOptions.TimeProvider` w jego lokalnej strefie czasowej.

## Testy i aktualizacje źródła

Testy używają reprezentatywnych HTML/JSON i kontrolowanego transportu HTTP, bez konta ani połączenia z Librusem. Dwa scenariusze ocen opisowych pochodzą z testów źródła. Testy nie potwierdzają działania na aktualnym koncie ani wszystkich wariantów stron używanych przez szkoły.

Port bazuje na commicie `c54f05f3b483627328852825fff3e46fab27e87d` z 2026-10-10. [Mapa symboli i skróty plików](docs/upstream-map.json) oraz [procedura aktualizacji](docs/PORTING.md) pozwalają porównać kolejną wersję:

```powershell
pwsh -NoProfile -File ./tools/Compare-Upstream.ps1 -SourcePath ../librus-apix
pwsh -NoProfile -File ./tools/Compare-Upstream.ps1 -SourcePath ../librus-apix -ShowDiff
pwsh -NoProfile -File ./tools/Compare-Upstream.ps1 -SourcePath ../librus-apix -FailOnChanges
```

`-SourcePath` musi wskazywać Twoją lokalną kopię źródła Pythona. Skrypt nie pobiera jej automatycznie i nie zna prywatnych ścieżek autora. Do sprawdzenia narzędzi bez tej kopii:

```sh
pwsh -NoProfile -File ./tools/Test-CompareUpstream.ps1
pwsh -NoProfile -File ./tools/Test-Package.ps1 -PackagePath ./artifacts/packages/LibrusApixNet.1.6.0-beta1.nupkg
```

Test paczki tworzy tymczasowy projekt z osobnym cache NuGet, wymusza instalację LibrusApixNet z lokalnej paczki i pobiera zależność AngleSharp z NuGet.org. Nie łączy się z Librusem.

Kod źródłowy i pliki budowania są dostępne w [publicznym repozytorium GitHub](https://github.com/wojciech-urbanowski/LibrusApixNet). Metadane paczki wskazują repozytorium i commit użyty do jej zbudowania.

## Przykład konsolowy

Pomoc nie wymaga konta:

```sh
dotnet run --project examples/QuickStart -- --help
```

Do odczytu rzeczywistych danych ustaw klucz sesji albo nazwę użytkownika i hasło. Przykładowe wartości poniżej zastąp własnymi; nie zapisuj danych logowania w kodzie ani zgłoszeniach błędów.

```powershell
# PowerShell
$env:LIBRUS_USERNAME = 'twoj-login'
$env:LIBRUS_PASSWORD = 'twoje-haslo'
dotnet run --project examples/QuickStart -c Release
# Alternatywnie w nowej sesji: $env:LIBRUS_API_KEY = 'DZIENNIKSID:SDZIENNIKSID'
```

```bash
# Bash
export LIBRUS_USERNAME='twoj-login'
export LIBRUS_PASSWORD='twoje-haslo'
dotnet run --project examples/QuickStart -c Release
# Alternatywnie w nowej sesji: export LIBRUS_API_KEY='DZIENNIKSID:SDZIENNIKSID'
```

Jeśli `LIBRUS_API_KEY` jest ustawiony, przykład używa go zamiast nazwy użytkownika i hasła. Przykład pobiera informacje o uczniu, oceny i ogłoszenia; wypisuje dane osobowe na konsolę.

## Licencja i współpraca

Port bazuje na GPL v3 z pliku licencji upstreamu; sprzeczny wpis MIT w jego metadanych odnotowano w [NOTICE.md](NOTICE.md). Biblioteka jest udostępniana bez gwarancji. Zależność parsera, [AngleSharp 1.8.4](https://www.nuget.org/packages/AngleSharp/1.8.4), zachowuje własną licencję MIT i używa selektorów CSS odpowiadających parserom BeautifulSoup.

Instrukcje zgłaszania błędów, anonimizowania HTML i przygotowania zmian: [CONTRIBUTING.md](CONTRIBUTING.md).
