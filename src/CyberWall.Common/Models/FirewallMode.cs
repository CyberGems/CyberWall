namespace CyberWall.Common.Models;

public enum FirewallMode
{
    Disabled = 0,
    BlockAll = 1,
    Ask = 2
}

public enum Verdict
{
    Allow = 0,
    Block = 1,
    Ask = 2
}

public enum Direction
{
    Inbound = 0,
    Outbound = 1,
    Both = 2
}

public enum PopupDecision
{
    None = 0,
    AllowAlways,
    AllowOnce,
    BlockAlways,
    Dismiss
}
