using System;
using System.Collections.Generic;
using System.IO;
using Flow.Launcher.Plugin.Caffeine.Settings;
using Flow.Launcher.Plugin.Caffeine.Tray;
using Flow.Launcher.Plugin.Caffeine.Utilities;

namespace Flow.Launcher.Plugin.Caffeine;

/// <summary>
/// Main plugin class for the Caffeine Flow Launcher plugin
/// </summary>
public class Caffeine : IPlugin, ISettingProvider, IDisposable
{
    internal static bool IsActive { get; private set; } = false;

    private PluginInitContext _context;
    private Settings.Settings _settings;
    private string _iconPath;
    private CaffeineTimer _timer;
    private TimeSpan? _activeDuration;

    private static readonly (string label, TimeSpan? duration)[] Presets =
    {
        ("indefinitely", null),
        ("30 minutes", TimeSpan.FromMinutes(30)),
        ("1 hour", TimeSpan.FromHours(1)),
        ("2 hours", TimeSpan.FromHours(2)),
        ("8 hours", TimeSpan.FromHours(8)),
    };

    /// <summary>
    /// Initialize the plugin
    /// </summary>
    /// <param name="context">Plugin initialization context</param>
    public void Init(PluginInitContext context)
    {
        _context = context;
        _settings = context.API.LoadSettingJsonStorage<Settings.Settings>();
        _iconPath = Path.Combine(context.CurrentPluginMetadata.PluginDirectory, "Images/icon.png");

        if (_settings.StartWithFlowLauncher)
        {
            StartCaffeine();
        }
    }

    /// <summary>
    /// Query method for Flow Launcher
    /// </summary>
    public List<Result> Query(Query query)
    {
        var results = new List<Result>();
        var input = query.Search.Trim();

        if (string.IsNullOrEmpty(input))
            return BuildPresetResults(results);

        if (input.Equals("off", StringComparison.OrdinalIgnoreCase))
            return BuildOffResult(results);

        return BuildCustomDurationResult(results, input);
    }

    private List<Result> BuildPresetResults(List<Result> results)
    {
        var score = 500_000;

        if (IsActive)
        {
            results.Add(new Result
            {
                Title = "Turn off Caffeine",
                SubTitle = $"Currently active — {GetStatusText()}",
                Action = c => { StopCaffeine(); return true; },
                IcoPath = _iconPath,
                Score = score
            });

            foreach (var (label, duration) in Presets)
            {
                if (duration == _activeDuration)
                    continue;

                score -= 100_000;
                var d = duration;
                results.Add(new Result
                {
                    Title = $"Switch to {label}",
                    SubTitle = "",
                    Action = c => { StartCaffeine(d); return true; },
                    IcoPath = _iconPath,
                    Score = score
                });
            }
        }
        else
        {
            foreach (var (label, duration) in Presets)
            {
                var d = duration;
                results.Add(new Result
                {
                    Title = $"Keep awake for {label}",
                    SubTitle = "",
                    Action = c => { StartCaffeine(d); return true; },
                    IcoPath = _iconPath,
                    Score = score
                });
                score -= 100_000;
            }
        }

        return results;
    }

    private List<Result> BuildOffResult(List<Result> results)
    {
        if (IsActive)
        {
            results.Add(new Result
            {
                Title = "Turn off Caffeine",
                SubTitle = $"Currently active — {GetStatusText()}",
                Action = c => { StopCaffeine(); return true; },
                IcoPath = _iconPath
            });
        }
        else
        {
            results.Add(new Result
            {
                Title = "Caffeine is not active",
                SubTitle = "Nothing to turn off",
                Action = c => false,
                IcoPath = _iconPath
            });
        }

        return results;
    }

    private List<Result> BuildCustomDurationResult(List<Result> results, string input)
    {
        var parsed = ParseDuration(input);
        if (parsed.HasValue)
        {
            var duration = parsed.Value;
            results.Add(new Result
            {
                Title = $"Keep awake for {FormatDurationLabel(duration)}",
                SubTitle = IsActive ? "Will restart with new duration" : "",
                Action = c => { StartCaffeine(duration); return true; },
                IcoPath = _iconPath
            });
        }
        else
        {
            results.Add(new Result
            {
                Title = "Invalid duration",
                SubTitle = "Try a number (hours) or number + m (minutes). Example: 3 or 45m",
                Action = c => false,
                IcoPath = _iconPath
            });
        }

        return results;
    }

    /// <summary>
    /// Parse user input into a TimeSpan. Returns null if input is invalid.
    /// Supports: "45m" (minutes), "3" or "3h" (hours), "1.5" (1.5 hours = 90 min).
    /// </summary>
    internal static TimeSpan? ParseDuration(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        if (input.EndsWith("m", StringComparison.OrdinalIgnoreCase))
        {
            if (double.TryParse(input.AsSpan(0, input.Length - 1), out var minutes)
                && minutes > 0 && minutes <= 1440)
            {
                var rounded = Math.Max(1, (int)Math.Ceiling(minutes));
                return TimeSpan.FromMinutes(rounded);
            }
            return null;
        }

        var numStr = input.EndsWith("h", StringComparison.OrdinalIgnoreCase)
            ? input.AsSpan(0, input.Length - 1)
            : input.AsSpan();

        if (double.TryParse(numStr, out var hours) && hours > 0 && hours <= 24)
            return TimeSpan.FromHours(hours);

        return null;
    }

    /// <summary>
    /// Format a TimeSpan as a human-readable label. Example: "1 hour", "45 minutes", "2h 30m".
    /// </summary>
    internal static string FormatDurationLabel(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            var hours = (int)duration.TotalHours;
            var minutes = duration.Minutes;
            if (minutes == 0)
                return hours == 1 ? "1 hour" : $"{hours} hours";
            return $"{hours}h {minutes}m";
        }
        var mins = (int)Math.Ceiling(duration.TotalMinutes);
        return mins == 1 ? "1 minute" : $"{mins} minutes";
    }

    private string GetStatusText()
    {
        if (!IsActive) return "Inactive";
        if (_timer == null) return "Indefinite";
        return _timer.FormatRemainingTime();
    }

    /// <summary>
    /// Start caffeine, or change the active duration if already running.
    /// Pass null for indefinite mode.
    /// </summary>
    private void StartCaffeine(TimeSpan? duration = null)
    {
        _activeDuration = duration;

        if (!IsActive)
        {
            PowerUtilities.PreventPowerSave(_settings.KeepDisplayOn);
            IsActive = true;

            if (duration.HasValue)
            {
                _timer = new CaffeineTimer();
                _timer.Expired += OnTimerExpired;
                _timer.Start(duration.Value);
            }

            if (_settings.ShowTrayIcon)
                TrayIconManager.ShowTray(
                    _context,
                    OnDurationSelected,
                    () => StopCaffeine(),
                    GetStatusText,
                    () => _settings.KeepDisplayOn,
                    OnToggleKeepDisplayOn);
            if (_settings.SendNotifications)
            {
                var durationText = duration.HasValue ? $" for {FormatDurationLabel(duration.Value)}" : "";
                _context.API.ShowMsg("Caffeine - Flow Launcher ☕", $"Caffeine is now active{durationText} 🟢", _iconPath);
            }
        }
        else
        {
            if (duration.HasValue)
            {
                if (_timer == null)
                {
                    _timer = new CaffeineTimer();
                    _timer.Expired += OnTimerExpired;
                }
                _timer.Start(duration.Value);
            }
            else
            {
                _timer?.Dispose();
                _timer = null;
            }
            TrayIconManager.UpdateStatus();
            if (_settings.SendNotifications)
            {
                var switchText = duration.HasValue ? FormatDurationLabel(duration.Value) : "indefinite";
                _context.API.ShowMsg("Caffeine - Flow Launcher ☕", $"Switched to {switchText} 🔄", _iconPath);
            }
        }
    }

    /// <summary>
    /// Stop caffeine and clean up timer.
    /// </summary>
    private void StopCaffeine(bool timerExpired = false)
    {
        if (IsActive)
        {
            PowerUtilities.Shutdown();
            IsActive = false;
            _activeDuration = null;

            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }

            if (_settings.ShowTrayIcon) TrayIconManager.HideTray();
            if (_settings.SendNotifications)
            {
                var message = timerExpired
                    ? "Caffeine timer expired — system can now sleep ⏰"
                    : "Caffeine is now inactive 🔴";
                _context.API.ShowMsg("Caffeine - Flow Launcher ☕", message, _iconPath);
            }
        }
    }

    private void OnTimerExpired(object sender, EventArgs e)
    {
        StopCaffeine(timerExpired: true);
    }

    private void OnDurationSelected(TimeSpan? duration)
    {
        StartCaffeine(duration);
    }

    private void OnToggleKeepDisplayOn(bool keepDisplayOn)
    {
        _settings.KeepDisplayOn = keepDisplayOn;
        _context.API.SaveSettingJsonStorage<Settings.Settings>();
        UpdatePowerSettings();
    }

    /// <summary>
    /// Called by PluginSettings when the Show Tray Icon checkbox changes while caffeine is active.
    /// </summary>
    internal void ShowTrayIfActive()
    {
        if (IsActive && _settings.ShowTrayIcon)
            TrayIconManager.ShowTray(
                _context,
                OnDurationSelected,
                () => StopCaffeine(),
                GetStatusText,
                () => _settings.KeepDisplayOn,
                OnToggleKeepDisplayOn);
    }

    /// <summary>
    /// Called by PluginSettings when power options change while caffeine is active.
    /// </summary>
    internal void UpdatePowerSettings()
    {
        if (IsActive)
            PowerUtilities.PreventPowerSave(_settings.KeepDisplayOn);
    }

    /// <summary>
    /// Create the settings panel for the plugin
    /// </summary>
    public System.Windows.Controls.Control CreateSettingPanel()
    {
        return new PluginSettings(this, _context, _settings);
    }

    /// <summary>
    /// Dispose of resources when the plugin is unloaded
    /// </summary>
    public void Dispose()
    {
        StopCaffeine();
    }
}
