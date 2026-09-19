using System.Windows.Controls;
using Flow.Launcher.Plugin.Caffeine.Tray;

namespace Flow.Launcher.Plugin.Caffeine.Settings;

public partial class PluginSettings : UserControl
{
    private readonly Caffeine _caffeine;
    private readonly PluginInitContext _context;
    private readonly Settings _settings;
    private bool _isLoading;

    /// <summary>
    /// Initialize the plugin settings UI
    /// </summary>
    /// <param name="caffeine">The plugin instance</param>
    /// <param name="context">Plugin context</param>
    /// <param name="settings">Plugin settings</param>
    public PluginSettings(Caffeine caffeine, PluginInitContext context, Settings settings)
    {
        InitializeComponent();
        _caffeine = caffeine;
        _context = context;
        _settings = settings;
        LoadSettings();
    }

    private void LoadSettings()
    {
        _isLoading = true;
        StartWithFlowLauncherCheckBox.IsChecked = _settings.StartWithFlowLauncher;
        SendNotificationsCheckBox.IsChecked = _settings.SendNotifications;
        ShowTrayIconCheckBox.IsChecked = _settings.ShowTrayIcon;
        _isLoading = false;
    }

    private void StartWithFlowLauncherCheckBox_Changed(object sender, System.Windows.RoutedEventArgs e)
    {
        if (!_isLoading)
            SaveSettings();
    }

    private void SendNotificationsCheckBox_Changed(object sender, System.Windows.RoutedEventArgs e)
    {
        if (!_isLoading)
            SaveSettings();
    }

    private void ShowTrayIconCheckBox_Changed(object sender, System.Windows.RoutedEventArgs e)
    {
        if (!_isLoading)
            SaveSettings();
    }

    private void SaveSettings()
    {
        _settings.StartWithFlowLauncher = StartWithFlowLauncherCheckBox.IsChecked ?? false;
        _settings.SendNotifications = SendNotificationsCheckBox.IsChecked ?? true;
        _settings.ShowTrayIcon = ShowTrayIconCheckBox.IsChecked ?? true;

        _context.API.SaveSettingJsonStorage<Settings>();

        if (_settings.ShowTrayIcon && Caffeine.IsActive) _caffeine.ShowTrayIfActive();
        if (!_settings.ShowTrayIcon) TrayIconManager.HideTray();
        if (Caffeine.IsActive) _caffeine.UpdatePowerSettings();
    }
}
