using System.Reflection;

namespace Vihor
{
    internal static class AppInfo
    {
        public const string Name = "Vihor IP Scanner";
        public const string Website = "https://cyber-security.ba";
        public const string Repository = "https://github.com/freedole/vihor-ip-scanner";
        public const string Author = "Đorđe Kaurinović";

        public static string Version
        {
            get
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return $"{v.Major}.{v.Minor}.{v.Build}";
            }
        }
    }
}
