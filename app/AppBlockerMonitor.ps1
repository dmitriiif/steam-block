[CmdletBinding()]
param(
    [string]$ConfigPath = 'C:\ProgramData\WindowsAppBlocker\config.json',
    [switch]$DryRun,
    [switch]$Once,
    [datetime]$AtTime = [datetime]::MinValue
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'AppBlocker.Common.ps1')

$script:LogPath = Join-Path (Split-Path -Parent $ConfigPath) 'WindowsAppBlocker.log'
$script:RecentMessages = @{}

function Write-AppBlockerLog {
    param([string]$Message, [string]$Level = 'INFO')

    $key = "$Level|$Message"
    $now = Get-Date
    if ($script:RecentMessages.ContainsKey($key) -and (($now - $script:RecentMessages[$key]).TotalMinutes -lt 5)) { return }
    $script:RecentMessages[$key] = $now

    if ((Test-Path -LiteralPath $script:LogPath) -and (Get-Item -LiteralPath $script:LogPath).Length -gt 1MB) {
        for ($index = 2; $index -ge 1; $index--) {
            $source = "$($script:LogPath).$index"
            $destination = "$($script:LogPath).$($index + 1)"
            if (Test-Path -LiteralPath $source) { Move-Item -LiteralPath $source -Destination $destination -Force }
        }
        Move-Item -LiteralPath $script:LogPath -Destination "$($script:LogPath).1" -Force
    }

    Add-Content -LiteralPath $script:LogPath -Value ('{0:u} [{1}] {2}' -f $now, $Level, $Message) -Encoding UTF8
    if ($DryRun) { Write-Output ('[{0}] {1}' -f $Level, $Message) }
}

function Get-ProcessOwnerSid {
    param([Parameter(Mandatory = $true)]$Process)
    try {
        $owner = Invoke-CimMethod -InputObject $Process -MethodName GetOwnerSid -ErrorAction Stop
        if ($owner.ReturnValue -eq 0) { return [string]$owner.Sid }
    } catch { }
    return $null
}

function Test-ConfiguredProcess {
    param($Process, $Config)
    if (-not $Process.ExecutablePath) { return $false }
    return Test-ConfiguredExecutablePath -ProcessPath ([string]$Process.ExecutablePath) -Executables @($Config.Executables)
}

function Invoke-AppBlockerCheck {
    param($Config, [datetime]$CurrentTime)

    if (-not (Test-AppBlockerTime -CurrentTime $CurrentTime -BlockStart $Config.BlockStart -BlockEnd $Config.BlockEnd)) { return }

    $processes = Get-CimInstance -ClassName Win32_Process -ErrorAction Stop
    foreach ($process in $processes) {
        if (-not (Test-ConfiguredProcess -Process $process -Config $Config)) { continue }
        if ((Get-ProcessOwnerSid -Process $process) -ne [string]$Config.TargetUserSid) { continue }

        try {
            # Re-read and re-check the process to avoid terminating a PID that Windows has already reused.
            $current = Get-CimInstance -ClassName Win32_Process -Filter ("ProcessId = {0}" -f $process.ProcessId) -ErrorAction Stop
            if (-not $current -or $current.CreationDate -ne $process.CreationDate) { continue }
            if (-not (Test-ConfiguredProcess -Process $current -Config $Config)) { continue }
            if ((Get-ProcessOwnerSid -Process $current) -ne [string]$Config.TargetUserSid) { continue }

            if ($DryRun) {
                Write-AppBlockerLog -Message ("Would stop {0} (PID {1})" -f $current.Name, $current.ProcessId)
            } else {
                Stop-Process -Id $current.ProcessId -Force -ErrorAction Stop
                Write-AppBlockerLog -Message ("Stopped {0} (PID {1})" -f $current.Name, $current.ProcessId)
            }
        } catch [Microsoft.PowerShell.Commands.ProcessCommandException] {
            # The process exited between inspection and termination.
        } catch {
            Write-AppBlockerLog -Level 'ERROR' -Message ("Could not stop {0} (PID {1}): {2}" -f $process.Name, $process.ProcessId, $_.Exception.Message)
        }
    }
}

$mutex = $null
$ownsMutex = $false
try {
    $createdNew = $false
    $mutex = New-Object Threading.Mutex($true, 'Global\WindowsAppBlockerMonitor', [ref]$createdNew)
    if (-not $createdNew) { exit 0 }
    $ownsMutex = $true

    do {
        $config = Get-AppBlockerConfig -Path $ConfigPath
        $currentTime = if ($AtTime -ne [datetime]::MinValue) { $AtTime } else { Get-Date }
        Invoke-AppBlockerCheck -Config $config -CurrentTime $currentTime
        if ($Once) { break }
        Start-Sleep -Seconds ([int]$config.CheckIntervalSeconds)
    } while ($true)
} catch {
    try { Write-AppBlockerLog -Level 'FATAL' -Message $_.Exception.Message } catch { }
    Write-Error $_
    exit 1
} finally {
    if ($mutex) {
        if ($ownsMutex) { $mutex.ReleaseMutex() }
        $mutex.Dispose()
    }
}
