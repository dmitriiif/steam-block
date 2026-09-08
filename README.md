# Windows App Blocker

Windows App Blocker is a small Windows 11 utility that closes apps you choose during a daily time window.

## Start here

1. Download and extract the complete project folder.
2. Double-click **Windows App Blocker.exe** in the main folder.
3. Approve the Windows administrator prompt.
4. Complete the one-time setup: choose the same hours every day, separate weekday/weekend hours, or individual hours for all seven days. Then select the apps to block and decide which controls remain available later.
5. Choose whether to add Start menu and desktop shortcuts, then select **Install and finish setup**.
6. Turn protection on from the control panel.

You can then open **Windows App Blocker** from the Windows Start menu. Closing the control panel does not stop protection.

Protection starts off, so setup cannot unexpectedly close an app. The default block window is 23:00–07:00. Overnight windows work as expected: a 23:00–07:00 schedule blocks apps from 23:00 through 06:59. With a weekday/weekend schedule, an overnight period finishes using the schedule of the day on which it started.

During setup, changing hours, turning protection off, removing apps from the block list, and uninstalling can each be set to **Yes — anytime**, **No — never**, or **Only during allowed hours**. “Allowed hours” are times when the configured schedule permits the blocked apps to run. The turn-off policy never prevents turning protection on. These setup policies are fixed in the app; locked controls are disabled in the control panel, and permanent **No — never** choices require confirmation.

## How it works

The app installs a small, hidden PowerShell monitor through Windows Task Scheduler. During the configured window, it checks running processes every two seconds and force-closes any whose full executable path exactly matches an entry in your list.

Matching the complete path matters: selecting `C:\Apps\Example.exe` will not block another `Example.exe` elsewhere. Only processes owned by the Windows account that installed the blocker are affected.

Windows requests administrator access because the app installs and controls a protected background task. Installed files live under `C:\ProgramData\WindowsAppBlocker`, and the scheduled task is named `WindowsAppBlocker`.

> Apps are force-closed. Save your work before a blocking window begins. Windows App Blocker is a self-control aid, not security software; a Windows administrator can still bypass it outside the app.

## Files

- `Windows App Blocker.exe` is the launcher and control panel.
- `app` contains the source, installer, uninstaller, and background monitor.
- `tests` contains safe automated checks that do not close programs or change Task Scheduler.

## Testing

Run the safe test suite:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Test-AppBlocker.ps1
```

For a practical test, choose a harmless app such as Notepad, set a short window containing the current time, and turn protection on. The selected app should close and be closed again within about two seconds if reopened.

## Uninstall

If uninstalling was allowed during setup, open Windows App Blocker and select **Uninstall**. For **Only during allowed hours**, the button becomes available outside the blocking window. After confirmation, it turns protection off and removes the scheduled task, shortcuts, settings, logs, and installed application files.

## Building the launcher

Windows 11 includes the .NET Framework compiler used by the build script:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\app\Build.ps1
```

This recreates `Windows App Blocker.exe`.

## License

Windows App Blocker is available under the [MIT License](LICENSE).
