using System.Windows;

namespace MusicPlayer.Views;

public partial class LoadingDialog : Window
{
    public LoadingDialog(string title = "加载中")
    {
        InitializeComponent();
        TitleText.Text = title;
    }

    public void UpdateProgress(int current, int total)
    {
        Dispatcher.Invoke(() =>
        {
            ProgressBar.Maximum = total;
            ProgressBar.Value = current;
            StatusText.Text = $"{current} / {total}";
        });
    }

    public void UpdateProgress(int percent)
    {
        Dispatcher.Invoke(() =>
        {
            ProgressBar.Value = percent;
            StatusText.Text = $"{percent}%";
        });
    }
}
