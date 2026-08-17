[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$BinaryPath,
    [Parameter(Mandatory)]
    [ValidateSet("x64", "arm64")]
    [string]$Architecture
)

$ErrorActionPreference = "Stop"
$binary = [System.IO.Path]::GetFullPath($BinaryPath)

function Resolve-Dumpbin {
    $command = Get-Command dumpbin.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $vswhereCommand = Get-Command vswhere.exe -ErrorAction SilentlyContinue
    $vswherePath = if ($null -ne $vswhereCommand) { $vswhereCommand.Source } else { $null }
    if ($null -eq $vswherePath) {
        $vswherePath = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
        if (-not (Test-Path $vswherePath)) {
            $vswherePath = $null
        }
    }
    if ($null -eq $vswherePath) {
        throw "dumpbin.exe is not on PATH and vswhere.exe could not be found."
    }

    $hostDirectory = switch ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture) {
        "X64" { "Hostx64" }
        "Arm64" { "Hostarm64" }
        "X86" { "Hostx86" }
        default { $null }
    }
    $installations = @(& $vswherePath -products * -property installationPath)
    if ($LASTEXITCODE -ne 0) {
        throw "vswhere.exe failed while locating Visual Studio."
    }

    foreach ($installation in $installations) {
        $toolsets = @(Get-ChildItem (Join-Path $installation "VC\Tools\MSVC") -Directory -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending)
        foreach ($toolset in $toolsets) {
            if ($null -ne $hostDirectory) {
                $preferred = Get-ChildItem (Join-Path $toolset.FullName "bin\$hostDirectory\*\dumpbin.exe") -File -ErrorAction SilentlyContinue |
                    Select-Object -First 1
                if ($null -ne $preferred) {
                    return $preferred.FullName
                }
            }

            $fallback = Get-ChildItem (Join-Path $toolset.FullName "bin\Host*\*\dumpbin.exe") -File -ErrorAction SilentlyContinue |
                Select-Object -First 1
            if ($null -ne $fallback) {
                return $fallback.FullName
            }
        }
    }

    throw "Visual Studio does not contain dumpbin.exe."
}

$dumpbin = Resolve-Dumpbin

function Invoke-Dumpbin([string]$Option) {
    $output = (& $dumpbin $Option $binary | Out-String)
    if ($LASTEXITCODE -ne 0) {
        throw "dumpbin.exe $Option failed for '$binary'.`n$output"
    }
    return $output
}

$headers = Invoke-Dumpbin /headers
$expectedMachine = if ($Architecture -eq "x64") { "machine \(x64\)" } else { "machine \(ARM64\)" }
if ($headers -notmatch $expectedMachine) {
    throw "Native binary does not have the expected $Architecture machine type."
}

$exports = Invoke-Dumpbin /exports
$actualExports = @([regex]::Matches($exports, '\bnsc55_[a-z0-9_]+\b') | ForEach-Object Value | Sort-Object -Unique)
$expectedExports = @(
    "nsc55_abi_version", "nsc55_clear_midi", "nsc55_create", "nsc55_destroy", "nsc55_queue_midi",
    "nsc55_render_f32", "nsc55_render_s16", "nsc55_reset_emulator", "nsc55_romset_supported", "nsc55_sample_rate"
)
if (Compare-Object $expectedExports $actualExports) {
    throw "Native export set differs from the C ABI contract."
}

$dependents = Invoke-Dumpbin /dependents
if ($dependents -match '(?i)(msvcp|vcruntime|ucrtbase|SDL|speex|rtmidi|clap)') {
    throw "Native binary has a forbidden runtime or frontend dependency.`n$dependents"
}

$symbols = Invoke-Dumpbin /symbols
if ($symbols -match 'LCD_FontRender|back_palette|lcd_font') {
    throw "Native binary contains visual LCD renderer symbols."
}

Write-Output "Windows native binary verified: $binary"
