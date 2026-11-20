namespace CyberWall.Common;

/// <summary>
/// Hover text of the tray icon. The suite standard is the app name plus a single
/// segment, capped at 63 characters because that is what Windows displays, with
/// the ellipsis included in the cap.
/// </summary>
public static class TrayTooltipText
{
    public const int MaxLength = 63;

    public static string Format(string? segment)
    {
        string clean = segment?.Trim() ?? string.Empty;
        string text = clean.Length == 0 ? "CyberWall" : $"CyberWall · {clean}";
        return text.Length > MaxLength ? text[..(MaxLength - 1)] + "…" : text;
    }
}
