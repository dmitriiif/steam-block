# Steam Block

Steam Block is a small Windows 11 utility that closes Steam and installed Steam games during hours you choose. A lightweight PowerShell monitor runs through Windows Task Scheduler, while a compact GUI lets you change the schedule or turn blocking on and off.

The default schedule blocks Steam from 23:00 until 07:00. The start time is included and the end time is excluded. Schedules that cross midnight are supported.

## Install and open the GUI

1. Double-click `Open-SteamBlock.cmd`.
2. Approve the Windows administrator prompt.
3. Select **Install / repair background task**.
4. Choose the start and end times, select **Save time**, then select **Enable**.

After installation, open **Steam Block** from the Windows Start menu. The GUI does not need to remain open. Installation stores protected runtime files in `C:\ProgramData\SteamCurfew` and creates a scheduled task named `SteamCurfew`.

The installer discovers Steam from the registry, reads all configured Steam libraries, and records the directories of currently installed games. Use **Install / repair background task** again after adding games or Steam libraries so the list is refreshed.

## What gets closed

During blocked hours the monitor checks every two seconds and closes:

- Steam client processes installed beneath the detected Steam directory;
- games found through Steam's `appmanifest_*.acf` files;
- exact executable paths optionally added to `ExtraExecutables` in the installed `config.json`.

The monitor only targets processes owned by the Windows account recorded during installation. It does not stop the Steam system service. Closing a game forcibly can lose unsaved progress.

## Configuration

The GUI edits `BlockStart` and `BlockEnd`. Advanced settings live in `C:\ProgramData\SteamCurfew\config.json`:

- `CheckIntervalSeconds`: polling interval from 1 to 60 seconds;
- `SteamProcessNames`: Steam executables to recognize beneath the Steam directory;
- `GameDirectories`: game directories whose executables should be stopped;
- `ExtraExecutables`: exact paths for additional launchers or games.

Run the GUI as administrator when editing the protected installed configuration. Configuration writes are validated and replaced atomically. The background monitor reloads the file on every check, so saved schedule changes take effect without a reboot.

If Steam is installed somewhere the installer cannot discover, run this from an elevated PowerShell window:

```powershell
.\Install-SteamCurfew.ps1 -SteamPath 'D:\Apps\Steam'
```

## Dry run and logs

Dry run lists matching processes without stopping them:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\SteamCurfew.ps1 -ConfigPath 'C:\ProgramData\SteamCurfew\config.json' -DryRun -Once -AtTime '2026-01-01 23:30:00'
```

Events and actionable errors are written to `C:\ProgramData\SteamCurfew\SteamCurfew.log`. The log rotates at approximately 1 MB and retains three older files. Repeated identical messages are suppressed for five minutes while the monitor is running.

## Test

The automated tests validate overnight and same-day schedules, boundary times, invalid values, and safe path matching. They do not terminate processes or alter Task Scheduler.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Test-SteamBlock.ps1
```

Before relying on the blocker, temporarily choose a block interval containing the current time. Save game progress, enable Steam Block, and confirm that Steam and one installed game close and cannot stay open. Also test after restarting Windows and after sleep/resume.

## Uninstall

Disable the blocker in the GUI first. Then run the following from an elevated PowerShell window:

```powershell
.\Uninstall-SteamCurfew.ps1 -RemoveFiles
```

Without `-RemoveFiles`, the scheduled task and Start menu shortcut are removed while the installed configuration and logs remain in place.

This utility is a self-control aid. A Windows administrator can disable or uninstall it.
