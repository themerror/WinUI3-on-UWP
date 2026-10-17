using System.Diagnostics;
using System.Security;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.UI.Notifications;
using LaunchActivatedEventArgs = Microsoft.UI.Xaml.LaunchActivatedEventArgs;

namespace UWPTest;

public partial class App : Application
{
    private const string CounterSettingKey = "Counter";

    /// <summary>跨挂起/终止需要保存的演示状态（MainPage 的计数器）。</summary>
    public static int Counter { get; set; }

    public App()
    {
        InitializeComponent();

        // WinUI 3 的 Application 不提供 UWP 生命周期事件，
        // 标准 UWP 做法是直接订阅 Windows.ApplicationModel.Core.CoreApplication。
        // 仅在 UWP（CoreWindow）模式下订阅——Win32 入口不受 PLM 托管，事件不会触发。
        if (Program.IsCoreWindow)
        {
            CoreApplication.Suspending += OnSuspending;
            CoreApplication.Resuming += OnResuming;
            CoreApplication.EnteredBackground += OnEnteredBackground;
            CoreApplication.LeavingBackground += OnLeavingBackground;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (Program.IsCoreWindow)
        {
            // UWPLaunchActivatedEventArgs 是 WASDK 暴露的 UWP 激活参数桥接。
            ApplicationExecutionState? previousState = args.UWPLaunchActivatedEventArgs?.PreviousExecutionState;

            // 标准 UWP 还原路径：上次被 PLM 终止 → 从 LocalSettings 还原状态。
            if (previousState == ApplicationExecutionState.Terminated
                && ApplicationData.Current.LocalSettings.Values.TryGetValue(CounterSettingKey, out object? saved)
                && saved is int counter)
            {
                Counter = counter;
                ShowToast($"启动：上次被系统终止（Terminated），已还原计数器 = {counter}");
            }
            else
            {
                ShowToast($"启动：PreviousExecutionState = {previousState}");
            }
        }

        EnsureWindow(args);
    }

    private void EnsureWindow(LaunchActivatedEventArgs e)
    {
        // UWP（CoreWindow）模式下窗口由系统创建，必须使用 Window.Current；
        // 只有 Win32 模式才允许 new Window()。
        Window? window = Window.Current;
        if (window is null)
        {
            window = new Window { Title = "UWPTest" };
        }

        if (window.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            window.Content = rootFrame;
        }

        if (rootFrame.Content is null)
        {
            rootFrame.Navigate(typeof(MainPage), e);
        }

        window.Activate();
    }

    #region PLM 生命周期（标准 UWP 形式）

    private void OnSuspending(object? sender, Windows.ApplicationModel.SuspendingEventArgs e)
    {
        // 挂起处理上限约 5 秒；异步操作必须拿 deferral。
        Windows.ApplicationModel.SuspendingDeferral deferral = e.SuspendingOperation.GetDeferral();
        try
        {
            // 这是进程被系统终止前唯一的通知：在这里保存状态。
            ApplicationData.Current.LocalSettings.Values[CounterSettingKey] = Counter;
            ShowToast($"Suspending：状态已保存（计数器 = {Counter}）");
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void OnResuming(object? sender, object e)
    {
        // 注意：Resuming 不在 UI 线程上触发；恢复后内存状态未丢失，一般只需刷新数据。
        ShowToast("Resuming：已从挂起恢复");
    }

    private void OnEnteredBackground(object? sender, Windows.ApplicationModel.EnteredBackgroundEventArgs e)
    {
        ShowToast("EnteredBackground：进入后台");
    }

    private void OnLeavingBackground(object? sender, Windows.ApplicationModel.LeavingBackgroundEventArgs e)
    {
        ShowToast("LeavingBackground：回到前台");
    }

    #endregion

    private static void ShowToast(string message)
    {
        try
        {
            XmlDocument doc = new();
            doc.LoadXml($"""
                <toast>
                  <visual>
                    <binding template="ToastGeneric">
                      <text>UWPTest 生命周期</text>
                      <text>{SecurityElement.Escape(message)}</text>
                    </binding>
                  </visual>
                </toast>
                """);
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(doc));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Toast failed: {ex.Message}");
        }
    }
}
