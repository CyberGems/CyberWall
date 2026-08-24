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
        DataContext = this;
        Loaded += (_, _) => PositionBottomRight();
        TitleLbl.Text = Strings.T("NewConnection");
        AppLbl.Text = Strings.T("AppWantsToConnect", ev.DisplayName);
        BlockBtn.Content = Strings.T("Block");
        AllowBtn.Content = Strings.T("Allow");
        RememberChk.Content = Strings.T("Remember");
        DetailLbl.Text = $"{(ev.Direction == Direction.Inbound ? Strings.T("Inbound") : Strings.T("Outbound"))} • {ev.Protocol} • {ev.RemoteAddress}:{ev.RemotePort} • PID {ev.ProcessId}";
        PathLbl.Text = ev.AppPath;
        MouseLeftButtonDown += (_, _) => DragMove();
    }

    private void PositionBottomRight()
    {
        var wa = SystemParameters.WorkArea;
        var open = System.Windows.Application.Current.Windows.OfType<ConnectionPopup>().Count(w => w.IsVisible && w != this);
        Left = wa.Right - Width - 20;
        Top = wa.Bottom - Height - 20 - open * (Height + 10);
    }

    private void Allow_Click(object s, RoutedEventArgs e) { ResultVerdict = Verdict.Allow; Close(); ClosedWithVerdict?.Invoke(this); }
    private void Block_Click(object s, RoutedEventArgs e) { ResultVerdict = Verdict.Block; Close(); ClosedWithVerdict?.Invoke(this); }
    private void Close_Click(object s, RoutedEventArgs e) { ResultVerdict = Verdict.Block; Close(); ClosedWithVerdict?.Invoke(this); }
    protected override void OnClosed(EventArgs e) { base.OnClosed(e); ClosedWithVerdict?.Invoke(this); }
}
