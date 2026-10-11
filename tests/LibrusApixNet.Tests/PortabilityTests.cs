using System.Globalization;

namespace LibrusApixNet.Tests;

public sealed class PortabilityTests
{
    [Theory]
    [InlineData("pl-PL")]
    [InlineData("en-US")]
    public async Task GradesAndDatePayloadsDoNotDependOnSystemCulture(string cultureName)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);

            var grades = Grades.Parse(Fixtures.NumericGrades);
            Assert.Equal(5.5, grades.Semesters[0]["Matematyka"][0].Value);
            Assert.Equal(3.75, Assert.Single(grades.Semesters[1]["Matematyka"]).Value);
            Assert.Equal(5.5, grades.Averages["Matematyka"][0].NumericValue);
            Assert.Equal(4.5, new Grade("Math", "4,5", true, "", "", "", 1, "", "", 1).Value);
            Assert.Equal(85, new Grade("Math", "85", true, "", "", "", 1, "", "", 1).Value);
            Assert.Null(new Grade("Math", "85%", true, "", "", "", 1, "", "", 1).Value);
            Assert.Equal("2026-09-22", Assert.Single(Grades.Parse(Fixtures.NewDescriptive).Descriptive[0]["Przedmiot"]).Date);

            var handler = new MockHttp(request => MockHttp.Response(request.Uri.AbsolutePath switch
            {
                "/przegladaj_plan_lekcji" => Fixtures.Timetable(),
                "/terminarz/" => Fixtures.Schedule,
                "/moje_zadania" => Fixtures.Homework,
                "/zrealizowane_lekcje" => Fixtures.Completed,
                _ => throw new InvalidOperationException("Unexpected endpoint in portability test.")
            }));
            using var client = new Client(new Token("a:b"), MockHttp.Options, handler);
            var week = await Timetable.GetTimetableAsync(client, new DateOnly(2026, 10, 5));
            Assert.Equal("Monday", week[0][0].Weekday);
            Assert.Equal("2026-10-05", week[0][0].Date);
            await Schedule.GetScheduleAsync(client, 10, 2026);
            await HomeworkService.GetHomeworkAsync(client, "2026-10-01", "2026-10-09");
            await CompletedLessons.GetCompletedAsync(client, "2026-10-01", "2026-10-09", 2);

            var bodies = handler.Requests.Select(request => request.Body).ToArray();
            Assert.Equal("tydzien=2026-10-05_2026-10-11", bodies[0]);
            Assert.Equal("rok=2026&miesiac=10", bodies[1]);
            Assert.Contains("dataOd=2026-10-01&dataDo=2026-10-09", bodies[2]);
            Assert.Contains("data1=2026-10-01&data2=2026-10-09", bodies[3]);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }
}
