using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UWPTest;

public sealed partial class MainPage : Page
{
	public MainPage()
	{
		InitializeComponent();
		// 若上次被 PLM 终止，App.OnLaunched 已从 LocalSettings 还原计数器。
		CounterButton.Content = $"点击计数: {App.Counter}";
		EnvInfo.Text = $"""
            系统版本：{SystemInfo.OsVersion}（{SystemInfo.DeviceFamily}）
            OS 架构：{SystemInfo.OsArchitecture}　进程架构：{SystemInfo.ProcessArchitecture}
            .NET 运行时：{SystemInfo.DotNetRuntime}
            激活入口：{SystemInfo.EntryMode}
            窗口模型（≈RuntimeBehavior）：{SystemInfo.WindowingModel}
            生命周期策略（AppLifecycleBehavior）：{SystemInfo.LifecycleManagement}
            信任等级（TrustLevel）：{SystemInfo.TrustLevel}
            包身份：{SystemInfo.PackageIdentity}
            """;
	}

	private void CounterButton_Click(object sender, RoutedEventArgs e)
	{
		App.Counter++;
		CounterButton.Content = $"点击计数: {App.Counter}";
	}
}