using Forms = System.Windows.Forms;

namespace CS2Box.Services;

/// <summary>系统托盘图标。</summary>
public sealed class Tray : IDisposable
{
    readonly Forms.NotifyIcon _icon;
    bool _tipShown;

    public Tray(Action show, Action launch, Action exit)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开 CS2 盒子", null, (_, _) => show());
        menu.Items.Add("启动 CS2", null, (_, _) => launch());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => exit());

        _icon = new Forms.NotifyIcon
        {
            Text = "CS2 盒子",
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) show(); };
    }

    /// <summary>第一次缩到托盘时提示一下。</summary>
    public void TipOnce()
    {
        if (_tipShown) return;
        _tipShown = true;
        _icon.ShowBalloonTip(3000, "CS2 盒子", "已缩小到托盘，右键图标可以退出。", Forms.ToolTipIcon.None);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
