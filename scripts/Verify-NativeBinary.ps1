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
$headers = (& dumpbin /headers $binary | Out-String)
$expectedMachine = if ($Architecture -eq "x64") { "machine \(x64\)" } else { "machine \(ARM64\)" }
if ($headers -notmatch $expectedMachine) {
    throw "Native binary does not have the expected $Architecture machine type."
}

$exports = (& dumpbin /exports $binary | Out-String)
$actualExports = @([regex]::Matches($exports, '\bnsc55_[a-z0-9_]+\b') | ForEach-Object Value | Sort-Object -Unique)
$expectedExports = @(
    "nsc55_abi_version", "nsc55_clear_midi", "nsc55_create", "nsc55_destroy", "nsc55_queue_midi",
    "nsc55_render_f32", "nsc55_render_s16", "nsc55_reset_emulator", "nsc55_romset_supported", "nsc55_sample_rate"
)
if (Compare-Object $expectedExports $actualExports) {
    throw "Native export set differs from the C ABI contract."
}

$dependents = (& dumpbin /dependents $binary | Out-String)
if ($dependents -match '(?i)(msvcp|vcruntime|ucrtbase|SDL|speex|rtmidi|clap)') {
    throw "Native binary has a forbidden runtime or frontend dependency.`n$dependents"
}

$symbols = (& dumpbin /symbols $binary | Out-String)
if ($symbols -match 'LCD_FontRender|back_palette|lcd_font') {
    throw "Native binary contains visual LCD renderer symbols."
}

Write-Output "Windows native binary verified: $binary"
