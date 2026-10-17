using System.Runtime.InteropServices;
using System.Text;
using Windows.ApplicationModel;
using Windows.System.Profile;

namespace UWPTest;

/// <summary>
/// 动态采集的运行环境信息：系统版本、设备族、架构、.NET 运行时、
/// 激活入口、窗口模型（≈RuntimeBehavior）、PLM 策略（AppLifecycleBehavior）、
/// 信任等级（TrustLevel）、包身份。全部在进程内实时查询，不写死。
/// </summary>
public static class SystemInfo
{
    public static string OsVersion { get; }
    public static string DeviceFamily { get; }
    public static string OsArchitecture { get; } = RuntimeInformation.OSArchitecture.ToString();
    public static string ProcessArchitecture { get; } = RuntimeInformation.ProcessArchitecture.ToString();
    public static string DotNetRuntime { get; } = RuntimeInformation.FrameworkDescription;
    public static string EntryMode { get; }
    public static string WindowingModel { get; }
    public static string LifecycleManagement { get; }
    public static string TrustLevel { get; }
    public static string PackageIdentity { get; }

    static SystemInfo()
    {
        (OsVersion, DeviceFamily) = QueryOsVersion();

        WindowingModel = Program.WindowingModel switch
        {
            0 => "None",
            1 => "Universal（UWP / CoreWindow）",
            2 => "ClassicDesktop（Win32 / HWND）",
            3 => "ClassicPhone",
            -1 => "查询失败",
            _ => $"未知({Program.WindowingModel})"
        };

        LifecycleManagement = Program.LifecycleManagement switch
        {
            0 => "Unmanaged（不受 PLM 托管）",
            1 => "Managed（受 PLM 托管）",
            -1 => "查询失败",
            _ => $"未知({Program.LifecycleManagement})"
        };

        TrustLevel = QueryIsAppContainer()
            ? "appContainer（AppContainer 沙箱，低 IL）"
            : "mediumIL（完全信任）";

        EntryMode = Program.IsCoreWindow
            ? "UWP 入口（windowsApp，CoreWindow 激活）"
            : "Win32 入口（packagedClassicApp，经典激活）";

        PackageIdentity = QueryPackageIdentity();
    }

    // AnalyticsInfo.DeviceFamilyVersion 是编码后的 ulong：major/minor/build/revision 各占 16 位
    private static (string OsVersion, string DeviceFamily) QueryOsVersion()
    {
        try
        {
            string family = AnalyticsInfo.VersionInfo.DeviceFamily;
            if (ulong.TryParse(AnalyticsInfo.VersionInfo.DeviceFamilyVersion, out ulong v))
            {
                return ($"{v >> 48}.{(v >> 32) & 0xFFFF}.{(v >> 16) & 0xFFFF}.{v & 0xFFFF}", family);
            }
            return ("解析失败", family);
        }
        catch
        {
            return (Environment.OSVersion.VersionString, "未知");
        }
    }

    private const int TokenIsAppContainer = 29;
    private const int TokenQuery = 0x0008;

    private static bool QueryIsAppContainer()
    {
        IntPtr token = IntPtr.Zero;
        try
        {
            if (!OpenProcessToken(GetCurrentProcess(), TokenQuery, out token))
            {
                return false;
            }
            IntPtr buffer = Marshal.AllocHGlobal(sizeof(uint));
            try
            {
                return GetTokenInformation(token, TokenIsAppContainer, buffer, sizeof(uint), out _)
                    && Marshal.ReadInt32(buffer) != 0;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            return false;
        }
        finally
        {
            if (token != IntPtr.Zero)
            {
                CloseHandle(token);
            }
        }
    }

    private const int AppModelErrorNoPackage = 15700;   // 0x3D54
    private const int ErrorInsufficientBuffer = 122;    // 0x7A

    private static string QueryPackageIdentity()
    {
        uint length = 0;
        int rc = GetCurrentPackageFullName(ref length, null);
        if (rc == AppModelErrorNoPackage)
        {
            return "无（非打包进程）";
        }
        if (rc != ErrorInsufficientBuffer)
        {
            return $"查询失败(0x{rc:X})";
        }

        var sb = new StringBuilder((int)length);
        if (GetCurrentPackageFullName(ref length, sb) != 0)
        {
            return "查询失败";
        }

        try
        {
            PackageId id = Package.Current.Id;
            PackageVersion v = id.Version;
            return $"{id.FamilyName}\n{id.Name} {v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
        }
        catch
        {
            return sb.ToString();
        }
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, int desiredAccess, out IntPtr tokenHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr tokenHandle, int tokenInformationClass, IntPtr tokenInformation, uint tokenInformationLength, out uint returnLength);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, StringBuilder? packageFullName);
}
