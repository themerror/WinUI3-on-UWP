using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Win32;

namespace UWPTest;

public static class Program
{
	private const string XamlRegistryKeyPath = @"SOFTWARE\Microsoft\WinUI\Xaml";
	private const string XamlRegistryValueName = "EnableUWPWindow";

    /// <summary>
    /// 当前进程是否运行在 UWP（CoreWindow）容器内。
    /// 通过 appmodel 策略 API 查询窗口模型：Universal = UWP，ClassicDesktop = Win32。
    /// </summary>
    public static bool IsCoreWindow { get; } = WindowingModel == AppPolicyWindowingModelUniversal;

    /// <summary>窗口模型策略原始值：0=None，1=Universal，2=ClassicDesktop，3=ClassicPhone。</summary>
    public static int WindowingModel { get; } = QueryWindowingModel();

    /// <summary>生命周期管理策略原始值：0=Unmanaged，1=Managed（受 PLM 托管）。</summary>
    public static int LifecycleManagement { get; } = QueryLifecycleManagement();

	private static bool IsUwpWindowEnabled
	{
		get
		{
			try
			{
				using RegistryKey? key = Registry.LocalMachine.OpenSubKey(XamlRegistryKeyPath);
				return key?.GetValue(XamlRegistryValueName) is int value && value > 0;
			}
			catch
			{
				return false;
			}
		}
	}

	// UWP 容器要求 MTA 入口；自动生成的 STAThread 入口在 UWP 中不可用，
	// 因此 csproj 中定义了 DISABLE_XAML_GENERATED_MAIN 并在此处手写入口。
	[MTAThread]
	private static void Main(string[] args)
	{
		// EnableUWPWindow 开关只门控 CoreWindow 路径；Win32/PartialTrust 入口不需要它。
		if (IsCoreWindow && !IsUwpWindowEnabled)
		{
			throw new InvalidOperationException(
				"WinUI 3 的 UWP 支持未启用：请以管理员身份导入仓库根目录的 Enable-UWPWindow.reg，" +
				"将 HKLM\\SOFTWARE\\Microsoft\\WinUI\\Xaml\\EnableUWPWindow 设为 1 后重试。");
		}

		WinRT.ComWrappersSupport.InitializeComWrappers();
		XamlCheckProcessRequirements();

		Application.Start(p =>
		{
			var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
			SynchronizationContext.SetSynchronizationContext(context);
			_ = new App();
		});
	}

	[DllImport("Microsoft.ui.xaml.dll")]
	private static extern void XamlCheckProcessRequirements();

	// AppPolicyWindowingModel_Universal = 1，ClassicDesktop = 2
	private const int AppPolicyWindowingModelUniversal = 1;

	private const int TokenQuery = 0x0008;

    private static int QueryWindowingModel()
    {
        IntPtr token = IntPtr.Zero;
        try
        {
            if (!OpenProcessToken(GetCurrentProcess(), TokenQuery, out token))
            {
                return -1;
            }
            return AppPolicyGetWindowingModel(token, out int model) == 0 ? model : -1;
        }
        catch
        {
            return -1;
        }
        finally
        {
            if (token != IntPtr.Zero)
            {
                CloseHandle(token);
            }
        }
    }

    private static int QueryLifecycleManagement()
    {
        IntPtr token = IntPtr.Zero;
        try
        {
            if (!OpenProcessToken(GetCurrentProcess(), TokenQuery, out token))
            {
                return -1;
            }
            return AppPolicyGetLifecycleManagement(token, out int policy) == 0 ? policy : -1;
        }
        catch
        {
            return -1;
        }
        finally
        {
            if (token != IntPtr.Zero)
            {
                CloseHandle(token);
            }
        }
    }

	[DllImport("kernel32.dll")]
	private static extern IntPtr GetCurrentProcess();

	[DllImport("advapi32.dll", SetLastError = true)]
	private static extern bool OpenProcessToken(IntPtr processHandle, int desiredAccess, out IntPtr tokenHandle);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool CloseHandle(IntPtr handle);

    [DllImport("api-ms-win-appmodel-runtime-l1-1-2.dll")]
    private static extern int AppPolicyGetWindowingModel(IntPtr processToken, out int policy);

    [DllImport("api-ms-win-appmodel-runtime-l1-1-2.dll")]
    private static extern int AppPolicyGetLifecycleManagement(IntPtr processToken, out int policy);
}