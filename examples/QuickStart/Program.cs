using LibrusApixNet;

if (args.Contains("--help"))
{
    Console.WriteLine("Ustaw LIBRUS_API_KEY albo LIBRUS_USERNAME i LIBRUS_PASSWORD, następnie uruchom przykład.");
    Console.WriteLine("Przykład pobiera informacje o uczniu, oceny i ogłoszenia.");
    return;
}

var apiKey = Environment.GetEnvironmentVariable("LIBRUS_API_KEY");
var username = Environment.GetEnvironmentVariable("LIBRUS_USERNAME");
var password = Environment.GetEnvironmentVariable("LIBRUS_PASSWORD");
if (string.IsNullOrEmpty(apiKey) && (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)))
{
    Console.Error.WriteLine("Brak konfiguracji. Uruchom z --help.");
    Environment.ExitCode = 1;
    return;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
using var client = new Client(new Token(apiKey));
try
{
    if (string.IsNullOrEmpty(apiKey)) await client.GetTokenAsync(username!, password!, cancellation.Token);
    var student = await StudentInformationService.GetStudentInformationAsync(client, cancellation.Token);
    Console.WriteLine($"{student.Name}, klasa {student.ClassName}");
    var grades = await Grades.GetGradesAsync(client, cancellationToken: cancellation.Token);
    foreach (var semester in grades.Semesters)
        foreach (var (subject, marks) in semester)
            Console.WriteLine($"{subject}: {string.Join(", ", marks.Select(g => g.Mark))}");
    var announcements = await Announcements.GetAnnouncementsAsync(client, cancellation.Token);
    foreach (var announcement in announcements) Console.WriteLine($"{announcement.Date}: {announcement.Title}");
}
catch (OperationCanceledException) { Console.Error.WriteLine("Anulowano."); Environment.ExitCode = 1; }
catch (LibrusException e) { Console.Error.WriteLine(e.Message); Environment.ExitCode = 1; }
catch (HttpRequestException e) { Console.Error.WriteLine(e.Message); Environment.ExitCode = 1; }
