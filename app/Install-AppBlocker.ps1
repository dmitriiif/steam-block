[CmdletBinding()]
param(
    [string]$InstallPath = 'C:\ProgramData\WindowsAppBlocker',
    [string]$TargetUserSid,
    [switch]$SkipStartMenuShortcut,
    [switch]$AddDesktopShortcut,
    [switch]$NoStart
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'AppBlocker.Common.ps1')

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdministrator)) {
    throw 'Run this installer as administrator, or open Windows App Blocker.exe and approve its Windows prompt.'
}

$taskName = 'WindowsAppBlocker'
$configPath = Join-Path $InstallPath 'config.json'
$isNewInstall = -not (Test-Path -LiteralPath $configPath -PathType Leaf)
if (-not $TargetUserSid) { $TargetUserSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value }

if ($isNewInstall) {
    $config = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'config.json') -Raw | ConvertFrom-Json
    $config.TargetUserSid = $TargetUserSid
} else {
    $config = Get-AppBlockerConfig -Path $configPath
    if ($PSBoundParameters.ContainsKey('TargetUserSid')) { $config.TargetUserSid = $TargetUserSid }
}

New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
$runtimeFiles = @(
    'AppBlocker.Common.ps1',
    'AppBlockerMonitor.ps1',
    'Install-AppBlocker.ps1',
    'Uninstall-AppBlocker.ps1'
)
foreach ($file in $runtimeFiles) {
    $source = Join-Path $PSScriptRoot $file
    $destination = Join-Path $InstallPath $file
    if ((ConvertTo-NormalizedPath $source) -ne (ConvertTo-NormalizedPath $destination)) {
        Copy-Item -LiteralPath $source -Destination $destination -Force
    }
}

$guiSource = @(
    (Join-Path $PSScriptRoot 'Windows App Blocker.exe'),
    (Join-Path (Split-Path -Parent $PSScriptRoot) 'Windows App Blocker.exe')
) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if (-not $guiSource) { throw 'Windows App Blocker.exe was not found beside the app folder.' }
$guiDestination = Join-Path $InstallPath 'Windows App Blocker.exe'
if ((ConvertTo-NormalizedPath $guiSource) -ne (ConvertTo-NormalizedPath $guiDestination)) {
    Copy-Item -LiteralPath $guiSource -Destination $guiDestination -Force
}
Save-AppBlockerConfig -Config $config -Path $configPath

# Keep SYSTEM and administrators in control of installed files. Other users may read and run them.
& "$env:SystemRoot\System32\icacls.exe" $InstallPath '/inheritance:r' '/grant:r' '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' '*S-1-5-32-545:(OI)(CI)RX' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not secure the installation directory.' }

$powerShell = "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"
$monitorPath = Join-Path $InstallPath 'AppBlockerMonitor.ps1'
$arguments = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File "{0}" -ConfigPath "{1}"' -f $monitorPath, $configPath
$action = New-ScheduledTaskAction -Execute $powerShell -Argument $arguments
$triggers = @(
    (New-ScheduledTaskTrigger -AtStartup),
    (New-ScheduledTaskTrigger -AtLogOn -User $config.TargetUserSid)
)
$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -Hidden `
    -MultipleInstances IgnoreNew `
    -ExecutionTimeLimit ([timespan]::Zero) `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 1)
$principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest

if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) {
    Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
}
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $triggers -Settings $settings -Principal $principal -Description 'Blocks selected Windows applications during configured hours.' -Force | Out-Null

$programsDirectory = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs'
$shortcutPath = Join-Path $programsDirectory 'Windows App Blocker.lnk'
$shell = New-Object -ComObject WScript.Shell
if ($SkipStartMenuShortcut) {
    if (Test-Path -LiteralPath $shortcutPath) { Remove-Item -LiteralPath $shortcutPath -Force }
} else {
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $guiDestination
    $shortcut.Arguments = ''
    $shortcut.WorkingDirectory = $InstallPath
    $shortcut.IconLocation = $guiDestination
    $shortcut.Save()
}

$desktopShortcutPath = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Windows App Blocker.lnk'
if ($AddDesktopShortcut) {
    $desktopShortcut = $shell.CreateShortcut($desktopShortcutPath)
    $desktopShortcut.TargetPath = $guiDestination
    $desktopShortcut.Arguments = ''
    $desktopShortcut.WorkingDirectory = $InstallPath
    $desktopShortcut.IconLocation = $guiDestination
    $desktopShortcut.Save()
} elseif (Test-Path -LiteralPath $desktopShortcutPath) {
    Remove-Item -LiteralPath $desktopShortcutPath -Force
}

if ($NoStart) { Disable-ScheduledTask -TaskName $taskName | Out-Null }
else { Start-ScheduledTask -TaskName $taskName }

Write-Output ("Windows App Blocker installed with {0} configured app(s)." -f @($config.Executables).Count)
Write-Output 'Open "Windows App Blocker" from the Start menu to choose apps, change the schedule, or enable protection.'
