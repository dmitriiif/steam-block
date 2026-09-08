Set-StrictMode -Version 2.0

function ConvertTo-AppBlockerTime {
    param([Parameter(Mandatory = $true)][string]$Value)

    $parsed = [datetime]::MinValue
    if (-not [datetime]::TryParseExact(
        $Value,
        'HH:mm',
        [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::None,
        [ref]$parsed
    )) {
        throw "Invalid time '$Value'. Use 24-hour HH:mm format."
    }
    return $parsed.TimeOfDay
}

function Test-AppBlockerTime {
    param(
        [Parameter(Mandatory = $true)][datetime]$CurrentTime,
        [Parameter(Mandatory = $true)][string]$BlockStart,
        [Parameter(Mandatory = $true)][string]$BlockEnd
    )

    $start = ConvertTo-AppBlockerTime $BlockStart
    $end = ConvertTo-AppBlockerTime $BlockEnd
    if ($start -eq $end) { throw 'BlockStart and BlockEnd must be different.' }

    $now = $CurrentTime.TimeOfDay
    if ($start -lt $end) { return ($now -ge $start -and $now -lt $end) }
    return ($now -ge $start -or $now -lt $end)
}

function Get-AppBlockerDaySchedule {
    param(
        [Parameter(Mandatory = $true)]$Config,
        [Parameter(Mandatory = $true)][DayOfWeek]$DayOfWeek
    )

    if ([string]$Config.ScheduleMode -eq 'IndividualDays') {
        $prefix = [string]$DayOfWeek
        return @([string]$Config."${prefix}BlockStart", [string]$Config."${prefix}BlockEnd")
    }
    $isWeekend = $DayOfWeek -eq [DayOfWeek]::Saturday -or $DayOfWeek -eq [DayOfWeek]::Sunday
    if ($isWeekend) {
        return @([string]$Config.WeekendBlockStart, [string]$Config.WeekendBlockEnd)
    }
    return @([string]$Config.WeekdayBlockStart, [string]$Config.WeekdayBlockEnd)
}

function Test-AppBlockerDayEnabled {
    param(
        [Parameter(Mandatory = $true)]$Config,
        [Parameter(Mandatory = $true)][DayOfWeek]$DayOfWeek
    )

    if ([string]$Config.ScheduleMode -eq 'IndividualDays') {
        $propertyName = "${DayOfWeek}Enabled"
    } else {
        $isWeekend = $DayOfWeek -eq [DayOfWeek]::Saturday -or $DayOfWeek -eq [DayOfWeek]::Sunday
        $propertyName = if ($isWeekend) { 'WeekendEnabled' } else { 'WeekdayEnabled' }
    }
    if (-not ($Config.PSObject.Properties.Name -contains $propertyName)) { return $true }
    $value = $Config.$propertyName
    if ($null -eq $value) { return $true }
    return [bool]$value
}

function Test-AppBlockerSchedule {
    param(
        [Parameter(Mandatory = $true)][datetime]$CurrentTime,
        [Parameter(Mandatory = $true)]$Config
    )

    if ([string]$Config.ScheduleMode -eq 'EveryDay') {
        return Test-AppBlockerTime -CurrentTime $CurrentTime -BlockStart ([string]$Config.BlockStart) -BlockEnd ([string]$Config.BlockEnd)
    }

    $today = Get-AppBlockerDaySchedule -Config $Config -DayOfWeek $CurrentTime.DayOfWeek
    $todayStart = ConvertTo-AppBlockerTime $today[0]
    $todayEnd = ConvertTo-AppBlockerTime $today[1]
    $now = $CurrentTime.TimeOfDay
    $todayEnabled = Test-AppBlockerDayEnabled -Config $Config -DayOfWeek $CurrentTime.DayOfWeek
    if ($todayEnabled -and $todayStart -lt $todayEnd -and $now -ge $todayStart -and $now -lt $todayEnd) { return $true }
    if ($todayEnabled -and $todayStart -gt $todayEnd -and $now -ge $todayStart) { return $true }

    $yesterday = Get-AppBlockerDaySchedule -Config $Config -DayOfWeek $CurrentTime.AddDays(-1).DayOfWeek
    $yesterdayStart = ConvertTo-AppBlockerTime $yesterday[0]
    $yesterdayEnd = ConvertTo-AppBlockerTime $yesterday[1]
    $yesterdayEnabled = Test-AppBlockerDayEnabled -Config $Config -DayOfWeek $CurrentTime.AddDays(-1).DayOfWeek
    return ($yesterdayEnabled -and $yesterdayStart -gt $yesterdayEnd -and $now -lt $yesterdayEnd)
}

function ConvertTo-NormalizedPath {
    param([AllowNull()][AllowEmptyString()][string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) { return $null }
    try { return [IO.Path]::GetFullPath($Path).TrimEnd([char[]]@('\', '/')) }
    catch { return $null }
}

function Test-ConfiguredExecutablePath {
    param(
        [AllowNull()][string]$ProcessPath,
        [AllowNull()][object[]]$Executables
    )

    $normalizedProcessPath = ConvertTo-NormalizedPath $ProcessPath
    if (-not $normalizedProcessPath) { return $false }
    foreach ($executable in @($Executables)) {
        $configuredPath = ConvertTo-NormalizedPath ([string]$executable)
        if ($configuredPath -and $normalizedProcessPath.Equals($configuredPath, [StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }
    return $false
}

function Get-AppBlockerConfig {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Configuration file not found: $Path" }
    $config = Get-Content -LiteralPath $Path -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop

    $defaults = @{
        ScheduleMode = 'EveryDay'
        WeekdayBlockStart = [string]$config.BlockStart
        WeekdayBlockEnd = [string]$config.BlockEnd
        WeekendBlockStart = [string]$config.BlockStart
        WeekendBlockEnd = [string]$config.BlockEnd
        WeekdayEnabled = $true
        WeekendEnabled = $true
        ChangeTimesPolicy = 'Always'
        TurnOffProtectionPolicy = 'Always'
        UninstallPolicy = 'Always'
        RemoveExecutablesPolicy = 'Always'
        SetupCompleted = $false
        SetupVersion = 0
    }
    foreach ($propertyName in $defaults.Keys) {
        if (-not ($config.PSObject.Properties.Name -contains $propertyName)) {
            $config | Add-Member -NotePropertyName $propertyName -NotePropertyValue $defaults[$propertyName]
        }
    }

    $individualDefaults = @{}
    foreach ($dayName in @('Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday')) {
        $individualDefaults["${dayName}BlockStart"] = [string]$config.WeekdayBlockStart
        $individualDefaults["${dayName}BlockEnd"] = [string]$config.WeekdayBlockEnd
        $individualDefaults["${dayName}Enabled"] = $true
    }
    foreach ($dayName in @('Saturday', 'Sunday')) {
        $individualDefaults["${dayName}BlockStart"] = [string]$config.WeekendBlockStart
        $individualDefaults["${dayName}BlockEnd"] = [string]$config.WeekendBlockEnd
        $individualDefaults["${dayName}Enabled"] = $true
    }
    foreach ($propertyName in $individualDefaults.Keys) {
        if (-not ($config.PSObject.Properties.Name -contains $propertyName)) {
            $config | Add-Member -NotePropertyName $propertyName -NotePropertyValue $individualDefaults[$propertyName]
        }
    }

    if ([string]$config.ScheduleMode -notin @('EveryDay', 'WeekdayWeekend', 'IndividualDays')) {
        throw "ScheduleMode must be 'EveryDay', 'WeekdayWeekend', or 'IndividualDays'."
    }
    [void](ConvertTo-AppBlockerTime ([string]$config.BlockStart))
    [void](ConvertTo-AppBlockerTime ([string]$config.BlockEnd))
    if ([string]$config.BlockStart -eq [string]$config.BlockEnd) { throw 'BlockStart and BlockEnd must be different.' }
    foreach ($prefix in @('Weekday', 'Weekend', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday')) {
        $startProperty = "${prefix}BlockStart"
        $endProperty = "${prefix}BlockEnd"
        [void](ConvertTo-AppBlockerTime ([string]$config.$startProperty))
        [void](ConvertTo-AppBlockerTime ([string]$config.$endProperty))
        $enabledProperty = "${prefix}Enabled"
        $scheduleEnabled = -not ($config.PSObject.Properties.Name -contains $enabledProperty) -or $null -eq $config.$enabledProperty -or [bool]$config.$enabledProperty
        if ($scheduleEnabled -and [string]$config.$startProperty -eq [string]$config.$endProperty) {
            throw "$startProperty and $endProperty must be different."
        }
    }
    foreach ($policyProperty in @('ChangeTimesPolicy', 'TurnOffProtectionPolicy', 'UninstallPolicy', 'RemoveExecutablesPolicy')) {
        if ([string]$config.$policyProperty -notin @('Always', 'Never', 'AllowedHoursOnly')) {
            throw "$policyProperty has an invalid value."
        }
    }
    if ([int]$config.CheckIntervalSeconds -lt 1 -or [int]$config.CheckIntervalSeconds -gt 60) {
        throw 'CheckIntervalSeconds must be between 1 and 60.'
    }
    if ([string]::IsNullOrWhiteSpace([string]$config.TargetUserSid)) { throw 'TargetUserSid is required.' }
    try { [void](New-Object Security.Principal.SecurityIdentifier([string]$config.TargetUserSid)) }
    catch { throw 'TargetUserSid is not a valid Windows security identifier.' }
    if (-not ($config.PSObject.Properties.Name -contains 'Executables')) { throw 'Executables is required.' }

    foreach ($executable in @($config.Executables)) {
        $rawPath = [string]$executable
        $path = ConvertTo-NormalizedPath $rawPath
        if (-not [IO.Path]::IsPathRooted($rawPath) -or -not $path -or [IO.Path]::GetExtension($path) -ine '.exe') {
            throw "Invalid executable path '$executable'. Choose an absolute .exe path."
        }
    }
    return $config
}

function Save-AppBlockerConfig {
    param(
        [Parameter(Mandatory = $true)]$Config,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    $temporaryPath = Join-Path $parent ('.config-{0}.tmp' -f [guid]::NewGuid().ToString('N'))
    try {
        $Config | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $temporaryPath -Encoding UTF8
        [void](Get-AppBlockerConfig -Path $temporaryPath)
        Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
    } finally {
        if (Test-Path -LiteralPath $temporaryPath) { Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue }
    }
}
