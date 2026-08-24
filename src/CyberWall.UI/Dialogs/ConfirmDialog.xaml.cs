using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CyberWall.Common.I18n;
using CyberWall.UI.Converters;

namespace CyberWall.UI.Dialogs;

public partial class ConfirmDialog : Window
{
    private static readonly PathToIconConverter IconConv = new();

    public ConfirmDialog(string appName, string appPath)
    {
        InitializeComponent();

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
