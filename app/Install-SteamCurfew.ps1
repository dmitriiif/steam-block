[CmdletBinding()]
param(
    [string]$InstallPath = 'C:\ProgramData\SteamCurfew',
    [string]$TargetUserSid,
    [string]$SteamPath,
    [switch]$RefreshGames,
    [switch]$NoStart
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'SteamBlock.Common.ps1')

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdministrator)) {
    throw 'Run this installer as administrator, or open Steam Block.exe and approve its Windows prompt.'
}

$taskName = 'SteamCurfew'
$configPath = Join-Path $InstallPath 'config.json'
$isNewInstall = -not (Test-Path -LiteralPath $configPath -PathType Leaf)

if (-not $TargetUserSid) {
    $TargetUserSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
}

if ($isNewInstall) {
    $resolvedSteamPath = Get-SteamInstallPath -Override $SteamPath
    $config = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'config.json') -Raw | ConvertFrom-Json
    $config.TargetUserSid = $TargetUserSid
    $config.SteamPath = $resolvedSteamPath
    $config.GameDirectories = @(Get-SteamGameDirectories -LibraryPaths (Get-SteamLibraryPaths -SteamPath $resolvedSteamPath))
} else {
    $config = Get-SteamBlockConfig -Path $configPath
    if ($PSBoundParameters.ContainsKey('TargetUserSid')) { $config.TargetUserSid = $TargetUserSid }
    if ($PSBoundParameters.ContainsKey('SteamPath')) {
        $resolvedSteamPath = Get-SteamInstallPath -Override $SteamPath
        $config.SteamPath = $resolvedSteamPath
    } else {
        $resolvedSteamPath = Get-SteamInstallPath -Override ([string]$config.SteamPath)
    }
    if ($RefreshGames) {
        $config.GameDirectories = @(Get-SteamGameDirectories -LibraryPaths (Get-SteamLibraryPaths -SteamPath $resolvedSteamPath))
    }
}

New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null

$runtimeFiles = @(
    'SteamBlock.Common.ps1',
    'SteamCurfew.ps1',
    'Install-SteamCurfew.ps1',
    'Uninstall-SteamCurfew.ps1'
)
foreach ($file in $runtimeFiles) {
    $source = Join-Path $PSScriptRoot $file
    $destination = Join-Path $InstallPath $file
    if ((ConvertTo-NormalizedPath $source) -ne (ConvertTo-NormalizedPath $destination)) {
        Copy-Item -LiteralPath $source -Destination $destination -Force
    }
}

$guiSource = @(
    (Join-Path $PSScriptRoot 'Steam Block.exe'),
    (Join-Path (Split-Path -Parent $PSScriptRoot) 'Steam Block.exe')
) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if (-not $guiSource) { throw 'Steam Block.exe was not found beside the app folder.' }
$guiDestination = Join-Path $InstallPath 'Steam Block.exe'
if ((ConvertTo-NormalizedPath $guiSource) -ne (ConvertTo-NormalizedPath $guiDestination)) {
    Copy-Item -LiteralPath $guiSource -Destination $guiDestination -Force
}
$oldGuiPath = Join-Path $InstallPath 'SteamBlock-GUI.ps1'
if (Test-Path -LiteralPath $oldGuiPath) { Remove-Item -LiteralPath $oldGuiPath -Force }
Save-SteamBlockConfig -Config $config -Path $configPath

# Keep SYSTEM and administrators in control of executable files. Other users may read and run them.
& "$env:SystemRoot\System32\icacls.exe" $InstallPath '/inheritance:r' '/grant:r' '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' '*S-1-5-32-545:(OI)(CI)RX' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not secure the installation directory.' }

$powerShell = "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"
$monitorPath = Join-Path $InstallPath 'SteamCurfew.ps1'
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
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $triggers -Settings $settings -Principal $principal -Description 'Blocks Steam and installed Steam games during configured hours.' -Force | Out-Null

$programsDirectory = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs'
$shortcutPath = Join-Path $programsDirectory 'Steam Block.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $guiDestination
$shortcut.Arguments = ''
$shortcut.WorkingDirectory = $InstallPath
$shortcut.IconLocation = $guiDestination
$shortcut.Save()

if ($NoStart) {
    Disable-ScheduledTask -TaskName $taskName | Out-Null
} else {
    Start-ScheduledTask -TaskName $taskName
}

Write-Output ("Steam Block installed. Monitoring {0} Steam game librar{1}." -f @($config.GameDirectories).Count, $(if (@($config.GameDirectories).Count -eq 1) { 'y' } else { 'ies' }))
Write-Output 'Open "Steam Block" from the Start menu to change its schedule or enable/disable it.'
