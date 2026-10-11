# Przewodnik użytkownika

[Polski](USAGE.md) | [English](USAGE.en.md) | [README](../README.md)

Instalację z NuGet i podstawowy przykład logowania opisuje [README](../README.md#szybki-start). Poniższe przykłady korzystają z klienta `client` z aktywną sesją.

## Sesja i konfiguracja klienta

Wcześniej zapisany klucz sesji można przekazać bez logowania:

```csharp
using var client = new Client(new Token(apiKey)); // DZIENNIKSID:SDZIENNIKSID
```

Każda operacja sieciowa przyjmuje `CancellationToken`. `Client` utrzymuje jedną sesję HTTP i implementuje `IDisposable`. Odpowiedzi zwracane bezpośrednio przez `Client.GetAsync`, `PostAsync` i `PostJsonAsync` trzeba zwalniać; pozostałe metody robią to wewnętrznie. Opcjonalny `HttpMessageHandler` pozwala podpiąć testowy transport i jest zwalniany wraz z klientem. Przy własnym handlerze konfiguracja przekierowań należy do niego; domyślny handler obsługuje przekierowania i ciasteczka automatycznie.

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
// Wysyłanie wiadomości:
// var sent = await Messages.SendMessageAsync(client, "Temat", "Treść", [recipientId]);
```

`GradesResult` zawiera `Semesters`, `Averages` i `Descriptive`. Dwa semestry zajmują indeksy 0 i 1. `Gpa.Semester == 0` oznacza średnią roczną. `Grade.Value` zwraca `double?`: liczby całkowite i dziesiętne (także z przecinkiem), `+0.5` dla plusa i `-0.25` dla minusa. Zwraca `null` dla ocen nieliczonych do średniej oraz nienumerycznych, takich jak `85%`, `85/100` czy `A+`; oryginalny tekst pozostaje w `Grade.Mark`.

`Grades.GetGradesAsync` obsługuje także schemat, w którym oceny opisowe są pobierane z gateway API: odświeża OAuth, ustala identyfikator ucznia i dopisuje dane API tam, gdzie HTML nie zawiera ocen. Błędy opcjonalnego API pozostawiają wynik HTML; anulowanie wywołania jest propagowane. Sam parser `Grades.Parse` nie wykonuje żądań sieciowych. `Client.PostJsonAsync` umożliwia wysyłanie JSON; odpowiedź zwalnia wywołujący.

Zachowane osobliwości źródła: `Attendance.Semester` jest indeksem bloku HTML, liczonym od zera; wynik listy jest odwracany, gdy strona zawiera dwa semestry. `Messages.GetMaxPageNumberAsync` zwraca ostatni indeks strony, a analogiczna metoda `CompletedLessons` zwraca liczbę stron. Szczegóły i wszystkie różnice opisuje [mapa portu](PORTING.md).

## Powiadomienia

```csharp
var initial = await Notifications.GetInitialNotificationDataAsync(client); // raz na nowy token
var next = await Notifications.GetNewNotificationDataAsync(client, initial.Seen);

var stateJson = System.Text.Json.JsonSerializer.Serialize(next.Seen);
// Zapisz next.Seen i użyj go przy następnym odczycie.
```

Zwrócony stan zawiera identyfikatory już widzianych danych. Stan wejściowy nie jest modyfikowany. Liczniki na pulpicie są przypisane do tokena, więc kolejne odczyty korzystają z poszczególnych modułów. Datę zakresu zadań wyznacza `ClientOptions.TimeProvider` w jego lokalnej strefie czasowej.

## Przykład konsolowy

Przykład jest dostępny w [examples/QuickStart](../examples/QuickStart). Polecenia uruchamiane są z katalogu głównego sklonowanego repozytorium i wymagają SDK .NET 10. Pomoc nie wymaga konta:

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
