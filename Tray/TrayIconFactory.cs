using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Flow.Launcher.Plugin.Caffeine.Tray;

/// <summary>
/// Factory class for creating NotifyIcon instances.
/// </summary>
internal static class TrayIconFactory
{
    /// <summary>
    /// Creates a new caffeine NotifyIcon with a fully populated context menu.
    /// </summary>
    /// <param name="context">The plugin initialization context</param>
    /// <param name="onDurationSelected">Callback when a duration preset is clicked (null = indefinite)</param>
    /// <param name="onTurnOff">Callback when Turn Off is clicked</param>
    /// <param name="getKeepDisplayOn">Func returning whether keep display on is currently enabled</param>
    /// <param name="onToggleKeepDisplayOn">Callback when Keep screen active is toggled</param>
    /// <returns>The configured NotifyIcon and the status menu item for later updates</returns>
    public static (NotifyIcon notifyIcon, ToolStripMenuItem statusItem) CreateCaffeineIcon(
        PluginInitContext context,
        Action<TimeSpan?> onDurationSelected,
        Action onTurnOff,
        Func<bool> getKeepDisplayOn,
        Action<bool> onToggleKeepDisplayOn)
    {
        var notifyIcon = new NotifyIcon();
        var contextMenu = new ContextMenuStrip();

        var iconPath = Path.Combine(context.CurrentPluginMetadata.PluginDirectory, "Images/icon.ico");
        if (File.Exists(iconPath))
            notifyIcon.Icon = new Icon(iconPath);

        // Status label (non-clickable, updated by TrayIconManager)
        var statusItem = new ToolStripMenuItem("Caffeine is active") { Enabled = false };
        contextMenu.Items.Add(statusItem);
        contextMenu.Items.Add(new ToolStripSeparator());

        // Keep screen active checkbox toggle
        var keepDisplayItem = new ToolStripMenuItem("Keep screen active")
        {
            CheckOnClick = true,
            Checked = getKeepDisplayOn()
        };
        keepDisplayItem.Click += (s, e) =>
        {
            var isChecked = keepDisplayItem.Checked;
            Task.Run(() => onToggleKeepDisplayOn(isChecked));
        };
        contextMenu.Items.Add(keepDisplayItem);
        contextMenu.Items.Add(new ToolStripSeparator());

        // Duration presets — Task.Run prevents deadlock when callback hides the tray from the STA thread
        var presets = new (string label, TimeSpan? duration)[]
        {
            ("Indefinite", null),
            ("30 minutes", TimeSpan.FromMinutes(30)),
            ("1 hour", TimeSpan.FromHours(1)),
            ("2 hours", TimeSpan.FromHours(2)),
            ("8 hours", TimeSpan.FromHours(8)),
        };

        foreach (var (label, duration) in presets)
        {
            var d = duration;
            contextMenu.Items.Add(new ToolStripMenuItem(label, null, (s, e) => Task.Run(() => onDurationSelected(d))));
        }

        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(new ToolStripMenuItem("Turn Off", null, (s, e) => Task.Run(onTurnOff)));

        notifyIcon.Text = "Caffeine is active";
        notifyIcon.ContextMenuStrip = contextMenu;
        notifyIcon.Visible = false;

        return (notifyIcon, statusItem);
    }
}
