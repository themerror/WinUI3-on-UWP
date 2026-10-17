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
    }

    private void CounterButton_Click(object sender, RoutedEventArgs e)
    {
        App.Counter++;
        CounterButton.Content = $"点击计数: {App.Counter}";
    }
}
