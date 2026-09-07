# Steam Block

Steam Block is a small Windows 11 app that closes Steam and Steam games during hours you choose.

## Start here

1. Download and extract the complete project folder.
2. Double-click **Steam Block.exe** in the main folder.
3. Approve the Windows administrator prompt.
4. Select **Install Steam Block**.
5. Choose the hours you want and select **Turn protection ON**.

After setup, you can also find **Steam Block** in the Windows Start menu. You can close the control panel after making a change; protection continues quietly in the background.

The app starts with protection off, so it cannot unexpectedly close Steam during setup. Its default hours are 23:00 until 07:00.

## What the colors mean

- A large green **PROTECTION IS ON** card means the blocker is running.
- A large red **PROTECTION IS OFF** card means Steam is allowed at all times.
- When protection is on, the card also says whether Steam is blocked at the current moment.

The **Save times** button changes the schedule without changing whether protection is on. The larger button turns protection on or off and also saves the selected times.

## How it works

The control panel is a native C# Windows app, so opening it does not create a PowerShell console window. Windows will show an administrator prompt because changing a protected background task requires administrator access.

The blocker itself is a small PowerShell monitor started invisibly by Windows Task Scheduler. It checks every two seconds only during the blocked period. It starts automatically with Windows and does not show a window.

The installer detects Steam and all configured Steam library folders. It watches each library's `steamapps\common` folder, so games installed later in an existing library are covered automatically. If you add an entirely new Steam library on another drive, run the installer from `app\Install-SteamCurfew.ps1` with `-RefreshGames` or reinstall the app.

Steam Block only closes matching processes owned by the Windows user who installed it. It leaves the Steam system service alone. Games are force-closed, so save progress before the blocking time.

## Files

- `Steam Block.exe` is the only file you need to open.
- `app` contains the application source and background scripts.
- `README.md` is this guide.
- `tests` contains safe automated checks.

Installed runtime files are protected under `C:\ProgramData\SteamCurfew`. The scheduled task is named `SteamCurfew`.

## Testing

The automated tests do not close programs or alter Task Scheduler:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Test-SteamBlock.ps1
```

For a practical test, save any open game, choose a short period containing the current time, and turn protection on. Steam should close and should be closed again within about two seconds if reopened.

## Uninstall

Open Steam Block and select **Uninstall Steam Block**. The app shows a warning before it turns protection off and completely removes the scheduled task, Start menu entry, settings, logs, and installed application files.

Steam Block is a self-control aid. A Windows administrator can disable or uninstall it.

## Building the launcher

Windows 11 includes the .NET Framework compiler used by the build script:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\app\Build.ps1
```

This recreates `Steam Block.exe` as an optimized Windows executable with no console window.
