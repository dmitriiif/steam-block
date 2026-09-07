[CmdletBinding()]
param(
    [string]$InstallPath = 'C:\ProgramData\WindowsAppBlocker',
    [switch]$RemoveFiles,
    [int]$WaitForProcessId = 0
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this uninstaller as administrator.'
}

$task = Get-ScheduledTask -TaskName 'WindowsAppBlocker' -ErrorAction SilentlyContinue
if ($task) {
    Stop-ScheduledTask -TaskName 'WindowsAppBlocker' -ErrorAction SilentlyContinue
    Unregister-ScheduledTask -TaskName 'WindowsAppBlocker' -Confirm:$false
}

$shortcutPath = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\Windows App Blocker.lnk'
if (Test-Path -LiteralPath $shortcutPath) { Remove-Item -LiteralPath $shortcutPath -Force }
$desktopShortcutPath = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Windows App Blocker.lnk'
if (Test-Path -LiteralPath $desktopShortcutPath) { Remove-Item -LiteralPath $desktopShortcutPath -Force }

if ($RemoveFiles) {
    $expectedPath = [IO.Path]::GetFullPath('C:\ProgramData\WindowsAppBlocker').TrimEnd('\')
    $requestedPath = [IO.Path]::GetFullPath($InstallPath).TrimEnd('\')
    if (-not $requestedPath.Equals($expectedPath, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unexpected directory: $requestedPath"
    }
    if ($WaitForProcessId -gt 0) {
        $escapedPath = $requestedPath.Replace("'", "''")
        $cleanupCommand = @"
try { Wait-Process -Id $WaitForProcessId -ErrorAction SilentlyContinue } catch { }
Start-Sleep -Milliseconds 500
for (`$attempt = 0; `$attempt -lt 10; `$attempt++) {
    if (-not (Test-Path -LiteralPath '$escapedPath')) { break }
    Remove-Item -LiteralPath '$escapedPath' -Recurse -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
}
"@
        $encodedCommand = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($cleanupCommand))
        Start-Process `
            -FilePath "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" `
            -ArgumentList "-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand $encodedCommand" `
            -WorkingDirectory $env:TEMP `
            -WindowStyle Hidden | Out-Null
    } else {
        Remove-Item -LiteralPath $requestedPath -Recurse -Force
    }
}

Write-Output 'Windows App Blocker has been disabled and removed from Task Scheduler.'
