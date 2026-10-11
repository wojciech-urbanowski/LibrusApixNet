# Współpraca / Contributing

[README po polsku](README.md) | [English README](README.en.md)

## Polski

Zgłoszenie błędu powinno zawierać wersję portu, system, wynik `dotnet --info`, użyty moduł, kroki odtworzenia i oczekiwany wynik. Dodaj minimalny przykład oraz zanonimizowany fragment HTML/JSON, jeżeli problem dotyczy parsera.

Przed publikacją usuń login, hasło, klucze sesji, ciasteczka (`DZIENNIKSID`, `SDZIENNIKSID`, `oauth_token`), tokeny CSRF, nagłówki autoryzacyjne, imiona i nazwiska, adresy e-mail, nazwy szkoły i klasy oraz inne dane pozwalające rozpoznać osoby. Zastąp identyfikatory i odnośniki syntetycznymi wartościami, zachowując ich format i powiązania. Zachowaj strukturę HTML, klasy CSS i etykiety potrzebne parserowi. Nie załączaj surowych plików HAR, sesji przeglądarki ani zrzutów dziennika. Sprawdź ręcznie także tytuły, ukryte pola formularzy i osadzone skrypty.

Nowe schematy stron utrwalaj jako syntetyczne albo zanonimizowane scenariusze testowe. Zachowaj publiczne API, `CancellationToken` i zależności dostępne na Windows, Linux i macOS. Zmiany przeniesione z Pythona dopisz do [mapy portu](docs/PORTING.md); opisuj świadome różnice względem upstreamu.

Przed wysłaniem zmian uruchom kompilację i testy Release, pakowanie oraz oba skrypty sprawdzające opisane w [przewodniku dla współtwórców](docs/DEVELOPMENT.md). CI powtarza te sprawdzenia na trzech systemach. Nie uruchamiaj automatycznych testów na rzeczywistym koncie Librusa.

Zmiany do projektu są udostępniane na licencji `GPL-3.0-only`. Zachowuj informacje o autorstwie i pochodzeniu materiałów.

## English

A bug report should include the port version, operating system, `dotnet --info`, the affected module, reproduction steps and expected result. Include a minimal example and a sanitized HTML/JSON fragment when reporting a parser issue.

Before publishing, remove usernames, passwords, session keys, cookies (`DZIENNIKSID`, `SDZIENNIKSID`, `oauth_token`), CSRF tokens, authorization headers, personal names, email addresses, school/class names and other identifying data. Replace IDs and links with synthetic values while preserving their format and relationships. Keep the HTML structure, CSS classes and labels required by the parser. Do not attach raw HAR files, browser sessions or school register dumps. Also inspect tooltips, hidden form fields and embedded scripts manually.

Capture new page layouts in synthetic or sanitized regression fixtures. Preserve the public API, `CancellationToken` support and dependencies available on Windows, Linux and macOS. Record changes ported from Python in the [porting guide](docs/PORTING.en.md), including deliberate differences from upstream.

Before submitting a change, run the Release build and tests, pack the library and run both verification scripts documented in the [development guide](docs/DEVELOPMENT.en.md). CI repeats these checks on all three systems. Automated tests must not access a live Librus account.

Contributions are distributed under `GPL-3.0-only`. Preserve attribution and provenance notices.
