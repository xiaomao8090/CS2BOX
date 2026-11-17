using Wpf.Ui.Controls;

namespace CS2Box.Services;

/// <summary>右下角通知，用 WPF-UI 的 Snackbar。</summary>
public static class Toast
{
    public static SnackbarPresenter? Presenter { get; set; }

    public static void Ok(string msg) => Show("完成", msg, ControlAppearance.Success, SymbolRegular.CheckmarkCircle24);
    public static void Info(string msg) => Show("提示", msg, ControlAppearance.Secondary, SymbolRegular.Info24);
    public static void Error(string msg) => Show("出错了", msg, ControlAppearance.Danger, SymbolRegular.ErrorCircle24);

    static void Show(string title, string msg, ControlAppearance appearance, SymbolRegular icon)
    {
        if (Presenter == null) return;
        new Snackbar(Presenter)
        {
            Title = title,
            Content = msg,
            Appearance = appearance,
            Icon = new SymbolIcon(icon),
            Timeout = TimeSpan.FromSeconds(3),
        }.Show();
    }

    /// <summary>执行一个操作，出异常时弹错误通知。</summary>
    public static async Task Try(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { Error(ex.Message); }
    }

    public static void Try(Action action)
    {
        try { action(); }
        catch (Exception ex) { Error(ex.Message); }
    }
}
