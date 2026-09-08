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

$splitSchedule = [pscustomobject]@{
    ScheduleMode = 'WeekdayWeekend'
    BlockStart = '23:00'
    BlockEnd = '07:00'
    WeekdayBlockStart = '22:00'
    WeekdayBlockEnd = '06:00'
    WeekendBlockStart = '12:00'
    WeekendBlockEnd = '14:00'
}
Assert-Equal $true  (Test-AppBlockerSchedule -CurrentTime ([datetime]'2026-01-09 23:30:00') -Config $splitSchedule) 'Friday uses weekday start'
Assert-Equal $true  (Test-AppBlockerSchedule -CurrentTime ([datetime]'2026-01-10 05:30:00') -Config $splitSchedule) 'Saturday morning finishes Friday overnight schedule'
Assert-Equal $false (Test-AppBlockerSchedule -CurrentTime ([datetime]'2026-01-10 06:30:00') -Config $splitSchedule) 'Friday overnight end is respected on Saturday'
Assert-Equal $true  (Test-AppBlockerSchedule -CurrentTime ([datetime]'2026-01-10 12:30:00') -Config $splitSchedule) 'Saturday same-day weekend schedule applies'
Assert-Equal $true  (Test-AppBlockerSchedule -CurrentTime ([datetime]'2026-01-11 13:59:59') -Config $splitSchedule) 'Sunday uses weekend schedule'
Assert-Equal $false (Test-AppBlockerSchedule -CurrentTime ([datetime]'2026-01-12 09:00:00') -Config $splitSchedule) 'Monday does not inherit a non-overnight Sunday schedule'

$disabledWeekendSchedule = [pscustomobject]@{
    ScheduleMode = 'WeekdayWeekend'
    BlockStart = '23:00'; BlockEnd = '07:00'
    WeekdayBlockStart = '22:00'; WeekdayBlockEnd = '06:00'; WeekdayEnabled = $true
    WeekendBlockStart = '12:00'; WeekendBlockEnd = '14:00'; WeekendEnabled = $false
}
Assert-Equal $true  (Test-AppBlockerSchedule -CurrentTime ([datetime]'2026-01-10 05:30:00') -Config $disabledWeekendSchedule) 'Disabled weekend still finishes enabled Friday overnight schedule'
Assert-Equal $false (Test-AppBlockerSchedule -CurrentTime ([datetime]'2026-01-10 12:30:00') -Config $disabledWeekendSchedule) 'Disabled weekend starts no blocking period'

$disabledWeekdaySchedule = [pscustomobject]@{
    ScheduleMode = 'WeekdayWeekend'
    BlockStart = '23:00'; BlockEnd = '07:00'
    WeekdayBlockStart = '22:00'; WeekdayBlockEnd = '06:00'; WeekdayEnabled = $false
    WeekendBlockStart = '12:00'; WeekendBlockEnd = '14:00'; WeekendEnabled = $true
}
Assert-Equal $false (Test-AppBlockerSchedule -CurrentTime ([datetime]'2026-01-09 23:30:00') -Config $disabledWeekdaySchedule) 'Disabled weekdays start no blocking period'
Assert-Equal $true  (Test-AppBlockerSchedule -CurrentTime ([datetime]'2026-01-10 12:30:00') -Config $disabledWeekdaySchedule) 'Enabled weekend schedule still applies'

$dailySchedule = [pscustomobject]@{
    ScheduleMode = 'EveryDay'
    BlockStart = '23:00'
    BlockEnd = '07:00'
}
Assert-Equal $true (Test-AppBlockerSchedule -CurrentTime ([datetime]'2026-01-10 03:00:00') -Config $dailySchedule) 'Every-day schedule remains compatible'

$individualSchedule = [pscustomobject]@{
    ScheduleMode = 'IndividualDays'
    BlockStart = '23:00'; BlockEnd = '07:00'
    WeekdayBlockStart = '23:00'; WeekdayBlockEnd = '07:00'
    WeekendBlockStart = '00:00'; WeekendBlockEnd = '09:00'
    MondayBlockStart = '10:00'; MondayBlockEnd = '11:00'
    MondayEnabled = $true
    TuesdayBlockStart = '12:00'; TuesdayBlockEnd = '13:00'
    TuesdayEnabled = $true
    WednesdayBlockStart = '14:00'; WednesdayBlockEnd = '15:00'
    WednesdayEnabled = $false
    ThursdayBlockStart = '16:00'; ThursdayBlockEnd = '17:00'
    ThursdayEnabled = $true
    FridayBlockStart = '22:00'; FridayBlockEnd = '06:00'
    FridayEnabled = $true
    SaturdayBlockStart = '12:00'; SaturdayBlockEnd = '13:00'
    SaturdayEnabled = $false
    SundayBlockStart = '18:00'; SundayBlockEnd = '19:00'
    SundayEnabled = $true
}
Assert-Equal $false (Test-AppBlockerSchedule -CurrentTime ([datetime]'2026-01-07 14:30:00') -Config $individualSchedule) 'Disabled individual Wednesday starts no blocking period'
Assert-Equal $false (Test-AppBlockerSchedule -CurrentTime ([datetime]'2026-01-07 12:30:00') -Config $individualSchedule) 'Tuesday hours do not apply on Wednesday'
Assert-Equal $true  (Test-AppBlockerSchedule -CurrentTime ([datetime]'2026-01-10 05:30:00') -Config $individualSchedule) 'Individual Friday overnight schedule finishes Saturday'
Assert-Equal $false (Test-AppBlockerSchedule -CurrentTime ([datetime]'2026-01-10 06:30:00') -Config $individualSchedule) 'Individual overnight end is respected'
Assert-Equal $false (Test-AppBlockerSchedule -CurrentTime ([datetime]'2026-01-10 12:30:00') -Config $individualSchedule) 'Disabled individual Saturday starts no blocking period'

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
    Assert-Equal 'EveryDay' $loaded.ScheduleMode 'Legacy config receives every-day schedule default'
    Assert-Equal 'Always' $loaded.UninstallPolicy 'Legacy config receives permissive policy default'
    Assert-Equal 'Always' $loaded.TurnOffProtectionPolicy 'Legacy config receives protection policy default'
    Assert-Equal '23:00' $loaded.MondayBlockStart 'Legacy config receives individual-day defaults'
    Assert-Equal $true $loaded.WeekdayEnabled 'Legacy config enables weekdays by default'
    Assert-Equal $true $loaded.WeekendEnabled 'Legacy config enables weekends by default'
    Assert-Equal $true $loaded.MondayEnabled 'Legacy config enables individual days by default'
} finally {
    Remove-Item -LiteralPath $testConfigPath -Force -ErrorAction SilentlyContinue
}

$disabledEqualScheduleAccepted = $true
try {
    [pscustomobject]@{
        BlockStart = '23:00'
        BlockEnd = '07:00'
        ScheduleMode = 'IndividualDays'
        MondayBlockStart = '10:00'
        MondayBlockEnd = '10:00'
        MondayEnabled = $false
        CheckIntervalSeconds = 2
        TargetUserSid = 'S-1-5-21-1000-1000-1000-1000'
        Executables = @()
    } | ConvertTo-Json | Set-Content -LiteralPath $testConfigPath -Encoding UTF8
    [void](Get-AppBlockerConfig -Path $testConfigPath)
} catch {
    $disabledEqualScheduleAccepted = $false
} finally {
    Remove-Item -LiteralPath $testConfigPath -Force -ErrorAction SilentlyContinue
}
Assert-Equal $true $disabledEqualScheduleAccepted 'Disabled day does not require a time range'

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

$badPolicyRejected = $false
try {
    [pscustomobject]@{
        BlockStart = '23:00'
        BlockEnd = '07:00'
        ScheduleMode = 'EveryDay'
        WeekdayBlockStart = '23:00'
        WeekdayBlockEnd = '07:00'
        WeekendBlockStart = '00:00'
        WeekendBlockEnd = '09:00'
        ChangeTimesPolicy = 'Sometimes'
        UninstallPolicy = 'Always'
        RemoveExecutablesPolicy = 'Always'
        SetupCompleted = $true
        CheckIntervalSeconds = 2
        TargetUserSid = 'S-1-5-21-1000-1000-1000-1000'
        Executables = @()
    } | ConvertTo-Json | Set-Content -LiteralPath $testConfigPath -Encoding UTF8
    [void](Get-AppBlockerConfig -Path $testConfigPath)
} catch {
    $badPolicyRejected = $true
} finally {
    Remove-Item -LiteralPath $testConfigPath -Force -ErrorAction SilentlyContinue
}
Assert-Equal $true $badPolicyRejected 'Invalid setup policy is rejected'

if ($failures -gt 0) { throw "$failures test(s) failed." }
Write-Host 'All Windows App Blocker tests passed.' -ForegroundColor Cyan
