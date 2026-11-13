using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using CyberWall.Common.Backups;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.Common.Settings;
using CyberWall.UI.Services;
using WpfBrush = System.Windows.Media.Brush;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace CyberWall.UI.Dialogs;

public partial class BackupRestoreDialog : Window
{
    private sealed class BackupItemViewModel
    {
        public required BackupEntryInfo Entry { get; init; }
        public bool IsExternal { get; init; }
        public BackupDocument? CachedDocument { get; init; }
        public string FilePath => Entry.FilePath;
        public string FileName => Entry.FileName;

        public string DateDisplay => Entry.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd  HH:mm:ss");

        public string ReasonLabel => Entry.Reason switch
        {
            BackupReason.Auto => Strings.T("BackupReasonAuto"),
            BackupReason.Manual => Strings.T("BackupReasonManual"),
            BackupReason.PreClear => Strings.T("BackupReasonPreClear"),
            BackupReason.PreRestore => Strings.T("BackupReasonPreRestore"),
            _ => Entry.Reason.ToString()
        };

        public WpfBrush BadgeBackground { get; init; } = System.Windows.Media.Brushes.Transparent;
        public WpfBrush BadgeForeground { get; init; } = System.Windows.Media.Brushes.Transparent;

        public string SummaryDisplay
        {
            get
            {
                var rulesPart = string.Format(
                    Strings.T("BackupRulesCountFmt"),
                    Entry.TotalRules,
                    Entry.AllowedCount,
                    Entry.BlockedCount);

                var modeLabel = Entry.FirewallMode switch
                {
                    FirewallMode.Ask => Strings.T("ModeAskShort"),
                    FirewallMode.BlockAll => Strings.T("ModeBlockAllShort"),
                    FirewallMode.Killswitch => Strings.T("ModeKillswitchShort"),
                    _ => Entry.FirewallMode.ToString()
                };

                return $"{rulesPart} · {modeLabel}";
            }
        }

        public string SizeDisplay => FormatBytes(Entry.SizeBytes);

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            double kb = bytes / 1024.0;
            if (kb < 1024) return $"{kb:0.#} KB";
            return $"{kb / 1024.0:0.##} MB";
        }
    }

    private readonly AppSettings _settings;
    private readonly List<BackupItemViewModel> _items = new();

    public BackupDocument? SelectedDocument { get; private set; }
    public string? SelectedFileName { get; private set; }
    public bool RestoreRules { get; private set; }
    public bool RestoreSettings { get; private set; }

    public BackupRestoreDialog(AppSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        InitializeComponent();
        CyberWallWindowChrome.Apply(this, 12);
        Icon = AppIconHelper.CreateShieldImageSource(64);

        ApplyTexts();
        ReloadList(selectFilePath: null);
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        };
    }

    private void ApplyTexts()
    {
        Title = Strings.T("BackupRestoreTitle");
        TitleLbl.Text = Strings.T("BackupRestoreTitle");
        SubtitleLbl.Text = Strings.T("BackupRestoreSubtitle");
        AvailableListLbl.Text = Strings.T("BackupAvailableList");
        EmptyStateLbl.Text = Strings.T("BackupEmptyList");
        BrowseFileBtn.Content = Strings.T("BackupBrowseFileBtn");
        DeleteBtn.Content = Strings.T("BackupDeleteSelectedBtn");
        WhatToRestoreLbl.Text = Strings.T("BackupWhatToRestore");
        RestoreRulesChk.Content = Strings.T("BackupRestoreRulesChk");
        RestoreSettingsChk.Content = Strings.T("BackupRestoreSettingsChk");
        SafetyHintLbl.Text = Strings.T("BackupRestoreSafetyHint");
        KillswitchWarnLbl.Text = Strings.T("BackupRestoreKillswitchWarn");
        CancelBtn.Content = Strings.T("Cancel");
        RestoreBtn.Content = Strings.T("BackupRestoreApplyBtn");
        AutomationProperties.SetName(CloseHeaderBtn, Strings.T("Cancel"));
    }

    private void ReloadList(string? selectFilePath)
    {
        var externalItems = _items.Where(i => i.IsExternal).ToList();
        _items.Clear();

        foreach (var ext in externalItems)
        {
            if (File.Exists(ext.FilePath))
            {
                _items.Add(ext);
            }
        }

        var folderEntries = BackupService.ListBackups(_settings);
        foreach (var entry in folderEntries)
        {
            if (!entry.IsValid) continue;
            if (_items.Any(i => string.Equals(i.FilePath, entry.FilePath, StringComparison.OrdinalIgnoreCase)))
                continue;

            _items.Add(CreateItemViewModel(entry, isExternal: false, cachedDoc: null));
        }

        BackupListBox.ItemsSource = null;
        BackupListBox.ItemsSource = _items;
        EmptyStateLbl.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (_items.Count > 0)
        {
            var match = !string.IsNullOrWhiteSpace(selectFilePath)
                ? _items.FirstOrDefault(i => string.Equals(i.FilePath, selectFilePath, StringComparison.OrdinalIgnoreCase))
                : null;
            BackupListBox.SelectedItem = match ?? _items[0];
        }

        UpdateActionStates();
    }

    private BackupItemViewModel CreateItemViewModel(BackupEntryInfo entry, bool isExternal, BackupDocument? cachedDoc)
    {
        var (bgKey, fgKey) = entry.Reason switch
        {
            BackupReason.Manual => ("BadgeAllowBgBrush", "BadgeAllowFgBrush"),
            BackupReason.PreClear or BackupReason.PreRestore => ("BadgeWarnBgBrush", "BadgeWarnFgBrush"),
            _ => ("CardBrush", "AccentBrush")
        };

        return new BackupItemViewModel
        {
            Entry = entry,
            IsExternal = isExternal,
            CachedDocument = cachedDoc,
            BadgeBackground = (TryFindResource(bgKey) as WpfBrush) ?? System.Windows.Media.Brushes.Transparent,
            BadgeForeground = (TryFindResource(fgKey) as WpfBrush) ?? System.Windows.Media.Brushes.Gray
        };
    }

    private void BackupListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (BackupListBox.SelectedItem is BackupItemViewModel vm)
        {
            RestoreRulesChk.IsEnabled = vm.Entry.HasRules;
            if (!vm.Entry.HasRules) RestoreRulesChk.IsChecked = false;
            else if (RestoreRulesChk.IsChecked != true && RestoreSettingsChk.IsChecked != true) RestoreRulesChk.IsChecked = true;

            RestoreSettingsChk.IsEnabled = vm.Entry.HasSettings;
            if (!vm.Entry.HasSettings) RestoreSettingsChk.IsChecked = false;
            else if (RestoreSettingsChk.IsChecked != true && !vm.Entry.HasRules) RestoreSettingsChk.IsChecked = true;
        }

        UpdateActionStates();
    }

    private void RestoreScope_Changed(object sender, RoutedEventArgs e)
    {
        UpdateActionStates();
    }

    private void UpdateActionStates()
    {
        if (!IsInitialized
            || BackupListBox == null
            || DeleteBtn == null
            || RestoreRulesChk == null
            || RestoreSettingsChk == null
            || RestoreBtn == null
            || KillswitchWarnBanner == null)
        {
            return;
        }

        var selected = BackupListBox.SelectedItem as BackupItemViewModel;
        DeleteBtn.IsEnabled = selected != null && !selected.IsExternal;

        bool wantsRules = RestoreRulesChk.IsEnabled && RestoreRulesChk.IsChecked == true;
        bool wantsSettings = RestoreSettingsChk.IsEnabled && RestoreSettingsChk.IsChecked == true;

        RestoreBtn.IsEnabled = selected != null && (wantsRules || wantsSettings);

        bool showKillswitchWarning = selected != null
            && wantsSettings
            && selected.Entry.FirewallMode is FirewallMode.BlockAll or FirewallMode.Killswitch;

        KillswitchWarnBanner.Visibility = showKillswitchWarning ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BrowseFileBtn_Click(object sender, RoutedEventArgs e)
    {
        var dir = BackupService.GetEffectiveDirectory(_settings);
        var dlg = new OpenFileDialog
        {
            Title = Strings.T("BackupBrowseTitle"),
            Filter = $"CyberWall Backup (*{BackupService.FileExtension})|*{BackupService.FileExtension}|JSON (*.json)|*.json",
            InitialDirectory = Directory.Exists(dir) ? dir : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dlg.ShowDialog(this) != true) return;

        var load = BackupService.LoadAndVerify(dlg.FileName);
        if (!load.Success || load.Document == null)
        {
            var errMsg = ResolveLoadError(load);
            var errDlg = new ConfirmDialog(Strings.T("BackupRestoreTitle"), errMsg, Strings.T("Ok"), cancelText: null, ConfirmIconType.Warning)
            {
                Owner = this
            };
            errDlg.ShowDialog();
            return;
        }

        var existing = _items.FirstOrDefault(i => string.Equals(i.FilePath, dlg.FileName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            BackupListBox.SelectedItem = existing;
            return;
        }

        var doc = load.Document;
        var fi = new FileInfo(dlg.FileName);
        var entry = new BackupEntryInfo
        {
            FilePath = fi.FullName,
            FileName = fi.Name,
            CreatedUtc = doc.CreatedUtc,
            Reason = doc.Reason,
            TotalRules = doc.Summary.TotalRules,
            AllowedCount = doc.Summary.AllowedCount,
            BlockedCount = doc.Summary.BlockedCount,
            FirewallMode = doc.Summary.FirewallMode,
            SizeBytes = fi.Exists ? fi.Length : 0,
            IsValid = true,
            HasRules = doc.Payload.Rules != null,
            HasSettings = doc.Payload.Settings != null
        };

        _items.Insert(0, CreateItemViewModel(entry, isExternal: true, cachedDoc: doc));
        BackupListBox.ItemsSource = null;
        BackupListBox.ItemsSource = _items;
        EmptyStateLbl.Visibility = Visibility.Collapsed;
        BackupListBox.SelectedIndex = 0;
        UpdateActionStates();
    }

    private void DeleteBtn_Click(object sender, RoutedEventArgs e)
    {
        if (BackupListBox.SelectedItem is not BackupItemViewModel selected || selected.IsExternal) return;

        var msg = string.Format(Strings.T("BackupDeleteConfirmMsg"), selected.FileName);
        var confirm = new ConfirmDialog(Strings.T("BackupDeleteConfirmTitle"), msg, Strings.T("Ok"), Strings.T("Cancel"), ConfirmIconType.Trash)
        {
            Owner = this
        };
        if (confirm.ShowDialog() != true) return;

        if (BackupService.DeleteBackup(selected.FilePath))
        {
            ReloadList(selectFilePath: null);
        }
    }

    private void RestoreBtn_Click(object sender, RoutedEventArgs e)
    {
        if (BackupListBox.SelectedItem is not BackupItemViewModel selected) return;

        bool wantsRules = RestoreRulesChk.IsEnabled && RestoreRulesChk.IsChecked == true;
        bool wantsSettings = RestoreSettingsChk.IsEnabled && RestoreSettingsChk.IsChecked == true;
        if (!wantsRules && !wantsSettings) return;

        var load = BackupService.LoadAndVerify(selected.FilePath);
        if (!load.Success || load.Document == null)
        {
            var errMsg = ResolveLoadError(load);
            var errDlg = new ConfirmDialog(Strings.T("BackupRestoreTitle"), errMsg, Strings.T("Ok"), cancelText: null, ConfirmIconType.Warning)
            {
                Owner = this
            };
            errDlg.ShowDialog();
            ReloadList(selectFilePath: null);
            return;
        }

        var scopeText = (wantsRules, wantsSettings) switch
        {
            (true, true) => Strings.T("BackupScopeBoth"),
            (true, false) => Strings.T("BackupScopeRulesOnly"),
            _ => Strings.T("BackupScopeSettingsOnly")
        };

        var confirmMsg = string.Format(Strings.T("BackupRestoreConfirmMsg"), selected.FileName, scopeText);
        var confirm = new ConfirmDialog(Strings.T("BackupRestoreTitle"), confirmMsg, Strings.T("BackupRestoreApplyBtn"), Strings.T("Cancel"), ConfirmIconType.Warning)
        {
            Owner = this
        };
        if (confirm.ShowDialog() != true) return;

        SelectedDocument = load.Document;
        SelectedFileName = selected.FileName;
        RestoreRules = wantsRules && load.Document.Payload.Rules != null;
        RestoreSettings = wantsSettings && load.Document.Payload.Settings != null;
        DialogResult = true;
        Close();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static string ResolveLoadError(BackupLoadResult result) => result.ErrorKey switch
    {
        "BackupErrNotFound" => Strings.T("BackupErrNotFound"),
        "BackupErrUnsupportedVersion" => Strings.T("BackupErrUnsupportedVersion"),
        "BackupErrChecksumMismatch" => Strings.T("BackupErrChecksumMismatch"),
        "BackupErrReadFailed" => Strings.T("BackupErrReadFailed"),
        _ => Strings.T("BackupErrInvalidFormat")
    };
}

