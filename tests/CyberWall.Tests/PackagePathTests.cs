using CyberWall.Common;

namespace CyberWall.Tests;

public class PackagePathTests
{
    /// <summary>
    /// The package family name is "Name_PublisherId". The architecture and the
    /// resource id of the package full name are not part of it.
    /// </summary>
    [Fact]
    public void ReadsTheFamilyNameFromAWindowsAppsPath()
    {
        var path = @"C:\Program Files\WindowsApps\WhatsApp_2.2420.5.0_x64__8wekyb3d8bbwe\app\WhatsApp.exe";
        Assert.True(PackagePath.TryGetFamilyName(path, out var family));
        Assert.Equal("WhatsApp_8wekyb3d8bbwe", family);
    }

    [Fact]
    public void ReadsThePackageDirectoryFromAWindowsAppsPath()
    {
        var path = @"C:\Program Files\WindowsApps\WhatsApp_2.2420.5.0_x64__8wekyb3d8bbwe\app\WhatsApp.exe";
        Assert.True(PackagePath.TryGetPackageDir(path, out var dir));
        Assert.Equal(@"C:\Program Files\WindowsApps\WhatsApp_2.2420.5.0_x64__8wekyb3d8bbwe", dir);
    }

    [Fact]
    public void WorksWithForwardSlashes()
    {
        var path = "C:/Program Files/WindowsApps/Demo_1.0.0.0_x64__abcdefghijklmnop/Demo.exe";
        Assert.True(PackagePath.TryGetFamilyName(path, out var family));
        Assert.Equal("Demo_abcdefghijklmnop", family);
    }

    [Fact]
    public void ReturnsFalseForRegularProgramFiles()
    {
        Assert.False(PackagePath.TryGetFamilyName(@"C:\Program Files\Notepad\notepad.exe", out var family));
        Assert.Equal("", family);
    }

    [Fact]
    public void ReturnsFalseForDeletedPackageFolders()
    {
        Assert.False(PackagePath.TryGetFamilyName(@"C:\Program Files\WindowsApps\Deleted\Demo.exe", out _));
        Assert.False(PackagePath.TryGetFamilyName(@"C:\Program Files\WindowsApps\DeletedAllUserPackages\Demo.exe", out _));
    }

    [Fact]
    public void ReturnsFalseForEmptyInput()
    {
        Assert.False(PackagePath.TryGetFamilyName(null, out _));
        Assert.False(PackagePath.TryGetFamilyName("   ", out _));
    }
}
