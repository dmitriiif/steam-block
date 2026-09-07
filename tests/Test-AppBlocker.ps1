Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'app\AppBlocker.Common.ps1')

$failures = 0
function Assert-Equal {
    param($Expected, $Actual, [string]$Name)
    if ($Expected -ne $Actual) {
        Write-Host "FAIL: $Name (expected $Expected, got $Actual)" -ForegroundColor Red
        $script:failures++
    } else {
        Write-Host "PASS: $Name" -ForegroundColor Green
    }
}

Assert-Equal $false (Test-AppBlockerTime -CurrentTime ([datetime]'2026-01-01 22:59:59') -BlockStart '23:00' -BlockEnd '07:00') 'Before overnight block'
Assert-Equal $true  (Test-AppBlockerTime -CurrentTime ([datetime]'2026-01-01 23:00:00') -BlockStart '23:00' -BlockEnd '07:00') 'Overnight start is inclusive'
Assert-Equal $true  (Test-AppBlockerTime -CurrentTime ([datetime]'2026-01-02 00:00:00') -BlockStart '23:00' -BlockEnd '07:00') 'Blocked across midnight'
Assert-Equal $true  (Test-AppBlockerTime -CurrentTime ([datetime]'2026-01-02 06:59:59') -BlockStart '23:00' -BlockEnd '07:00') 'Just before overnight end'
Assert-Equal $false (Test-AppBlockerTime -CurrentTime ([datetime]'2026-01-02 07:00:00') -BlockStart '23:00' -BlockEnd '07:00') 'Overnight end is exclusive'
Assert-Equal $true  (Test-AppBlockerTime -CurrentTime ([datetime]'2026-01-01 12:30:00') -BlockStart '09:00' -BlockEnd '17:00') 'Same-day blocked interval'
Assert-Equal $false (Test-AppBlockerTime -CurrentTime ([datetime]'2026-01-01 18:00:00') -BlockStart '09:00' -BlockEnd '17:00') 'After same-day interval'

$targets = @('C:\Program Files\Example\Example.exe', 'D:\Tools\Editor.exe')
Assert-Equal $true  (Test-ConfiguredExecutablePath -ProcessPath 'c:\program files\example\EXAMPLE.EXE' -Executables $targets) 'Executable paths are case insensitive'
Assert-Equal $true  (Test-ConfiguredExecutablePath -ProcessPath 'D:\Tools\Editor.exe' -Executables $targets) 'Second configured executable matches'
Assert-Equal $false (Test-ConfiguredExecutablePath -ProcessPath 'C:\Program Files\Example\Helper.exe' -Executables $targets) 'Different executable in same directory does not match'
Assert-Equal $false (Test-ConfiguredExecutablePath -ProcessPath 'C:\Program Files\ExampleOther\Example.exe' -Executables $targets) 'Similar directory does not match'
Assert-Equal $false (Test-ConfiguredExecutablePath -ProcessPath $null -Executables $targets) 'Missing process path does not match'

$sameTimeRejected = $false
try { [void](Test-AppBlockerTime -CurrentTime (Get-Date) -BlockStart '08:00' -BlockEnd '08:00') } catch { $sameTimeRejected = $true }
Assert-Equal $true $sameTimeRejected 'Equal start and end is rejected'

$badFormatRejected = $false
try { [void](ConvertTo-AppBlockerTime '25:00') } catch { $badFormatRejected = $true }
Assert-Equal $true $badFormatRejected 'Invalid time is rejected'

$testConfigPath = Join-Path $env:TEMP ('app-blocker-test-{0}.json' -f [guid]::NewGuid().ToString('N'))
try {
    [pscustomobject]@{
        BlockStart = '23:00'
        BlockEnd = '07:00'
        CheckIntervalSeconds = 2
        TargetUserSid = 'S-1-5-21-1000-1000-1000-1000'
        Executables = $targets
    } | ConvertTo-Json | Set-Content -LiteralPath $testConfigPath -Encoding UTF8
    $loaded = Get-AppBlockerConfig -Path $testConfigPath
    Assert-Equal 2 @($loaded.Executables).Count 'Valid executable list loads'
} finally {
    Remove-Item -LiteralPath $testConfigPath -Force -ErrorAction SilentlyContinue
}

$badSidRejected = $false
try {
    [pscustomobject]@{
        BlockStart = '23:00'
        BlockEnd = '07:00'
        CheckIntervalSeconds = 2
        TargetUserSid = 'not-a-sid'
        Executables = @()
    } | ConvertTo-Json | Set-Content -LiteralPath $testConfigPath -Encoding UTF8
    [void](Get-AppBlockerConfig -Path $testConfigPath)
} catch {
    $badSidRejected = $true
} finally {
    Remove-Item -LiteralPath $testConfigPath -Force -ErrorAction SilentlyContinue
}
Assert-Equal $true $badSidRejected 'Invalid target SID is rejected'

$badExecutableRejected = $false
try {
    [pscustomobject]@{
        BlockStart = '23:00'
        BlockEnd = '07:00'
        CheckIntervalSeconds = 2
        TargetUserSid = 'S-1-5-21-1000-1000-1000-1000'
        Executables = @('relative-program.exe')
    } | ConvertTo-Json | Set-Content -LiteralPath $testConfigPath -Encoding UTF8
    [void](Get-AppBlockerConfig -Path $testConfigPath)
} catch {
    $badExecutableRejected = $true
} finally {
    Remove-Item -LiteralPath $testConfigPath -Force -ErrorAction SilentlyContinue
}
Assert-Equal $true $badExecutableRejected 'Relative executable path is rejected'

if ($failures -gt 0) { throw "$failures test(s) failed." }
Write-Host 'All Windows App Blocker tests passed.' -ForegroundColor Cyan
