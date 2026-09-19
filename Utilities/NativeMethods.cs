using System.Runtime.InteropServices;

namespace Flow.Launcher.Plugin.Caffeine.Utilities;

/// <summary>
/// Native Windows API methods
/// </summary>
internal static class NativeMethods
{
    // Import SetThreadExecutionState Win32 API and necessary flags
    [DllImport("kernel32.dll")]
    public static extern uint SetThreadExecutionState(uint esFlags);
    public const uint ES_CONTINUOUS = 0x80000000;
    public const uint ES_SYSTEM_REQUIRED = 0x00000001;
    public const uint ES_AWAYMODE_REQUIRED = 0x00000040;
    public const uint ES_DISPLAY_REQUIRED = 0x00000002;
}
