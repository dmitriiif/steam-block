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

    [void](ConvertTo-AppBlockerTime ([string]$config.BlockStart))
    [void](ConvertTo-AppBlockerTime ([string]$config.BlockEnd))
    if ([string]$config.BlockStart -eq [string]$config.BlockEnd) { throw 'BlockStart and BlockEnd must be different.' }
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
