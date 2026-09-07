Set-StrictMode -Version 2.0

function ConvertTo-SteamBlockTime {
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

function Test-SteamBlockTime {
    param(
        [Parameter(Mandatory = $true)][datetime]$CurrentTime,
        [Parameter(Mandatory = $true)][string]$BlockStart,
        [Parameter(Mandatory = $true)][string]$BlockEnd
    )

    $start = ConvertTo-SteamBlockTime $BlockStart
    $end = ConvertTo-SteamBlockTime $BlockEnd
    if ($start -eq $end) {
        throw 'BlockStart and BlockEnd must be different.'
    }

    $now = $CurrentTime.TimeOfDay
    if ($start -lt $end) {
        return ($now -ge $start -and $now -lt $end)
    }

    return ($now -ge $start -or $now -lt $end)
}

function ConvertTo-NormalizedPath {
    param([AllowNull()][AllowEmptyString()][string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) { return $null }
    try {
        return [IO.Path]::GetFullPath($Path).TrimEnd([char[]]@('\', '/'))
    } catch {
        return $null
    }
}

function Test-PathInsideDirectory {
    param(
        [AllowNull()][string]$Path,
        [AllowNull()][string]$Directory
    )

    $normalizedPath = ConvertTo-NormalizedPath $Path
    $normalizedDirectory = ConvertTo-NormalizedPath $Directory
    if (-not $normalizedPath -or -not $normalizedDirectory) { return $false }

    if ($normalizedPath.Equals($normalizedDirectory, [StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    $prefix = $normalizedDirectory + [IO.Path]::DirectorySeparatorChar
    return $normalizedPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)
}

function Get-SteamBlockConfig {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Configuration file not found: $Path"
    }

    $config = Get-Content -LiteralPath $Path -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
    [void](ConvertTo-SteamBlockTime ([string]$config.BlockStart))
    [void](ConvertTo-SteamBlockTime ([string]$config.BlockEnd))
    if ([string]$config.BlockStart -eq [string]$config.BlockEnd) {
        throw 'BlockStart and BlockEnd must be different.'
    }
    if ([int]$config.CheckIntervalSeconds -lt 1 -or [int]$config.CheckIntervalSeconds -gt 60) {
        throw 'CheckIntervalSeconds must be between 1 and 60.'
    }
    if ([string]::IsNullOrWhiteSpace([string]$config.TargetUserSid)) {
        throw 'TargetUserSid is required.'
    }
    try {
        [void](New-Object Security.Principal.SecurityIdentifier([string]$config.TargetUserSid))
    } catch {
        throw 'TargetUserSid is not a valid Windows security identifier.'
    }
    if ([string]::IsNullOrWhiteSpace([string]$config.SteamPath)) {
        throw 'SteamPath is required.'
    }

    return $config
}

function Save-SteamBlockConfig {
    param(
        [Parameter(Mandatory = $true)]$Config,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }

    $temporaryPath = Join-Path $parent ('.config-{0}.tmp' -f [guid]::NewGuid().ToString('N'))
    try {
        $Config | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $temporaryPath -Encoding UTF8
        [void](Get-SteamBlockConfig -Path $temporaryPath)
        Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
    } finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
        }
    }
}

function Get-SteamInstallPath {
    param([string]$Override)

    if ($Override) {
        $candidate = ConvertTo-NormalizedPath $Override
        if ($candidate -and (Test-Path -LiteralPath (Join-Path $candidate 'steam.exe'))) { return $candidate }
        throw "Steam was not found at the supplied path: $Override"
    }

    $candidates = @()
    foreach ($registryPath in @('HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam', 'HKLM:\SOFTWARE\Valve\Steam')) {
        try {
            $properties = Get-ItemProperty -LiteralPath $registryPath -ErrorAction Stop
            if ($properties.SteamPath) { $candidates += [string]$properties.SteamPath }
            if ($properties.InstallPath) { $candidates += [string]$properties.InstallPath }
        } catch { }
    }
    if (${env:ProgramFiles(x86)}) { $candidates += (Join-Path ${env:ProgramFiles(x86)} 'Steam') }
    if ($env:ProgramFiles) { $candidates += (Join-Path $env:ProgramFiles 'Steam') }

    foreach ($candidatePath in ($candidates | Select-Object -Unique)) {
        $normalized = ConvertTo-NormalizedPath $candidatePath
        if ($normalized -and (Test-Path -LiteralPath (Join-Path $normalized 'steam.exe'))) { return $normalized }
    }

    throw 'Steam installation could not be found. Pass -SteamPath to the installer.'
}

function ConvertFrom-VdfEscapedString {
    param([string]$Value)
    return $Value.Replace('\\', '\').Replace('\"', '"')
}

function Get-SteamLibraryPaths {
    param([Parameter(Mandatory = $true)][string]$SteamPath)

    $results = @($SteamPath)
    $vdfPath = Join-Path $SteamPath 'steamapps\libraryfolders.vdf'
    if (Test-Path -LiteralPath $vdfPath) {
        foreach ($line in (Get-Content -LiteralPath $vdfPath -ErrorAction SilentlyContinue)) {
            if ($line -match '^\s*"path"\s*"((?:\\.|[^"])*)"') {
                $results += ConvertFrom-VdfEscapedString $matches[1]
            }
        }
    }

    return @($results | ForEach-Object { ConvertTo-NormalizedPath $_ } | Where-Object { $_ } | Sort-Object -Unique)
}

function Get-SteamGameDirectories {
    param([Parameter(Mandatory = $true)][string[]]$LibraryPaths)

    $results = @()
    foreach ($library in $LibraryPaths) {
        $commonPath = ConvertTo-NormalizedPath (Join-Path $library 'steamapps\common')
        if ($commonPath -and (Test-Path -LiteralPath $commonPath -PathType Container)) { $results += $commonPath }
    }
    return @($results | Sort-Object -Unique)
}
