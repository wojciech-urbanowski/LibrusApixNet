namespace LibrusApixNet.Tests;

public sealed class ParserTests
{
    [Fact]
    public void NumericGradesIncludeImprovedGradesAveragesAndSemesters()
    {
        var result = Grades.Parse(Fixtures.NumericGrades);
        var grades = result.Semesters[0]["Matematyka"];
        Assert.Equal(2, grades.Count);
        Assert.Equal(5.5, grades[0].Value);
        Assert.Null(grades[1].Value);
        Assert.Equal(2, grades[0].Weight);
        Assert.Equal("Anna", grades[0].Teacher);
        Assert.Equal("2026-09-21", grades[0].Date);
        Assert.Equal("/grade/1", grades[0].Href);
        Assert.Contains("Przedmiot: Matematyka", grades[0].Description);
        Assert.Equal(3.75, Assert.Single(result.Semesters[1]["Matematyka"]).Value);
        Assert.Equal(new[] { 1, 2, 0 }, result.Averages["Matematyka"].Select(g => g.Semester));
        Assert.Equal(5.5, result.Averages["Matematyka"][0].NumericValue);
    }

    [Theory]
    [InlineData(false, "5", "2026-09-21", "/grade/1")]
    [InlineData(true, "6", "2026-09-22", "")]
    public void DescriptiveGradesSupportBothUpstreamSchemas(bool modern, string mark, string date, string href)
    {
        var result = Grades.Parse(modern ? Fixtures.NewDescriptive : Fixtures.OldDescriptive);
        var grade = Assert.Single(result.Descriptive[0]["Przedmiot"]);
        Assert.Equal(mark, grade.Mark); Assert.Equal(date, grade.Date); Assert.Equal(href, grade.Href);
        Assert.Equal("Nauczyciel", grade.Teacher); Assert.Equal(1, grade.Semester);
        Assert.Empty(result.Descriptive[1]["Przedmiot"]);
    }

    [Fact]
    public void DescriptiveSemesterSummaryIsParsed()
    {
        var grade = Assert.Single(Grades.Parse(Fixtures.DescriptiveSummary).Descriptive[0]["Ocena opisowa"]);
        Assert.Equal("2026-01-30", grade.Date); Assert.Equal("Jan Kowalski", grade.Teacher);
        Assert.Equal("Rozwój społeczny.\nKomunikacja.", grade.Description);
    }

    [Theory]
    [InlineData("1", 1)] [InlineData("6+", 6.5)] [InlineData("2-", 1.75)]
    public void GradeValueUsesSourceModifiers(string mark, double expected) => Assert.Equal(expected, NewGrade(mark).Value);

    [Theory]
    [InlineData("85", 85)] [InlineData("100", 100)] [InlineData("4,5", 4.5)] [InlineData("4.5", 4.5)]
    public void GradeValueSupportsUpstream16NumericFormats(string mark, double expected) => Assert.Equal(expected, NewGrade(mark).Value);

    [Theory]
    [InlineData("A+")] [InlineData("85%")] [InlineData("85/100")] [InlineData("+")] [InlineData("np")]
    public void NonNumericCountingGradesPreserveTheirText(string mark)
    {
        var grade = NewGrade(mark);
        Assert.Null(grade.Value);
        Assert.Equal(mark, grade.Mark);
    }

    private static Grade NewGrade(string mark) => new("Math", mark, true, "", "", "", 1, "", "", 1);

    [Fact]
    public void AttendanceKeepsSourceSemesterIndicesAndChronologicalOutput()
    {
        var semesters = Attendances.Parse(Fixtures.Attendance);
        var first = Assert.Single(semesters[0]); var second = Assert.Single(semesters[1]);
        Assert.Equal("11", first.Href); Assert.Equal(1, first.Semester); Assert.True(first.Excursion);
        Assert.Equal("22", second.Href); Assert.Equal(0, second.Semester); Assert.Equal(2, second.Period);
        Assert.Equal("2026-05-10", second.Date); Assert.Equal("Ułamki", second.Topic); Assert.Equal("Matematyka", second.Subject);
    }

    [Fact]
    public void AnnouncementsAndDetailsAreParsed()
    {
        Assert.Equal(new Announcement("Wycieczka", "Anna", "Spotkanie o 8:00.", "2026-10-09"), Assert.Single(Announcements.Parse(Fixtures.Announcements)));
        Assert.Empty(Announcements.Parse(Fixtures.EmptyAnnouncements));
        Assert.Equal("Anna", Attendances.ParseDetail(Fixtures.Details)["Nauczyciel"]);
        Assert.Equal("Ułamki", Schedule.ParseDetail(Fixtures.Details)["Opis"]);
    }

    [Fact]
    public void HomeworkParsesDatesLinksAndNonbreakingSpaces()
    {
        var homework = Assert.Single(HomeworkService.Parse(Fixtures.Homework));
        Assert.Equal("77", homework.Href); Assert.Equal("2026-10-09 10:00", homework.TaskDate); Assert.Equal("2026-10-12 08:00", homework.CompletionDate);
        Assert.Equal("Ćwiczenia 1–5", HomeworkService.ParseDetail(Fixtures.HomeworkDetails)["Opis zadania"]);
        Assert.Empty(HomeworkService.Parse("<p class='msgEmptyTable'>Brak zadań</p>"));
    }

    [Fact]
    public void MessagesHaveReadStatusAttachmentsAndContent()
    {
        var received = Messages.Parse(Fixtures.Received);
        Assert.True(received[0].Unread); Assert.True(received[0].HasAttachment); Assert.Equal("42", received[0].Href);
        Assert.False(received[1].Unread); Assert.False(received[1].HasAttachment);
        Assert.True(Assert.Single(Messages.ParseSent(Fixtures.Sent)).Unread);
        Assert.Empty(Messages.Parse(Fixtures.EmptyMessages));
        Assert.Equal(new MessageData("Anna", "Spotkanie", "Dzień dobry! & Do zobaczenia.", "2026-10-09"), Messages.ParseContent(Fixtures.Content));
    }

    [Fact]
    public void CompletedLessonParsesTeacherAndAttendance()
    {
        var lesson = Assert.Single(CompletedLessons.Parse(Fixtures.Completed));
        Assert.Equal("Matematyka", lesson.Subject); Assert.Equal("Anna", lesson.Teacher); Assert.Equal(2, lesson.LessonNumber);
        Assert.Equal("44", lesson.AttendanceHref); Assert.Equal("ob", lesson.AttendanceSymbol);
    }

    [Fact]
    public void StudentInformationKeepsMultilineSchoolAndOptionalLuckyNumber()
    {
        var student = StudentInformationService.Parse(Fixtures.Student);
        Assert.Equal("Jan Nowak", student.Name); Assert.Equal("1A", student.ClassName); Assert.Equal(7, student.Number);
        Assert.Equal("Szkoła\nWarszawa", student.School); Assert.Equal(17, student.LuckyNumber);
        Assert.Null(StudentInformationService.Parse(Fixtures.Student.Replace("<b>17</b>", "<b>?</b>")).LuckyNumber);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void TimetableHandlesImplicitAndExplicitBodyMultipleSubjectsAndRecess(bool explicitBody)
    {
        var days = Timetable.Parse(Fixtures.Timetable(explicitBody));
        Assert.Equal(7, days.Count);
        Assert.All(days, day => Assert.Single(day));
        var first = days[0][0];
        Assert.Equal("Monday", first.Weekday); Assert.Equal("Sunday", days[6][0].Weekday);
        Assert.Equal("Matematyka / Historia", first.Subject); Assert.Equal("Anna, sala 1 / Jan, sala 2", first.TeacherAndClassroom);
        Assert.Equal("08:45", first.NextRecessFrom); Assert.Equal("08:55", first.NextRecessTo);
        Assert.Equal(new PeriodChange("Maria", "Chemia", "3", "2026-10-04"), first.Info["Zastępstwo"]);
    }

    [Fact]
    public void ScheduleDistinguishesLessonNumbersAndClockTime()
    {
        var schedule = Schedule.Parse(Fixtures.Schedule);
        Assert.False(schedule.ContainsKey(10)); Assert.True(Schedule.Parse(Fixtures.Schedule, true).ContainsKey(10));
        var test = Assert.Single(schedule[9]);
        Assert.Equal("Sprawdzian", test.Title); Assert.Equal("Matematyka", test.Subject); Assert.Equal(2, test.Number);
        Assert.Equal("sprawdzian/99", test.Href); Assert.Equal("Anna", test.Data["Nauczyciel"]);
        var meeting = Assert.Single(schedule[11]);
        Assert.Equal("18:30", meeting.Hour); Assert.Null(meeting.Number);
        Assert.Single(Schedule.ParseRecentlyAdded(Fixtures.Recent));
        Assert.Empty(Schedule.ParseRecentlyAdded("<div class='container-background'></div>"));
    }

    [Fact]
    public void NoAccessIsConsistentlyReportedAcrossAllParsers()
    {
        const string denied = "<h2 class='inside'>Brak dostępu</h2>";
        Action[] parse = [() => Grades.Parse(denied), () => Attendances.Parse(denied), () => Announcements.Parse(denied),
            () => HomeworkService.Parse(denied), () => Messages.Parse(denied), () => CompletedLessons.Parse(denied),
            () => StudentInformationService.Parse(denied), () => Timetable.Parse(denied), () => Schedule.Parse(denied), () => Notifications.ParseAmounts(denied)];
        foreach (var action in parse) Assert.Throws<TokenException>(action);
    }

    [Fact]
    public void MissingAndMalformedRequiredContentThrowsParseException()
    {
        Action[] parse = [() => Grades.Parse(""), () => Attendances.Parse(""), () => Announcements.Parse(""), () => HomeworkService.Parse(""),
            () => Messages.Parse(""), () => Messages.ParseContent(""), () => StudentInformationService.Parse(""), () => Timetable.Parse(""), () => Schedule.Parse("")];
        foreach (var action in parse) Assert.Throws<ParseException>(action);
        Assert.Throws<ParseException>(() => Grades.Parse(Fixtures.NewDescriptive.Replace("Data: 2026-09-22", "Data: invalid")));
        Assert.Throws<ParseException>(() => Timetable.Parse(Fixtures.Timetable().Replace("08:45 - 08:55", "invalid")));
    }
}
