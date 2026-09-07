Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'app\SteamBlock.Common.ps1')

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

Assert-Equal $false (Test-SteamBlockTime -CurrentTime ([datetime]'2026-01-01 22:59:59') -BlockStart '23:00' -BlockEnd '07:00') 'Before overnight block'
Assert-Equal $true  (Test-SteamBlockTime -CurrentTime ([datetime]'2026-01-01 23:00:00') -BlockStart '23:00' -BlockEnd '07:00') 'Overnight start is inclusive'
Assert-Equal $true  (Test-SteamBlockTime -CurrentTime ([datetime]'2026-01-02 00:00:00') -BlockStart '23:00' -BlockEnd '07:00') 'Blocked across midnight'
Assert-Equal $true  (Test-SteamBlockTime -CurrentTime ([datetime]'2026-01-02 06:59:59') -BlockStart '23:00' -BlockEnd '07:00') 'Just before overnight end'
Assert-Equal $false (Test-SteamBlockTime -CurrentTime ([datetime]'2026-01-02 07:00:00') -BlockStart '23:00' -BlockEnd '07:00') 'Overnight end is exclusive'
Assert-Equal $true  (Test-SteamBlockTime -CurrentTime ([datetime]'2026-01-01 12:30:00') -BlockStart '09:00' -BlockEnd '17:00') 'Same-day blocked interval'
Assert-Equal $false (Test-SteamBlockTime -CurrentTime ([datetime]'2026-01-01 18:00:00') -BlockStart '09:00' -BlockEnd '17:00') 'After same-day interval'

Assert-Equal $true  (Test-PathInsideDirectory -Path 'D:\Games\Example\bin\game.exe' -Directory 'D:\Games\Example') 'Executable inside directory'
Assert-Equal $false (Test-PathInsideDirectory -Path 'D:\Games\ExampleOther\game.exe' -Directory 'D:\Games\Example') 'Directory boundary is respected'
Assert-Equal $true  (Test-PathInsideDirectory -Path 'd:\games\example\GAME.EXE' -Directory 'D:\Games\Example') 'Paths are case insensitive'

$sameTimeRejected = $false
try { [void](Test-SteamBlockTime -CurrentTime (Get-Date) -BlockStart '08:00' -BlockEnd '08:00') } catch { $sameTimeRejected = $true }
Assert-Equal $true $sameTimeRejected 'Equal start and end is rejected'

$badFormatRejected = $false
try { [void](ConvertTo-SteamBlockTime '25:00') } catch { $badFormatRejected = $true }
Assert-Equal $true $badFormatRejected 'Invalid time is rejected'

$badSidRejected = $false
$badConfigPath = Join-Path $env:TEMP ('steam-block-test-{0}.json' -f [guid]::NewGuid().ToString('N'))
try {
    [pscustomobject]@{
        BlockStart = '23:00'
        BlockEnd = '07:00'
        CheckIntervalSeconds = 2
        TargetUserSid = 'not-a-sid'
        SteamPath = 'C:\Steam'
    } | ConvertTo-Json | Set-Content -LiteralPath $badConfigPath -Encoding UTF8
    [void](Get-SteamBlockConfig -Path $badConfigPath)
} catch {
    $badSidRejected = $true
} finally {
    Remove-Item -LiteralPath $badConfigPath -Force -ErrorAction SilentlyContinue
}
Assert-Equal $true $badSidRejected 'Invalid target SID is rejected'

if ($failures -gt 0) { throw "$failures test(s) failed." }
Write-Host 'All Steam Block tests passed.' -ForegroundColor Cyan
