using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CyberWall.Common.Geo;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.UI.Services;
using ComboBox = System.Windows.Controls.ComboBox;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;

namespace CyberWall.UI.Dialogs;

public partial class EditRuleDialog : Window, IModalAttentionWindow
{
    public string AppPath { get; }
    public string DisplayName { get; }
    public Verdict InboundVerdict { get; private set; }
    public Verdict OutboundVerdict { get; private set; }
    public GeoResult Geo { get; }
    public string? CountryCode => Geo.Iso2;
    public bool HasCountry => Geo.HasCountry;
    public string CountryLabel => CountryDisplay.Label(Geo);

    private DateTime _lastAttentionTime = DateTime.MinValue;

    public void TriggerAttention()
    {
        ModalAttentionHelper.Trigger(this, OuterBorder, WindowScale, WindowGlow, ref _lastAttentionTime);
    }

    public EditRuleDialog(AppRule rule, GeoResult? geo = null)
    {
        AppPath = rule.AppPath;
        DisplayName = rule.DisplayName;
        InboundVerdict = rule.EffectiveInboundVerdict;
        OutboundVerdict = rule.EffectiveOutboundVerdict;
        Geo = geo ?? GeoResult.Unknown;

        InitializeComponent();
        Icon = AppIconHelper.CreateShieldImageSource(64);
        CyberWallWindowChrome.Apply(this, 12);

        AppNameLbl.Text = DisplayName;
        AppPathLbl.Text = AppPath;
        AppPathLbl.ToolTip = AppPath;

        CountryFlag.CountryCode = CountryCode ?? "";
        CountryFlag.HasCountry = HasCountry;
        CountryNameLbl.Text = CountryLabel;
        CountryBadge.ToolTip = CountryLabel;

        PopulateOptions();
        RefreshLanguage();
    }

    private void PopulateOptions()
    {
        IncomingBox.Items.Clear();
        IncomingBox.Items.Add(new ComboBoxItem { Content = Strings.T("Allow"), Tag = Verdict.Allow });
        IncomingBox.Items.Add(new ComboBoxItem { Content = Strings.T("Block"), Tag = Verdict.Block });

        OutgoingBox.Items.Clear();
        OutgoingBox.Items.Add(new ComboBoxItem { Content = Strings.T("Allow"), Tag = Verdict.Allow });
        OutgoingBox.Items.Add(new ComboBoxItem { Content = Strings.T("Block"), Tag = Verdict.Block });

        SelectVerdict(IncomingBox, InboundVerdict);
        SelectVerdict(OutgoingBox, OutboundVerdict);
    }

    private static void SelectVerdict(ComboBox box, Verdict verdict)
    {
        foreach (var item in box.Items)
        {
            if (item is ComboBoxItem cbi && cbi.Tag is Verdict v && v == verdict)
            {
                box.SelectedItem = cbi;
                return;
            }
        }
        if (box.Items.Count > 0) box.SelectedIndex = 0;
    }

    private void RefreshLanguage()
    {
        Title = $"CyberWall — {Strings.T("EditRuleTitle")}";
        DialogTitleLbl.Text = Strings.T("EditRuleTitle");
        DialogSubtitleLbl.Text = Strings.T("EditRuleSubtitle");
        IncomingTitleLbl.Text = Strings.T("IncomingTraffic");
        IncomingDescLbl.Text = Strings.T("IncomingTrafficDesc");
        OutgoingTitleLbl.Text = Strings.T("OutgoingTraffic");
        OutgoingDescLbl.Text = Strings.T("OutgoingTrafficDesc");
        PresetClientBtn.Content = Strings.T("PresetClient");
        PresetBothBtn.Content = Strings.T("PresetBoth");
        PresetBlockBtn.Content = Strings.T("PresetBlock");
        SaveBtn.Content = Strings.T("Save");
        CancelBtn.Content = Strings.T("Cancel");
    }

    private void PresetClient_Click(object sender, RoutedEventArgs e)
    {
        SelectVerdict(IncomingBox, Verdict.Block);
        SelectVerdict(OutgoingBox, Verdict.Allow);
    }

    private void PresetBoth_Click(object sender, RoutedEventArgs e)
    {
        SelectVerdict(IncomingBox, Verdict.Allow);
        SelectVerdict(OutgoingBox, Verdict.Allow);
    }

    private void PresetBlock_Click(object sender, RoutedEventArgs e)
    {
        SelectVerdict(IncomingBox, Verdict.Block);
        SelectVerdict(OutgoingBox, Verdict.Block);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (IncomingBox.SelectedItem is ComboBoxItem inItem && inItem.Tag is Verdict inV)
            InboundVerdict = inV;
        if (OutgoingBox.SelectedItem is ComboBoxItem outItem && outItem.Tag is Verdict outV)
            OutboundVerdict = outV;

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
