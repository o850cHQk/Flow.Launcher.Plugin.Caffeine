using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Flow.Launcher.Plugin;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.Caffeine
{
    public class Caffeine : IPlugin
    {
        internal PluginInitContext Context;
        private bool _enabled = false;

        internal static class NativeMethods
        {
            // Import SetThreadExecutionState Win32 API and necessary flags
            [DllImport("kernel32.dll")]
            public static extern uint SetThreadExecutionState(uint esFlags);
            public const uint ES_CONTINUOUS = 0x80000000;
            public const uint ES_SYSTEM_REQUIRED = 0x00000001;
            public const uint ES_AWAYMODE_REQUIRED = 0x00000040;
            public const uint ES_DISPLAY_REQUIRED = 0x00000040;
        }

        public List<Result> Query(Query query)
        {
            var result = new Result
            {
                Title = $"Turn {CaffeineState()} Caffeine",
                SubTitle = "Toggle Caffeine off and on.",
                Action = c =>
                {
                    if (!_enabled)
                    {
                        PowerUtilities.PreventPowerSave();
                        _enabled = true;
                    }
                    else
                    {
                        PowerUtilities.Shutdown();
                        _enabled = false;
                    }
                    return true;
                },
                IcoPath = "Images/icon.png"
            };
            return new List<Result> { result };
        }

        public string CaffeineState()
        {
            if (!_enabled)
            {
                return "On";
            }
            else
            {
                return "Off";
            }
        }

        private void KeepAlive()
        {
            NativeMethods.SetThreadExecutionState(NativeMethods.ES_CONTINUOUS | NativeMethods.ES_SYSTEM_REQUIRED | NativeMethods.ES_AWAYMODE_REQUIRED);
        }
        public void Init(PluginInitContext context)
        {
            Context = context;
        }

    }
    public static class PowerUtilities
    {
        [Flags]
        public enum EXECUTION_STATE : uint
        {
            ES_AWAYMODE_REQUIRED = 0x00000040,
            ES_CONTINUOUS = 0x80000000,
            ES_DISPLAY_REQUIRED = 0x00000002,
            ES_SYSTEM_REQUIRED = 0x00000001
            // Legacy flag, should not be used.
            // ES_USER_PRESENT = 0x00000004
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern uint SetThreadExecutionState(EXECUTION_STATE esFlags);

        private static AutoResetEvent _event = new AutoResetEvent(false);

        public static void PreventPowerSave()
        {
            (new TaskFactory()).StartNew(() =>
                {
                    SetThreadExecutionState(
                        EXECUTION_STATE.ES_CONTINUOUS
                        | EXECUTION_STATE.ES_DISPLAY_REQUIRED
                        | EXECUTION_STATE.ES_SYSTEM_REQUIRED);
                    _event.WaitOne();

                },
                TaskCreationOptions.LongRunning);
        }

        public static void Shutdown()
        {
            _event.Set();
        }
    }
}