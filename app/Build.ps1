[CmdletBinding()]
param()

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$compilerCandidates = @(
    "$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:SystemRoot\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) { throw 'The Windows .NET Framework C# compiler was not found.' }

$outputPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'Windows App Blocker.exe'
$arguments = @(
    '/nologo',
    '/target:winexe',
    '/optimize+',
    '/platform:anycpu',
    ('/out:{0}' -f $outputPath),
    ('/win32manifest:{0}' -f (Join-Path $PSScriptRoot 'WindowsAppBlocker.exe.manifest')),
    '/reference:System.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll',
    '/reference:System.Web.Extensions.dll',
    '/reference:Microsoft.CSharp.dll',
    (Join-Path $PSScriptRoot 'WindowsAppBlocker.cs')
)

& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw "C# compilation failed with exit code $LASTEXITCODE." }
Write-Output "Built $outputPath"
