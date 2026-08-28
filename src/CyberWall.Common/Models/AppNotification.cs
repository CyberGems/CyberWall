namespace CyberWall.Common.Models;

public enum AppNotificationKind
{
    AutoBlocked = 0,
    SilentBlock = 1,
    ProtectionOff = 2,
    UpdateAvailable = 3,
    InternetLost = 4,
    InternetRestored = 5,
    AppVersionChanged = 6,
    AppExecutableChanged = 7
}

public sealed class AppNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public AppNotificationKind Kind { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool Read { get; set; }
    public string? AppPath { get; set; }
    public string? AppName { get; set; }
    public string? Detail { get; set; }
}
