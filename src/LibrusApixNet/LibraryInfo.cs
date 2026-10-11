using System.Reflection;

namespace LibrusApixNet;

public static class LibraryInfo
{
    public static string GetVersion() => typeof(Client).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
    public static string Version => GetVersion();
}
