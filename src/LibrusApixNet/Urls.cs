using System.Net;

namespace LibrusApixNet;

/// <summary>Endpoint overrides corresponding to librus_apix/urls.py and Client constructor arguments.</summary>
public sealed record ClientOptions
{
    public string BaseUrl { get; init; } = "https://synergia.librus.pl";
    public string ApiUrl { get; init; } = "https://api.librus.pl";
    public string IndexUrl { get => field ?? BaseUrl + "/uczen/index"; init; }
    public string GradesUrl { get => field ?? BaseUrl + "/przegladaj_oceny/uczen"; init; }
    public string TimetableUrl { get => field ?? BaseUrl + "/przegladaj_plan_lekcji"; init; }
    public string AnnouncementsUrl { get => field ?? BaseUrl + "/ogloszenia"; init; }
    public string MessageUrl { get => field ?? BaseUrl + "/wiadomosci/1/5"; init; }
    public string RecipientGroupsUrl { get => field ?? BaseUrl + "/wiadomosci/2/6"; init; }
    public string RecipientsUrl { get => field ?? BaseUrl + "/getRecipients"; init; }
    public string SendMessageUrl { get => field ?? BaseUrl + "/wiadomosci/1/6"; init; }
    public string AttendanceUrl { get => field ?? BaseUrl + "/przegladaj_nb/uczen"; init; }
    public string AttendanceDetailsUrl { get => field ?? BaseUrl + "/przegladaj_nb/szczegoly/"; init; }
    public string ScheduleUrl { get => field ?? BaseUrl + "/terminarz/"; init; }
    public string RecentScheduleUrl { get => field ?? BaseUrl + "/terminarz/dodane_od_ostatniego_logowania"; init; }
    public string HomeworkUrl { get => field ?? BaseUrl + "/moje_zadania"; init; }
    public string HomeworkDetailsUrl { get => field ?? BaseUrl + "/moje_zadania/podglad/"; init; }
    public string InfoUrl { get => field ?? BaseUrl + "/informacja"; init; }
    public string CompletedLessonsUrl { get => field ?? BaseUrl + "/zrealizowane_lekcje"; init; }
    public string GatewayApiAttendanceUrl { get => field ?? BaseUrl + "/gateway/api/2.0/Attendances"; init; }
    public string GatewayApiTokenInfoUrl { get => field ?? BaseUrl + "/gateway/api/2.0/Auth/TokenInfo"; init; }
    public string GatewayApiUserInfoUrl { get => field ?? BaseUrl + "/gateway/api/2.0/Auth/UserInfo/"; init; }
    public string GatewayApiUsersUrl { get => field ?? BaseUrl + "/gateway/api/2.0/Users"; init; }
    public string GatewayApiSubjectsUrl { get => field ?? BaseUrl + "/gateway/api/3.0/Auth/Subjects"; init; }
    public string GatewayApiDescriptiveGradesUrl { get => field ?? BaseUrl + "/gateway/api/2.0/Auth/DescriptiveGradingSystem/PartialGrades/Student/"; init; }
    public string RefreshOAuthUrl { get => field ?? BaseUrl + "/refreshToken"; init; }
    public string LoginPortalUrl { get => field ?? BaseUrl + "/loguj/portalRodzina?v=1774820765"; init; }
    public string AuthorizationUrl { get => field ?? ApiUrl + "/OAuth/Authorization?client_id=46"; init; }
    public string TwoFactorUrl { get => field ?? ApiUrl + "/OAuth/Authorization/2FA?client_id=46"; init; }
    public string UserAgent { get; init; } = "Mozilla/5.0 (Windows NT x.y; Win64; x64; rv:10.0) Gecko/20100101 Firefox/10.0";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(100);
    public IWebProxy? Proxy { get; init; }
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}
