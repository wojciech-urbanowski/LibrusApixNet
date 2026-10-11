# Mapa i utrzymanie portu

[Polski](PORTING.md) | [English](PORTING.en.md) | [README](../README.md)

Źródło: [RustySnek/librus-apix](https://github.com/RustySnek/librus-apix), autor oryginału: Pascal Jodłowski i współautorzy. Stan bazowy: wydanie **1.6.0**, commit **c54f05f3b483627328852825fff3e46fab27e87d**, 2026-10-10, `fix: replace [attr!=] selectors with :not() for soupsieve 3.0 compat`. Drzewo źródłowe było czyste. Port obejmuje wszystkie 15 modułów Pythona. Port C# i przygotowanie do publikacji: 2026-10-11, współtwórcy LibrusApixNet.

Licencja portu: `GPL-3.0-only`, zgodnie z plikiem licencji bazowego commita. Pełny tekst znajduje się w [LICENSE](../LICENSE), a autorstwo i rozbieżność ze starszym wpisem MIT w metadanych upstreamu opisuje [NOTICE.md](../NOTICE.md). Metadane dystrybucji i generowana dokumentacja Pythona nie są kopiowane do portu.

## Powiązania plików

Wszystkie pliki C# w tabeli znajdują się w `src/LibrusApixNet/`. Dokładne powiązania klas, metod publicznych i helperów są zapisane w [upstream-map.json](upstream-map.json).

| Python (`librus_apix/`) | C# | Główne punkty wejścia | Testy (`tests/LibrusApixNet.Tests/`) |
|---|---|---|---|
| `__init__.py` | `LibraryInfo.cs` | `LibraryInfo.GetVersion`, `Version` | standardowa wersja assembly |
| `client.py` | `Client.cs` | `Token`, `Client`, `GetTokenAsync`, `RefreshOAuthAsync`, `GetAsync`, `PostAsync`, `PostJsonAsync`, `NewClient` | `ClientTests.cs`, `GatewayGradeTests.cs` |
| `urls.py` | `Urls.cs` | `ClientOptions` | `ClientTests.cs`, `RequestTests.cs` |
| `helpers.py` | `Helpers.cs` | `Html.Parse`, wspólne selektory, tytuły i odnośniki | `ParserTests.cs` |
| `exceptions.py` | `Exceptions.cs` | wyjątki `*Exception` | `ClientTests.cs`, `ParserTests.cs`, `RequestTests.cs` |
| `grades.py` | `Grades.cs` | `Grades.GetGradesAsync`, `Parse`, `Grade`, `GradeDescriptive`, `Gpa`, `GradesResult`, helper gateway API | `ParserTests.cs`, `RequestTests.cs`, `GatewayGradeTests.cs` |
| `attendance.py` | `Attendance.cs` | `Attendances.GetAttendanceAsync`, `GetDetailAsync`, `GetGatewayAttendanceAsync`, `GetSubjectAttendanceAsync`, obliczenia frekwencji | `AttendanceTests.cs`, `ParserTests.cs`, `RequestTests.cs` |
| `timetable.py` | `Timetable.cs` | `Timetable.GetTimetableAsync`, `Parse`, `Period`, `PeriodChange` | `ParserTests.cs`, `RequestTests.cs` |
| `announcements.py` | `Announcements.cs` | `Announcements.GetAnnouncementsAsync`, `Parse` | `ParserTests.cs`, `RequestTests.cs` |
| `messages.py` | `Messages.cs` | `Messages.GetReceivedAsync`, `GetSentAsync`, `GetMaxPageNumberAsync`, `MessageContentAsync`, `RecipientGroupsAsync`, `GetRecipientsAsync`, `SendMessageAsync`, parsery | `ParserTests.cs`, `RequestTests.cs` |
| `homework.py` | `Homework.cs` | `HomeworkService.GetHomeworkAsync`, `HomeworkDetailAsync`, parsery | `ParserTests.cs`, `RequestTests.cs` |
| `schedule.py` | `Schedule.cs` | `Schedule.GetScheduleAsync`, `ScheduleDetailAsync`, `GetRecentlyAddedScheduleAsync`, parsery | `ParserTests.cs`, `RequestTests.cs` |
| `student_information.py` | `StudentInformation.cs` | `StudentInformationService.GetStudentInformationAsync`, `Parse` | `ParserTests.cs`, `RequestTests.cs` |
| `completed_lessons.py` | `CompletedLessons.cs` | `CompletedLessons.GetCompletedAsync`, `GetMaxPageNumberAsync`, `Parse` | `ParserTests.cs`, `RequestTests.cs` |
| `notifications.py` | `Notifications.cs` | `Notifications.GetNewTokenNotificationAmountsAsync`, `ParseBasicAmountAsync`, `GetInitialNotificationDataAsync`, `GetNewNotificationDataAsync`, `FilterNew` | `NotificationTests.cs` |

Nie wprowadzono jednej dużej klasy z całym API. Układ modułów odpowiada Pythonowi, a publiczne statyczne metody nadal przyjmują klienta. Helpery powtarzające się w kilku modułach przeniesiono do `Html` w `Helpers.cs`. Typ modelu `Attendance`, `Homework` lub `StudentInformation` i jego moduł nie mogą mieć tej samej nazwy w jednym namespace, stąd nazwy `Attendances`, `HomeworkService`, `StudentInformationService`.

## Konwencje i świadome różnice

| Obszar | Decyzja w porcie |
|---|---|
| Nazwy i I/O | PascalCase, `Task<T>`, sufiks `Async`, `CancellationToken`. Zamiast `requests`/`aiohttp`: jeden `HttpClient`, `CookieContainer`, konfigurowalny handler i proxy. |
| Modele | Rekordy C#. `grade` → `Mark`, `desc` → `Description`, `gpa` → `Average`, `API_Key` → `ApiKey`, pozostałe pola zwykle PascalCase. |
| Wyniki złożone | Krotki ocen, wysyłania, frekwencji i powiadomień zastępują `GradesResult`, `SendMessageResult`, `AttendanceFrequency`, `NotificationResult`. |
| Unia liczba/tekst | `Grade.Value`: `double?`, `null` dla ocen nieliczonych oraz nienumerycznych; `Grade.Mark` zachowuje tekst. Obsługiwane są liczby wielocyfrowe i dziesiętne z kropką lub przecinkiem. `Gpa.Average`: zachowany tekst, dodatkowo `NumericValue`; `StudentInformation.LuckyNumber`: `int?` zamiast `"?"`; `Event.Number`: `int?` zamiast `"unknown"`; `Lesson.LessonNumber`: parsowane `int` zamiast faktycznego tekstu źródła. |
| Plan lekcji | `DateOnly`, kontrola poniedziałku niezależna od języka systemu. Siedem list zawiera także puste okresy. `Period.Info` ma `PeriodChange?`, gdzie `null` odpowiada źródłowemu `""`. `Weekday` to angielskie nazwy jak w źródle. |
| Daty w modelach | Zachowano tekst, aby nie zgubić formatowania stron. Argumenty zakresów mają format `yyyy-MM-dd`, są sprawdzane przed żądaniem. Miesiąc/rok terminarza są liczbami z walidacją. |
| Parser HTML | AngleSharp wstawia `tbody` według HTML5. Selektory planu obsługują je jawnie. Niestandardowe BS4 `[class!='…']` zastąpiono standardowymi selektorami albo filtrowaniem elementów. |
| Oceny | Oba schematy opisowe ze źródła i podsumowanie I semestru. Kolumny zewnętrzne są wybierane przez bezpośrednie dzieci wiersza, aby zagnieżdżone tabelki nie przesuwały semestrów. Tekst oceny jest przycinany. Opis zachowuje sposób traktowania `<br/>` źródła. Nieznany schemat podsumowania II semestru nadal nie jest implementowany. |
| Oceny opisowe gateway (1.6.0) | Znacznik `newSchemaGradesByStudentTable` uruchamia odświeżenie OAuth, odczyt tokena/użytkownika i POST JSON ocen. Uczeń przypisany do rodzica jest wybierany przed identyfikatorem użytkownika. Nazwy przedmiotów pochodzą najpierw z HTML, potem z API; nauczyciele z API użytkowników. Dane API uzupełniają puste wyniki HTML. Błędy opcjonalnego API pozostawiają wynik HTML, ale anulowanie przez wywołującego jest propagowane. `Grades.Parse` pozostaje parserem offline. |
| Wysyłanie wiadomości | Status odczytywany z odpowiedzi HTTP, a nie z obiektu HTML (błąd źródła). Obsługa zarówno `nie zostala`, jak i `nie została`. Lista odbiorców koduje powtarzane `DoKogo` jak `requests`. |
| Wiadomości wysłane | `Unread` odczytywane z tekstu komórki `NIE`; Python porównywał obiekt `Tag` do napisu i zawsze zwracał `False`. |
| Terminarz | Godzina `HH:mm` rozpoznawana przed numerem lekcji; Python potrafił uznać minuty za numer lekcji. Zachowano `Hour == "unknown"`, jeśli godziny brak. |
| Puste dane | Puste listy wiadomości, ogłoszeń i zadań są poprawnymi wynikami. Brak ogłoszeń przy inicjalizacji powiadomień nie powoduje indeksowania pustej listy. |
| Ciasteczka | Oddzielne kontenery klientów, domeny zgodne z URL. Podmiana tokena aktualizuje istniejące ciasteczka domenowe i usuwa wcześniejszy OAuth, gdy nowy token go nie zawiera. Odświeżenie OAuth wymaga nowego `Set-Cookie`, nie akceptuje starej wartości z kontenera. |
| Żądania HTTP | Metody modułów sprawdzają sukces HTTP przed parsowaniem (wysyłanie zwraca wynik powodzenia). Metody transportowe klienta oddają surową odpowiedź. Maintenance obsługuje także odpowiedź 503 bez JSON. |
| Frekwencja | Ogólna: ułamki 0–1; przedmiotowa: procenty 0–100. Zachowano różne traktowanie wycieczek w tych dwóch algorytmach. Nieznany typ JSON ma `unknown`, zamiast błędu indeksowania słownika. |
| Przedmioty z API | Wspólny klient/proxy; do ośmiu równoległych żądań, cache zadań dla lekcji i przedmiotów, maks. pięć prób z opóźnieniami 0.5/1/2/4 s dla przejściowych błędów HTTP/transportu. Błędy autoryzacji i anulowanie nie są ponawiane. |
| Powiadomienia | Trasa menu określa kategorię, niezależnie od kolejności pozycji. Brak kategorii daje pustą listę. Brak współdzielonych domyślnych list; wejściowy `NotificationIds` jest kopiowany. Zapisane ID terminarza zapobiegają ponownemu zgłoszeniu pierwszej partii. Wiadomości przeczytane także tworzą stan bazowy. |
| Identyfikatory powiadomień | Zachowano href, `title + date` dla ogłoszeń i MD5 tekstu dla terminarza. Ogłoszenia, zadania i wiadomości zatrzymują odczyt na pierwszym widzianym ID, więc kolejność wejściowa nadal ma znaczenie. Descriptive grades nie trafiają do powiadomień, tak jak w źródle. |
| Wersja biblioteki | Wersja portu ma format `<UpstreamVersion>-beta<BetaNumber>` z `Version.props`; `LibraryInfo.Version` może zawierać także metadane commita assembly. Powiązanie ze źródłem wyznaczają wersja i SHA w tej mapie. |

## Aktualizacja

1. Zaktualizuj lokalne źródło Pythona. Uruchom `pwsh -NoProfile -File ./tools/Compare-Upstream.ps1 -SourcePath ../librus-apix -ShowDiff` z katalogu głównego portu, zastępując `../librus-apix` własną lokalizacją. Skrypt porównuje treść wszystkich modułów oraz testów z SHA-256 w mapie; normalizuje CRLF do LF. Pokazuje także nowe i usunięte pliki. Nowy moduł ma `UNMAPPED`.
2. Dla zmienionych plików sprawdź `modules[].symbols` i wpisy w powyższej tabeli. Zmiany tytułów, popupów i filtrów mogą wymagać także `Helpers.cs`.
3. Przenieś nowe zachowanie i scenariusze źródłowych testów do odpowiadających plików C#. Zachowaj świadome poprawki portu albo jawnie zmień opis różnic. Nowe funkcje dopisz do mapy symboli.
4. Uruchom `dotnet test -c Release` i `dotnet build -c Release`. Publiczne parsery `Parse*` pozwalają sprawdzać zapisany HTML bez konta. Nowe schematy utrwalaj w `Fixtures.cs` albo jako zanonimizowane pliki testowe.
5. Po przeniesieniu i sprawdzeniu wszystkich zmian uaktualnij wersję, `commit`, datę, opis oraz skróty plików w `upstream-map.json` i ustaw odpowiadający `UpstreamVersion` w `Version.props`. Dla nowego wydania upstreamu ustaw `BetaNumber` na `1`. Nie przesuwaj bazowego commita, jeśli jakaś zmiana nadal czeka na port. Skrót to SHA-256 bajtów UTF-8 tekstu po `.Replace("\r\n", "\n")` — sposób liczenia znajduje się w `Get-NormalizedHash` w skrypcie.
6. Uruchom skrypt ponownie; brak zmian potwierdza zgodność z zapisanym stanem źródła, a nie samodzielnie poprawność portu. `-FailOnChanges` zwraca kod 1, gdy źródło ma zmiany. `-PassThru` zwraca obiekty zamiast tabeli.

Do samego portu i testów potrzebny jest tylko SDK .NET 10. Narzędzie porównania korzysta z PowerShell 7 na Windows, Linux i macOS; opcjonalny `-ShowDiff` wymaga repozytorium Git zawierającego commit bazowy. `-SourcePath` jest wymagany, może być względny lub absolutny i powinien być ujęty w cudzysłowy, jeśli zawiera spacje. Brak argumentu powoduje błąd bez pytania interaktywnego. Skrypt nie klonuje upstreamu.

`pwsh -NoProfile -File ./tools/Test-CompareUpstream.ps1` sprawdza narzędzie na tymczasowym, syntetycznym repozytorium bez konta Librusa ani kopii upstreamu. Sprawdza ścieżki ze spacjami, CRLF/LF, pliki zmienione/dodane/usunięte, ignorowanie `__pycache__`, `-PassThru`, `-ShowDiff` i kody wyjścia. `pwsh -NoProfile -File ./tools/Test-Package.ps1 -PackagePath ./artifacts/packages/LibrusApixNet.1.6.0-beta2.nupkg` weryfikuje metadane i instalację paczki w izolowanym projekcie.

CI wykonuje sprawdzenia na Windows, Linux i macOS. Zgodność z danym systemem potwierdza dopiero pomyślne wykonanie jego zadania CI. Testy automatyczne nie logują się do prawdziwego Librusa ani nie potwierdzają wszystkich wariantów szkolnych stron. Zakres 2FA pozostaje taki jak w Pythonie: wywołanie endpointu z istniejącego przepływu, bez interaktywnego wpisywania kodu. Zasady anonimizacji danych do testów opisuje [CONTRIBUTING.md](../CONTRIBUTING.md).
