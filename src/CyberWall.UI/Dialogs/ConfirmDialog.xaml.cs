using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CyberWall.Common.I18n;
using CyberWall.UI.Converters;
using CyberWall.UI.Services;

namespace CyberWall.UI.Dialogs;

public enum ConfirmIconType
{
    Default,
    Trash,
    Warning,
    Check,
    Info
}

public partial class ConfirmDialog : Window
{
    private static readonly PathToIconConverter IconConv = new();

    public ConfirmDialog(string appName, string appPath)
    {
        InitializeComponent();
        CyberWallWindowChrome.Apply(this, 12);
        Icon = AppIconHelper.CreateShieldImageSource(64);

        AppTitleLbl.Text = string.IsNullOrWhiteSpace(appName) ? System.IO.Path.GetFileNameWithoutExtension(appPath) : appName;
        MessageLbl.Text = Strings.T("RemoveRuleConfirm");
        OkBtn.Content = Strings.T("Ok");
        CancelBtn.Content = Strings.T("Cancel");

        var icon = IconConv.Convert(appPath, typeof(ImageSource), null!, null!) as ImageSource;
        if (icon != null)
        {
            AppIconImg.Source = icon;
            AppIconImg.Visibility = Visibility.Visible;
            TrashPath.Visibility = Visibility.Collapsed;
            AlertPath.Visibility = Visibility.Collapsed;
            CheckPath.Visibility = Visibility.Collapsed;
        }
        else
        {
            AppIconImg.Visibility = Visibility.Collapsed;
            TrashPath.Visibility = Visibility.Visible;
            AlertPath.Visibility = Visibility.Collapsed;
            CheckPath.Visibility = Visibility.Collapsed;
        }
    }

    public ConfirmDialog(string title, string message, string okText = "", string? cancelText = null, ConfirmIconType iconType = ConfirmIconType.Default)
    {
        InitializeComponent();
        CyberWallWindowChrome.Apply(this, 12);
        Icon = AppIconHelper.CreateShieldImageSource(64);

        AppTitleLbl.Text = title;
        MessageLbl.Text = message;
        OkBtn.Content = string.IsNullOrWhiteSpace(okText) ? Strings.T("Ok") : okText;

        if (string.IsNullOrWhiteSpace(cancelText))
        {
            CancelBtn.Visibility = Visibility.Collapsed;
        }
        else
        {
            CancelBtn.Content = cancelText;
            CancelBtn.Visibility = Visibility.Visible;
        }

        AppIconImg.Visibility = Visibility.Collapsed;
        TrashPath.Visibility = Visibility.Collapsed;
        AlertPath.Visibility = Visibility.Collapsed;
        CheckPath.Visibility = Visibility.Collapsed;

        if (iconType == ConfirmIconType.Check || title.Contains("día", StringComparison.OrdinalIgnoreCase) || message.Contains("día", StringComparison.OrdinalIgnoreCase) || title.Contains("up to date", StringComparison.OrdinalIgnoreCase) || message.Contains("up to date", StringComparison.OrdinalIgnoreCase))
        {
            CheckPath.Visibility = Visibility.Visible;
        }
        else if (iconType == ConfirmIconType.Trash || title.Contains("Limpiar", StringComparison.OrdinalIgnoreCase) || title.Contains("Clear", StringComparison.OrdinalIgnoreCase) || title.Contains("Remove", StringComparison.OrdinalIgnoreCase) || title.Contains("Eliminar", StringComparison.OrdinalIgnoreCase))
        {
            TrashPath.Visibility = Visibility.Visible;
        }
        else
        {
            AlertPath.Visibility = Visibility.Visible;
        }
    }

    public static bool Show(Window? owner, string title, string message, string okText, string? cancelText = null, ConfirmIconType iconType = ConfirmIconType.Default)
    {
        var dlg = new ConfirmDialog(title, message, okText, cancelText, iconType);
        if (owner != null) dlg.Owner = owner;
        return dlg.ShowDialog() == true;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
