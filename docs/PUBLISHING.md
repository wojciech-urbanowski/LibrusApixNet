# Publikacja na NuGet

[Polski](PUBLISHING.md) | [English](PUBLISHING.en.md) | [README](../README.md)

## Wersja i przebieg

[Version.props](../Version.props) zawiera `UpstreamVersion` i `BetaNumber`. Z tych wartości powstaje wersja `$(UpstreamVersion)-beta$(BetaNumber)`: obecnie `1.6.0-beta2`. Zmianę wersji Pythona poprzedź przeniesieniem kodu i aktualizacją [mapy upstreamu](upstream-map.json). Nie pobieramy numeru z PyPI podczas każdego builda, ponieważ nowy numer nie oznacza jeszcze zaktualizowanego portu.

Workflow [ci.yml](../.github/workflows/ci.yml) testuje Windows, Linux i macOS oraz sprawdza instalację paczki w osobnym projekcie. Zachowuje dokładnie tę paczkę, która przeszła weryfikację na Linux. Zadanie publikacji pobiera ją dopiero po sukcesie całej macierzy i wysyła na NuGet.org przez Trusted Publishing.

Publikacja działa dla `push` do `main` oraz ręcznego uruchomienia CI na `main`. Pull requesty i pozostałe gałęzie wykonują sprawdzenia bez publikacji. `--skip-duplicate` pomija wersje już wydane: zmiana kodu przy tym samym numerze nie zmienia pliku na NuGet. Dla kolejnej poprawki tego samego upstreamu zwiększ `BetaNumber` (np. do `2`), a dla nowego upstreamu ustaw jego wersję i przywróć `BetaNumber` do `1`.

## Pierwsza konfiguracja

1. Zaloguj się na [NuGet.org](https://www.nuget.org/users/account/LogOn) jako `wojciech-urbanowski`.
2. Otwórz menu profilu → **Trusted Publishing** → utwórz nową politykę GitHub.
3. Wybierz swoje konto jako właściciela i uzupełnij wartości:

| Pole | Wartość |
|---|---|
| Repository Owner | `wojciech-urbanowski` |
| Repository | `LibrusApixNet` |
| Workflow File | `ci.yml` (bez `.github/workflows/`) |
| Environment | pozostaw puste |
| Package pattern / glob | `LibrusApixNet` |
| Scopes | publikacja nowych paczek i nowych wersji |

4. Zapisz politykę. GitHub korzysta z OIDC; nie twórz stałego klucza API do tego workflow.
5. W [GitHub → Settings → Secrets and variables → Actions → Variables](https://github.com/wojciech-urbanowski/LibrusApixNet/settings/variables/actions) ustaw zmienne repozytorium:

| Zmienna | Wartość |
|---|---|
| `NUGET_USER` | `wojciech-urbanowski` (nazwa profilu NuGet, nie adres e-mail) |
| `NUGET_PUBLISH_ENABLED` | `true` po zapisaniu polityki |

6. W [Actions → CI](https://github.com/wojciech-urbanowski/LibrusApixNet/actions/workflows/ci.yml) wybierz **Run workflow**, gałąź `main`, a następnie **Run workflow**. Możesz również wysłać kolejny commit do `main`.
7. Po sukcesie zadania **Publish to NuGet.org** sprawdź [paczkę LibrusApixNet](https://www.nuget.org/packages/LibrusApixNet). Indeksowanie nie jest natychmiastowe; wersja beta może wymagać włączenia pokazywania prerelease w używanym kliencie.

Do chwili ustawienia obu zmiennych zadanie publikacji jest pomijane. Aby zatrzymać publikacje, ustaw `NUGET_PUBLISH_ENABLED` na `false`; CI nadal działa.

[Oficjalna instrukcja Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).

## Źródła i weryfikacja

Paczka jest objęta GPL-3.0-only. Kod źródłowy oraz pliki potrzebne do budowania są dostępne w [publicznym repozytorium GitHub](https://github.com/wojciech-urbanowski/LibrusApixNet), a metadane paczki wskazują commit użyty do jej zbudowania. Repozytorium powinno pozostać publiczne, aby odbiorcy paczki mieli dostęp do odpowiadających jej źródeł.

```powershell
dotnet pack src/LibrusApixNet/LibrusApixNet.csproj -c Release
$version = ./tools/Get-PackageVersion.ps1
./tools/Test-Package.ps1 -PackagePath "./artifacts/packages/LibrusApixNet.$version.nupkg"
```

Jeżeli logowanie OIDC nie działa, sprawdź nazwę profilu NuGet, zapisane uprawnienia polityki, właściciela repozytorium oraz nazwę `ci.yml`. Environment pozostaje pusty, ponieważ workflow nie używa GitHub Environment.
