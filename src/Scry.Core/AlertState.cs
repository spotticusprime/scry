namespace Scry.Core;

public enum AlertState
{
    Pending = 0,
    Firing,
    Acknowledged,
    Resolved,
    Suppressed,
}
