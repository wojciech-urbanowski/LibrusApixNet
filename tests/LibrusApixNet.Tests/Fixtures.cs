namespace LibrusApixNet.Tests;

internal static class Fixtures
{
    public const string NumericGrades = """
        <table><tr class="line0"><td class="center micro screen-only"></td><td>Matematyka</td>
          <td class="grade-cell"><span class="grade-box"><a href="/grade/1" title="Kategoria: Test<br>Data: 2026-09-21<br>Nauczyciel: Anna<br>Waga: 2<br>Licz do średniej: tak">5+</a></span>
            <span><span class="grade-box"><a href="/grade/2" title="Kategoria: Poprawa<br>Data: 2026-09-22<br>Nauczyciel: Anna<br>Waga: 1<br>Licz do średniej: nie">nb</a></span></span></td>
          <td></td><td></td>
          <td class="grade-cell"><span class="grade-box"><a href="/grade/3" title="Kategoria: Test<br>Data: 2026-09-23<br>Nauczyciel: Anna<br>Waga: 3<br>Licz do średniej: tak">4-</a></span></td>
          <td></td><td></td><td class="right">5,50</td><td class="right">3.75</td><td class="right">4.625</td>
        </tr></table>
        """;

    // The two descriptive fixtures below are taken from the regression scenarios in upstream test_grades.py.
    public const string OldDescriptive = """
        <table><tr class="line1"><td class="micro center screen-only"></td><td>Przedmiot</td><td>
          <table><tr><td class="grade-cell"><span class="grade-box"><a
            title="Kategoria: Test<br>Data: 2026-09-21<br>Nauczyciel: Nauczyciel<br>Waga: 1<br>Licz do średniej: tak"
            href="/grade/1">5</a></span></td></tr></table>
        </td><td></td></tr></table>
        """;

    public const string NewDescriptive = """
        <table><tr class="studentRow line1"><td class="micro center screen-only"></td><td>Przedmiot</td>
          <td class="gradesCell" data-semester="1"><span class="grade-box tooltip"
            title="Obszar: Komunikacja<br>Data: 2026-09-22 (wt.)<br>Nauczyciel: Nauczyciel<br>Ocena ze skali: 6<br>Treść oceny: <br>Dodał: Nauczyciel<br>Komentarz: Test<br/>">
            <span class="ocena">6</span></span></td><td class="gradesCell" data-semester="2"></td>
        </tr></table>
        """;

    public const string DescriptiveSummary = """
        <table><tr class="line0"><th><strong>Ocena opisowa</strong> (opublikowano: 2026-01-30 12:00, nauczyciel: Jan Kowalski)</th></tr>
        <tr class="line1"><td><p>Rozwój społeczny.</p><p>Komunikacja.</p></td></tr></table>
        """;

    public const string Attendance = """
        <table class="center big decorated">
        <tr class="line0"><td class="center bolded">Semestr II</td></tr>
        <tr class="line1"><td class="center"><a title="Data: 2026-05-10 (nd.)<br>Rodzaj: Nieobecność<br>Lekcja: Matematyka<br>Temat zajęć: Ułamki<br>Godzina lekcyjna: 2<br>Czy wycieczka: Nie<br>Nauczyciel: Anna" onclick="otworz_w_nowym_oknie('/przegladaj_nb/szczegoly/22', 1)">nb</a></td></tr>
        <tr class="line0"><td class="center bolded">Semestr I</td></tr>
        <tr class="line1"><td class="center"><a title="Data: 2026-01-10<br>Rodzaj: Wycieczka<br>Lekcja: Historia<br>Godzina lekcyjna: 3<br>Czy wycieczka: Tak<br>Nauczyciel: Jan" onclick="otworz_w_nowym_oknie('/przegladaj_nb/szczegoly/11', 1)">wy</a></td></tr>
        </table>
        """;

    public const string Announcements = """
        <table class="decorated big center printable margin-top"><thead><tr><td>Wycieczka</td></tr></thead>
        <tbody><tr class="line0"><td>Anna</td></tr><tr class="line1"><td>2026-10-09</td></tr><tr class="line0"><td>Spotkanie o 8:00.</td></tr></tbody></table>
        """;

    public const string Homework = """
        <table class="decorated myHomeworkTable"><tr class="line0">
        <td>Matematyka</td><td>Anna</td><td>Zadania 1–5</td><td>Praca domowa</td><td>2026-10-09</td><td>10:00</td><td>2026-10-12</td><td>08:00</td>
        <td><input onclick="otworz_w_nowym_oknie('/moje_zadania/podglad/77', 1)"></td></tr></table>
        """;

    public const string Received = """
        <table class="decorated stretch"><tbody><tr class="line0">
        <td></td><td><img src="attachment.png"></td><td><a href="/wiadomosci/1/5/42">Anna</a></td><td style="font-weight: bold">Spotkanie</td><td>2026-10-09</td><td></td>
        </tr><tr class="line1"><td></td><td></td><td><a href="/wiadomosci/1/5/41">Jan</a></td><td>Starsza wiadomość</td><td>2026-10-08</td><td></td></tr></tbody></table>
        """;

    public const string Sent = """
        <table class="decorated stretch"><tbody><tr class="line0">
        <td></td><td></td><td><a href="/wiadomosci/1/6/43">Anna</a></td><td>Odpowiedź</td><td>2026-10-09</td><td>NIE</td><td></td>
        </tr></tbody></table>
        """;

    public const string Content = """
        <table class="stretch"><tr><td class="left">Anna</td></tr><tr><td class="left">Spotkanie</td></tr><tr><td class="left">2026-10-09</td></tr></table>
        <div class="container-message-content">Dzień dobry! &amp; Do zobaczenia.</div>
        """;

    public const string Completed = """
        <table class="decorated"><tbody><tr><td class="center small">2026-10-09</td><td class="tiny">pt.</td>
        <td>2</td><td>Matematyka, Anna</td><td>Ułamki</td><td>Z</td><td><p class="box"><a onclick="otworz_w_nowym_oknie('/przegladaj_nb/szczegoly/44', 1)">ob</a></p></td>
        </tr></tbody></table>
        """;

    public const string Student = """
        <span class="luckyNumber"><b>17</b></span><table class="decorated big center"><tbody>
        <tr class="line0"><td>Jan Nowak</td></tr><tr class="line1"><td>1A</td></tr><tr class="line0"><td>7</td></tr>
        <tr class="line1"><td>Anna Kowalska</td></tr><tr class="line0"><td>Szkoła
          Warszawa</td></tr></tbody></table>
        """;

    public const string Schedule = """
        <div class="kalendarz-dzien"><div class="kalendarz-numer-dnia">9</div><table><tr><td
          title="Nauczyciel: Anna<br />Opis: Ułamki" onclick="otworz_w_nowym_oknie('/terminarz/sprawdzian/99', 1)"><span>Matematyka</span><br>Sprawdzian<br>Lekcja: 2</td></tr></table></div>
        <div class="kalendarz-dzien"><div class="kalendarz-numer-dnia">10</div></div>
        <div class="kalendarz-dzien"><div class="kalendarz-numer-dnia">11</div><table><tr><td>Spotkanie<br>Zebranie 18:30</td></tr></table></div>
        """;

    public const string Recent = """
        <div class="container-background"><table>
        <tr><td></td><td>czas dodania</td><td>rodzaj zdarzenia</td><td></td></tr>
        <tr><td>1</td><td>2026-10-09</td><td>Sprawdzian</td><td>Matematyka</td></tr></table></div>
        """;

    public const string Details = """
        <div class="container-background"><table><tr class="line0"><th>Nauczyciel</th><td>Anna</td></tr>
        <tr class="line1"><th>Opis</th><td>Ułamki</td></tr></table></div>
        """;

    public const string HomeworkDetails = """
        <div class="container-background"><table><tr class="line0"><td>Opis&nbsp;zadania</td><td>Ćwiczenia&nbsp;1–5</td></tr></table></div>
        """;

    public const string EmptyAnnouncements = "<div class='container border-red resizeable center'><div><p>Brak ogłoszeń</p></div></div>";
    public const string EmptyMessages = "<table class='decorated stretch'><tr class='line0'><td>Brak wiadomości</td></tr></table>";

    public static string Timetable(bool explicitBody = false)
    {
        var start = new DateOnly(2026, 10, 5);
        var cells = string.Join("", Enumerable.Range(0, 7).Select(i => $"""
            <td id="timetableEntryBox" class="line1" data-date="{start.AddDays(i):yyyy-MM-dd}" data-from="08:00" data-to="08:45">
            <div class="text"><b>Matematyka</b> - Anna, sala 1</div><div class="text"><b>Historia</b> - Jan, sala 2</div>
            <div class="center plan-lekcji-info">Zastępstwo</div><a title="&lt;b&gt;Nauczyciel&lt;/b&gt;: Maria&lt;br&gt;Przedmiot: Chemia&lt;br&gt;Sala: 3&lt;br&gt;Data dodania: 2026-10-04"></a></td>
            """));
        var rows = $"<tr class='line1'><td class='center'>1</td>{cells}</tr><tr class='line0'><td class='center'>08:45 - 08:55</td></tr>";
        return "<table class='decorated plan-lekcji'>" + (explicitBody ? "<tbody>" + rows + "</tbody>" : rows) + "</table>";
    }
}
