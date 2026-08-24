using System.Windows;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;

namespace CyberWall.UI.Popup;

public partial class ConnectionPopup : Window
{
    public ConnectionEvent Event { get; }
    public Verdict ResultVerdict { get; private set; } = Verdict.Block;
    public bool Remember => RememberChk.IsChecked == true;
    public event Action<ConnectionPopup>? ClosedWithVerdict;

    public ConnectionPopup(ConnectionEvent ev)
    {
        InitializeComponent();
        Event = ev;
        Loaded += (_, _) => PositionBottomRight();
        TitleLbl.Text = Strings.T("NewConnection");
        AppLbl.Text = Strings.T("AppWantsToConnect", ev.DisplayName);
        RememberChk.Content = Strings.T("Remember");
        DetailLbl.Text = $"{(ev.Direction == Direction.Inbound ? Strings.T("Inbound") : Strings.T("Outbound"))}  \u2022  {ev.Protocol}  \u2022  {ev.RemoteAddress}:{ev.RemotePort}  \u2022  PID {ev.ProcessId}";
        PathLbl.Text = ev.AppPath;
        MouseLeftButtonDown += (_, _) => DragMove();
    }

    private void PositionBottomRight()
    {
        var wa = SystemParameters.WorkArea;
        var offset = 16 + (Owner is Window ? 0 : 0);
        var open = System.Windows.Application.Current.Windows.OfType<ConnectionPopup>().Count(w => w.IsVisible && w != this);
        Left = wa.Right - Width - 16;
        Top = wa.Bottom - Height - 16 - open * (Height + 8);
    }

    private void Allow_Click(object s, RoutedEventArgs e) { ResultVerdict = Verdict.Allow; Close(); ClosedWithVerdict?.Invoke(this); }
    private void Block_Click(object s, RoutedEventArgs e) { ResultVerdict = Verdict.Block; Close(); ClosedWithVerdict?.Invoke(this); }
    private void Close_Click(object s, RoutedEventArgs e) { ResultVerdict = Verdict.Block; Close(); ClosedWithVerdict?.Invoke(this); }
    protected override void OnClosed(EventArgs e) { base.OnClosed(e); ClosedWithVerdict?.Invoke(this); }
}
