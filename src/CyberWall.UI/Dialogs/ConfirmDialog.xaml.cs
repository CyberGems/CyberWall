using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CyberWall.Common.I18n;
using CyberWall.UI.Converters;
using CyberWall.UI.Services;

namespace CyberWall.UI.Dialogs;

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
            FlamePath.Visibility = Visibility.Collapsed;
        }
        else
        {
            AppIconImg.Visibility = Visibility.Collapsed;
            FlamePath.Visibility = Visibility.Visible;
        }
    }

    public ConfirmDialog(string title, string message, string okText, string? cancelText = null)
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
        FlamePath.Visibility = Visibility.Visible;
    }

    public static bool Show(Window? owner, string title, string message, string okText, string? cancelText = null)
    {
        var dlg = new ConfirmDialog(title, message, okText, cancelText);
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
