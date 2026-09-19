Caffeine for Flow Launcher
==================
A plugin for [Flow launcher](https://github.com/Flow-Launcher/Flow.Launcher) that prevents your pc from sleeping or turning off the monitor.  
This is a replacement for [Caffeine](https://www.zhornsoftware.co.uk/caffeine/) that simply keeps your pc awake and screen on when active.  

Icons from [icons8](https://icons8.com/).  
Thanks for that one guy on stackexchange that had a nice clean example of how power management works.

### Usage

Type `caf` into [Flow Launcher](https://github.com/Flow-Launcher/Flow.Launcher) to choose how long to keep your PC awake. Press enter to activate — the first option (indefinitely) works just like the original toggle.

![caffeine presets](Images/readme/caff-presets.png)

You can also type a custom duration directly:

| Command | Effect |
|---------|--------|
| `caf` | Show duration presets (indefinite, 30m, 1h, 2h, 8h) |
| `caf 3` | Keep awake for 3 hours |
| `caf 45m` | Keep awake for 45 minutes |
| `caf 1.5` | Keep awake for 1.5 hours (90 minutes) |
| `caf off` | Turn off caffeine |

When caffeine is already active, typing `caf` shows a "Turn off" option at the top along with options to switch to a different duration. The current mode is hidden from the list since you're already using it.

### Tray Icon

Right-click the tray icon for quick access to toggle keeping the screen active, choose duration presets, or turn off. The remaining time is shown at the top of the menu and in the tooltip on hover.

![caffeine tray](Images/readme/caff-tray.png)

### Settings

![caffeine settings](Images/readme/caff-settings.png)

> **Note:**  
> The image above show the default settings for the plugin.

## Installation

1. Install [flow launcher](https://github.com/Flow-Launcher/Flow.Launcher) if you haven't already.
2. Execute the following command in [flow launcher](https://github.com/Flow-Launcher/Flow.Launcher) query to install the plugin.

```cmd
pm install Caffeine by o850cHQk
```

or

Search for `Caffeine` within [flow launchers](https://github.com/Flow-Launcher/Flow.Launcher) plugin store
