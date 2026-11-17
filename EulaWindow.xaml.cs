using System.Windows;
using CS2Box.Services;

namespace CS2Box;

/// <summary>启动时的免责声明窗口。readOnly = 从设置里重新查看，不给同意按钮。</summary>
public partial class EulaWindow : Wpf.Ui.Controls.FluentWindow
{
    readonly bool _readOnly;

    public EulaWindow(bool readOnly = false)
    {
        InitializeComponent();
        _readOnly = readOnly;
        TermsText.Text = Eula.Text;

        if (readOnly)
        {
            Title = "免责声明";
            AgreeBox.Visibility = Visibility.Collapsed;
            DeclineBtn.Content = "关闭";
            AcceptBtn.Visibility = Visibility.Collapsed;
        }
    }

    void Agree_Changed(object sender, RoutedEventArgs e) =>
        AcceptBtn.IsEnabled = AgreeBox.IsChecked == true;

    void Accept_Click(object sender, RoutedEventArgs e)
    {
        if (_readOnly) { Close(); return; }
        if (AgreeBox.IsChecked != true) return;
        DialogResult = true;
        Close();
    }

    void Decline_Click(object sender, RoutedEventArgs e)
    {
        if (_readOnly) { Close(); return; }
        DialogResult = false;
        Close();
    }
}
