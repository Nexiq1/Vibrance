# Vibrance

A small Windows app that boosts **vibrance** (color saturation) while you play and restores normal colors when you're back on the desktop — like VibranceGUI, but for any graphics card, with a separate level per monitor and a live preview.

## Running it

Start `dist\Vibrance.exe`. It needs the .NET Desktop Runtime 8 or newer (if it's missing, Windows shows a download link).

## Supported graphics cards

| Card | How colors are changed | Monitors |
| --- | --- | --- |
| NVIDIA GeForce | Digital Vibrance in the driver (same as the NVIDIA Control Panel) | each one separately |
| AMD Radeon | Saturation in the driver (same as "Saturation" in Radeon Software) | each one separately |
| Intel, laptops, others | Windows color filter | all at once |

The app picks the method on its own. If the driver can't change vibrance (e.g. a laptop whose screen is wired to the Intel GPU), it uses the Windows filter right away. You can also turn the filter on manually with **Windows filter instead of the driver**.

The Windows filter:
- covers all monitors at once, so "only change the monitor the game is on" is disabled while it's in use;
- may not show up in games running in exclusive fullscreen — switch the game to **borderless / windowed fullscreen**;
- makes Windows compose the screen through DWM while it's active, which can add a tiny bit of latency. At 50% on the desktop it's switched off completely.

Levels are percentages like in the NVIDIA Control Panel: **50% = normal colors, 100% = maximum**.

## How to use it

- **Desktop** – the vibrance level for each monitor while you're not gaming. Pick the monitor with the tabs above the slider.
- **Games** – `+ Add game` → *From open windows* (start the game and pick its window), *Choose an .exe file*, or *Popular games*. Select a game and set its slider; while you drag it you see a preview on the monitor that shows the Vibrance window.
- When a game from the list is in the foreground, vibrance switches automatically. After Alt+Tab the desktop level comes back.
- Double-click or F2 on a game → rename. Delete → remove.
- Closing the window hides the app in the tray (the color-wheel icon next to the clock). To quit completely: right-click the icon → **Exit**. On exit, vibrance goes back to the desktop level.

### Options

| Option | What it does |
| --- | --- |
| Enabled | Turns automatic switching on/off (also available in the tray icon's menu). |
| Start with Windows | Starts after you sign in, straight into the tray. |
| Only change the monitor the game is on | Your second monitor (Discord, browser) stays at its desktop level. |
| Keep vibrance while the game runs in the background | Doesn't go back to the desktop level after Alt+Tab while the game is still running. |
| Windows filter instead of the driver | Uses the Windows color filter even on an NVIDIA/AMD card. |

### Good to know

- If you change vibrance in the NVIDIA App / NVIDIA Control Panel, or saturation in Radeon Software, while you're on the desktop, Vibrance adopts it as the new desktop level for that monitor.
- The app doesn't inject into games or read their memory. It only checks which program is in the foreground and changes the driver setting or the Windows color filter.
- When the app closes (even if it crashes), the Windows filter disappears with it.
- Settings: `%APPDATA%\Vibrance\settings.json`. If a `settings.json` file sits next to `Vibrance.exe`, the app uses that one instead (portable mode, e.g. on a USB stick).

## Building from source

You need the .NET SDK 8 or newer. Run `build.cmd` — the finished file ends up in `dist\Vibrance.exe`.

Code: `src\Core` (NVIDIA NvAPI, AMD ADL, Windows filter, game detection, settings), `src\UI` (dark-themed controls), `src\MainForm.cs` (main window).
