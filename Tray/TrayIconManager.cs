using System;
using System.Windows.Forms;
using Timer = System.Timers.Timer;

namespace Flow.Launcher.Plugin.Caffeine.Tray;

/// <summary>
/// Manages the system tray icon for the Caffeine plugin.
/// </summary>
public static class TrayIconManager
{
    private static TrayIconThreadManager _threadManager;
    private static readonly object _lock = new();
    private static volatile bool _isVisible = false;

    private static NotifyIcon _notifyIcon;
    private static ToolStripMenuItem _statusItem;
    private static Func<string> _getStatusText;
    private static Timer _refreshTimer;

    /// <summary>
    /// Shows the caffeine tray icon in a separate background thread.
    /// </summary>
    /// <param name="context">The plugin initialization context</param>
    /// <param name="onDurationSelected">Callback when a duration preset is clicked</param>
    /// <param name="onTurnOff">Callback when Turn Off is clicked</param>
    /// <param name="getStatusText">Returns current status text (e.g. "1h 23m remaining" or "Indefinite")</param>
    /// <param name="getKeepDisplayOn">Func returning whether keep display on is currently enabled</param>
    /// <param name="onToggleKeepDisplayOn">Callback when Keep screen active is toggled</param>
    public static void ShowTray(
        PluginInitContext context,
        Action<TimeSpan?> onDurationSelected,
        Action onTurnOff,
        Func<string> getStatusText,
        Func<bool> getKeepDisplayOn,
        Action<bool> onToggleKeepDisplayOn)
    {
        lock (_lock)
        {
            if (_isVisible) return;

            _isVisible = true;
            _getStatusText = getStatusText;
            _threadManager = new TrayIconThreadManager();

            var (notifyIcon, statusItem) = TrayIconFactory.CreateCaffeineIcon(
                context,
                onDurationSelected,
                onTurnOff,
                getKeepDisplayOn,
                onToggleKeepDisplayOn);
            _notifyIcon = notifyIcon;
            _statusItem = statusItem;

            RefreshStatusText();
            _threadManager.StartTrayThread(notifyIcon);
            StartRefreshTimer();
        }
    }

    /// <summary>
    /// Hides the caffeine tray icon and stops the thread.
    /// </summary>
    public static void HideTray()
    {
        lock (_lock)
        {
            if (!_isVisible) return;

            _isVisible = false;
            StopRefreshTimer();
            _threadManager?.Dispose();
            _threadManager = null;
            _notifyIcon = null;
            _statusItem = null;
            _getStatusText = null;
        }
    }

    /// <summary>
    /// Immediately refresh the tooltip and status menu item text.
    /// Call this when the duration changes so the user sees the update right away.
    /// </summary>
    public static void UpdateStatus()
    {
        lock (_lock)
        {
            if (!_isVisible) return;
            RefreshStatusText();
        }
    }

    private static void StartRefreshTimer()
    {
        _refreshTimer = new Timer(60_000);
        _refreshTimer.Elapsed += (s, e) => UpdateStatus();
        _refreshTimer.AutoReset = true;
        _refreshTimer.Start();
    }

    private static void StopRefreshTimer()
    {
        if (_refreshTimer != null)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
            _refreshTimer = null;
        }
    }

    /// <summary>
    /// Updates the tooltip and status menu item by dispatching to the tray's STA thread.
    /// </summary>
    private static void RefreshStatusText()
    {
        var statusText = _getStatusText?.Invoke();
        if (statusText == null || _notifyIcon == null) return;

        var tooltipText = $"Caffeine — {statusText}";
        var menuText = $"Caffeine — {statusText}";

        try
        {
            if (_notifyIcon.ContextMenuStrip?.IsHandleCreated == true)
            {
                _notifyIcon.ContextMenuStrip.Invoke(new Action(() =>
                {
                    _notifyIcon.Text = tooltipText;
                    if (_statusItem != null)
                        _statusItem.Text = menuText;
                }));
            }
            else
            {
                _notifyIcon.Text = tooltipText;
                if (_statusItem != null)
                    _statusItem.Text = menuText;
            }
        }
        catch
        {
            // Icon may be disposing; safe to ignore
        }
    }
}
