[CmdletBinding()]
param()

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdministrator)) {
    $quotedScript = '"{0}"' -f $PSCommandPath
    Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $quotedScript)
    exit
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()

$taskName = 'SteamCurfew'
$installPath = 'C:\ProgramData\SteamCurfew'
$configPath = Join-Path $installPath 'config.json'
$localCommon = Join-Path $PSScriptRoot 'SteamBlock.Common.ps1'
. $localCommon

$form = New-Object Windows.Forms.Form
$form.Text = 'Steam Block'
$form.ClientSize = New-Object Drawing.Size(430, 320)
$form.FormBorderStyle = 'FixedDialog'
$form.MaximizeBox = $false
$form.StartPosition = 'CenterScreen'
$form.Font = New-Object Drawing.Font('Segoe UI', 10)
$form.BackColor = [Drawing.Color]::FromArgb(245, 247, 250)

$title = New-Object Windows.Forms.Label
$title.Text = 'Steam Block'
$title.Font = New-Object Drawing.Font('Segoe UI Semibold', 20)
$title.AutoSize = $true
$title.Location = New-Object Drawing.Point(24, 20)
$form.Controls.Add($title)

$status = New-Object Windows.Forms.Label
$status.AutoSize = $false
$status.Size = New-Object Drawing.Size(382, 34)
$status.Location = New-Object Drawing.Point(24, 67)
$status.Padding = New-Object Windows.Forms.Padding(10, 7, 10, 5)
$status.TextAlign = 'MiddleLeft'
$form.Controls.Add($status)

$startLabel = New-Object Windows.Forms.Label
$startLabel.Text = 'Block Steam from'
$startLabel.AutoSize = $true
$startLabel.Location = New-Object Drawing.Point(25, 125)
$form.Controls.Add($startLabel)

$startPicker = New-Object Windows.Forms.DateTimePicker
$startPicker.Format = 'Custom'
$startPicker.CustomFormat = 'HH:mm'
$startPicker.ShowUpDown = $true
$startPicker.Width = 92
$startPicker.Location = New-Object Drawing.Point(166, 120)
$form.Controls.Add($startPicker)

$endLabel = New-Object Windows.Forms.Label
$endLabel.Text = 'until'
$endLabel.AutoSize = $true
$endLabel.Location = New-Object Drawing.Point(275, 125)
$form.Controls.Add($endLabel)

$endPicker = New-Object Windows.Forms.DateTimePicker
$endPicker.Format = 'Custom'
$endPicker.CustomFormat = 'HH:mm'
$endPicker.ShowUpDown = $true
$endPicker.Width = 92
$endPicker.Location = New-Object Drawing.Point(314, 120)
$form.Controls.Add($endPicker)

$hint = New-Object Windows.Forms.Label
$hint.Text = 'The blocker runs quietly in the background and starts with Windows.'
$hint.ForeColor = [Drawing.Color]::DimGray
$hint.AutoSize = $true
$hint.Location = New-Object Drawing.Point(25, 162)
$form.Controls.Add($hint)

$saveButton = New-Object Windows.Forms.Button
$saveButton.Text = 'Save time'
$saveButton.Size = New-Object Drawing.Size(118, 38)
$saveButton.Location = New-Object Drawing.Point(25, 201)
$form.Controls.Add($saveButton)

$enableButton = New-Object Windows.Forms.Button
$enableButton.Text = 'Enable'
$enableButton.Size = New-Object Drawing.Size(118, 38)
$enableButton.Location = New-Object Drawing.Point(156, 201)
$enableButton.BackColor = [Drawing.Color]::FromArgb(35, 134, 84)
$enableButton.ForeColor = [Drawing.Color]::White
$enableButton.FlatStyle = 'Flat'
$form.Controls.Add($enableButton)

$disableButton = New-Object Windows.Forms.Button
$disableButton.Text = 'Disable'
$disableButton.Size = New-Object Drawing.Size(118, 38)
$disableButton.Location = New-Object Drawing.Point(288, 201)
$form.Controls.Add($disableButton)

$installButton = New-Object Windows.Forms.Button
$installButton.Text = 'Install / repair background task'
$installButton.Size = New-Object Drawing.Size(250, 34)
$installButton.Location = New-Object Drawing.Point(25, 264)
$form.Controls.Add($installButton)

$gamesLabel = New-Object Windows.Forms.Label
$gamesLabel.Text = ''
$gamesLabel.ForeColor = [Drawing.Color]::DimGray
$gamesLabel.AutoSize = $true
$gamesLabel.Location = New-Object Drawing.Point(290, 272)
$form.Controls.Add($gamesLabel)
$script:PickersLoaded = $false

function Get-TaskEnabled {
    $task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    return ($null -ne $task -and $task.Settings.Enabled)
}

function Load-Configuration {
    param([switch]$ForcePickerRefresh)
    if (-not (Test-Path -LiteralPath $configPath)) { return $null }
    $config = Get-SteamBlockConfig -Path $configPath
    if ($ForcePickerRefresh -or -not $script:PickersLoaded) {
        $startPicker.Value = [datetime]::Today.Add((ConvertTo-SteamBlockTime $config.BlockStart))
        $endPicker.Value = [datetime]::Today.Add((ConvertTo-SteamBlockTime $config.BlockEnd))
        $script:PickersLoaded = $true
    }
    $gamesLabel.Text = ('{0} games' -f @($config.GameDirectories).Count)
    return $config
}

function Update-Display {
    try {
        $config = Load-Configuration
        $task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
        if (-not $config -or -not $task) {
            $status.Text = 'Not installed'
            $status.BackColor = [Drawing.Color]::FromArgb(255, 238, 204)
            $status.ForeColor = [Drawing.Color]::FromArgb(120, 75, 0)
            $saveButton.Enabled = $false
            $enableButton.Enabled = $false
            $disableButton.Enabled = $false
            return
        }

        $saveButton.Enabled = $true
        $enableButton.Enabled = -not $task.Settings.Enabled
        $disableButton.Enabled = $task.Settings.Enabled
        if ($task.Settings.Enabled) {
            $blockedNow = Test-SteamBlockTime -CurrentTime (Get-Date) -BlockStart $config.BlockStart -BlockEnd $config.BlockEnd
            $status.Text = if ($blockedNow) { 'Enabled - Steam is blocked now' } else { 'Enabled - Steam is currently allowed' }
            $status.BackColor = [Drawing.Color]::FromArgb(218, 242, 228)
            $status.ForeColor = [Drawing.Color]::FromArgb(24, 95, 57)
        } else {
            $status.Text = 'Disabled - Steam is allowed'
            $status.BackColor = [Drawing.Color]::FromArgb(232, 235, 239)
            $status.ForeColor = [Drawing.Color]::FromArgb(70, 75, 82)
        }
    } catch {
        $status.Text = 'Error: ' + $_.Exception.Message
        $status.BackColor = [Drawing.Color]::FromArgb(255, 224, 224)
        $status.ForeColor = [Drawing.Color]::DarkRed
    }
}

function Save-Schedule {
    $config = Get-SteamBlockConfig -Path $configPath
    $startValue = $startPicker.Value.ToString('HH:mm')
    $endValue = $endPicker.Value.ToString('HH:mm')
    if ($startValue -eq $endValue) { throw 'Start and end times must be different.' }
    $config.BlockStart = $startValue
    $config.BlockEnd = $endValue
    Save-SteamBlockConfig -Config $config -Path $configPath
}

$saveButton.Add_Click({
    try {
        Save-Schedule
        Update-Display
        [Windows.Forms.MessageBox]::Show('The blocking schedule was saved.', 'Steam Block', 'OK', 'Information') | Out-Null
    } catch { [Windows.Forms.MessageBox]::Show($_.Exception.Message, 'Steam Block', 'OK', 'Error') | Out-Null }
})

$enableButton.Add_Click({
    try {
        Save-Schedule
        Enable-ScheduledTask -TaskName $taskName | Out-Null
        Start-ScheduledTask -TaskName $taskName
        Update-Display
    } catch { [Windows.Forms.MessageBox]::Show($_.Exception.Message, 'Steam Block', 'OK', 'Error') | Out-Null }
})

$disableButton.Add_Click({
    try {
        Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
        Disable-ScheduledTask -TaskName $taskName | Out-Null
        Update-Display
    } catch { [Windows.Forms.MessageBox]::Show($_.Exception.Message, 'Steam Block', 'OK', 'Error') | Out-Null }
})

$installButton.Add_Click({
    try {
        $installer = Join-Path $PSScriptRoot 'Install-SteamCurfew.ps1'
        & $installer -RefreshGames -NoStart
        Disable-ScheduledTask -TaskName $taskName | Out-Null
        $script:PickersLoaded = $false
        Update-Display
        [Windows.Forms.MessageBox]::Show('Installed successfully. Review the times, then select Enable.', 'Steam Block', 'OK', 'Information') | Out-Null
    } catch { [Windows.Forms.MessageBox]::Show($_.Exception.Message, 'Steam Block', 'OK', 'Error') | Out-Null }
})

$timer = New-Object Windows.Forms.Timer
$timer.Interval = 5000
$timer.Add_Tick({ Update-Display })
$timer.Start()

Update-Display
[void]$form.ShowDialog()
$timer.Dispose()
$form.Dispose()
