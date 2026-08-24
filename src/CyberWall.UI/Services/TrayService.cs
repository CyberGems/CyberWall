using System.Windows;
using WF = System.Windows.Forms;

namespace CyberWall.UI.Services;

public sealed class TrayService : IDisposable
{
    private readonly WF.NotifyIcon _icon;
    private readonly Window _win;
    private bool _exit;

    public TrayService(Window win)
    {
        _win = win;
        _icon = new WF.NotifyIcon
        {
            Visible = true,
            Text = "CyberWall — Firewall por programa",
            Icon = AppIconHelper.CreateShieldIcon(32)
        };
        var menu = new WF.ContextMenuStrip();
        menu.Items.Add("Mostrar", null, (_, _) => Show());
        menu.Items.Add("Salir", null, (_, _) => { _exit = true; _win.Close(); });
        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick += (_, _) => Show();
        _win.StateChanged += OnState;
        _win.Closing += OnClosing;
    }

    private void OnState(object? _, EventArgs __)
    {
        if (_win.WindowState == WindowState.Minimized) { _win.Hide(); _icon.ShowBalloonTip(1200, "CyberWall", "Minizado a bandeja", WF.ToolTipIcon.Info); }
    }

    private void OnClosing(object? _, System.ComponentModel.CancelEventArgs e)
    {
        if (_exit) { _icon.Visible = false; return; }
        e.Cancel = true;
        _win.Hide();
        _icon.ShowBalloonTip(1000, "CyberWall", "Sigue activo en bandeja", WF.ToolTipIcon.Info);
    }

    private void Show() { _win.Show(); _win.WindowState = WindowState.Normal; _win.Activate(); }
    public void Dispose() { _icon.Visible = false; _icon.Dispose(); }
}
