using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.Caffeine.Utilities;

/// <summary>
/// Utilities for preventing the system from going to sleep
/// </summary>
public static class PowerUtilities
{
    /// <summary>
    /// Execution state flags for preventing power save mode
    /// </summary>
    [Flags]
    public enum EXECUTION_STATE : uint
    {
        /// <summary>
        /// Away mode required - Keeps background processing &amp; network alive while allowing display off
        /// </summary>
        ES_AWAYMODE_REQUIRED = 0x00000040,
        /// <summary>
        /// Continuous execution state
        /// </summary>
        ES_CONTINUOUS = 0x80000000,
        /// <summary>
        /// Display required to stay on
        /// </summary>
        ES_DISPLAY_REQUIRED = 0x00000002,
        /// <summary>
        /// System required to stay awake
        /// </summary>
        ES_SYSTEM_REQUIRED = 0x00000001
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct REASON_CONTEXT
    {
        public uint Version;
        public uint Flags;
        public string SimpleReasonString;
    }

    private enum POWER_REQUEST_TYPE
    {
        PowerRequestDisplayRequired = 0,
        PowerRequestSystemRequired = 1,
        PowerRequestAwayModeRequired = 2,
        PowerRequestExecutionRequired = 3
    }

    private const uint POWER_REQUEST_CONTEXT_VERSION = 0;
    private const uint POWER_REQUEST_CONTEXT_SIMPLE_STRING = 0x1;

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern uint SetThreadExecutionState(EXECUTION_STATE esFlags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr PowerCreateRequest(ref REASON_CONTEXT Context);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool PowerSetRequest(IntPtr PowerRequestHandle, POWER_REQUEST_TYPE RequestType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool PowerClearRequest(IntPtr PowerRequestHandle, POWER_REQUEST_TYPE RequestType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    private static readonly object _lock = new();
    private static AutoResetEvent _event;
    private static IntPtr _powerRequestHandle = IntPtr.Zero;

    /// <summary>
    /// Prevent the system from entering power save mode
    /// </summary>
    /// <param name="keepDisplayOn">Whether to force the display to stay on in addition to system sleep prevention</param>
    public static void PreventPowerSave(bool keepDisplayOn = true)
    {
        lock (_lock)
        {
            Shutdown();

            _event = new AutoResetEvent(false);
            var currentEvent = _event;

            // Create Win32 Power Availability Request for Modern Standby & Windows Power Manager
            try
            {
                var context = new REASON_CONTEXT
                {
                    Version = POWER_REQUEST_CONTEXT_VERSION,
                    Flags = POWER_REQUEST_CONTEXT_SIMPLE_STRING,
                    SimpleReasonString = "Caffeine Flow Launcher Plugin - Active Workload"
                };

                _powerRequestHandle = PowerCreateRequest(ref context);
                if (_powerRequestHandle != IntPtr.Zero && _powerRequestHandle != new IntPtr(-1))
                {
                    PowerSetRequest(_powerRequestHandle, POWER_REQUEST_TYPE.PowerRequestSystemRequired);
                    PowerSetRequest(_powerRequestHandle, POWER_REQUEST_TYPE.PowerRequestExecutionRequired);

                    if (keepDisplayOn)
                    {
                        PowerSetRequest(_powerRequestHandle, POWER_REQUEST_TYPE.PowerRequestDisplayRequired);
                    }
                    else
                    {
                        PowerSetRequest(_powerRequestHandle, POWER_REQUEST_TYPE.PowerRequestAwayModeRequired);
                    }
                }
            }
            catch
            {
                // Fallback to SetThreadExecutionState if PowerCreateRequest fails
            }

            new TaskFactory().StartNew(() =>
                {
                    var flags = EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_SYSTEM_REQUIRED;
                    if (keepDisplayOn)
                    {
                        flags |= EXECUTION_STATE.ES_DISPLAY_REQUIRED;
                    }
                    else
                    {
                        flags |= EXECUTION_STATE.ES_AWAYMODE_REQUIRED;
                    }

                    SetThreadExecutionState(flags);
                    currentEvent.WaitOne();
                    SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
                },
                TaskCreationOptions.LongRunning);
        }
    }

    /// <summary>
    /// Allow the system to enter power save mode again
    /// </summary>
    public static void Shutdown()
    {
        lock (_lock)
        {
            if (_powerRequestHandle != IntPtr.Zero && _powerRequestHandle != new IntPtr(-1))
            {
                try
                {
                    PowerClearRequest(_powerRequestHandle, POWER_REQUEST_TYPE.PowerRequestSystemRequired);
                    PowerClearRequest(_powerRequestHandle, POWER_REQUEST_TYPE.PowerRequestExecutionRequired);
                    PowerClearRequest(_powerRequestHandle, POWER_REQUEST_TYPE.PowerRequestDisplayRequired);
                    PowerClearRequest(_powerRequestHandle, POWER_REQUEST_TYPE.PowerRequestAwayModeRequired);
                    CloseHandle(_powerRequestHandle);
                }
                catch { }
                _powerRequestHandle = IntPtr.Zero;
            }

            if (_event != null)
            {
                _event.Set();
                _event.Dispose();
                _event = null;
            }
        }
    }
}
