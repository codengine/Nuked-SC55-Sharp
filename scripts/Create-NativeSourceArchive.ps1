[CmdletBinding()]
param(
    [string]$OutputPath = ".artifacts/source/Nuked-SC55-Sharp-native-source.zip"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$upstreamRoot = Join-Path $root "external/Nuked-SC55"
$expectedCommit = "a48ef92e4bb85355a57a6ff2250d942f835f3503"
$actualCommit = (& git -c "safe.directory=$upstreamRoot" -C $upstreamRoot rev-parse HEAD).Trim()
if ($actualCommit -ne $expectedCommit) {
    throw "Expected Nuked-SC55 commit $expectedCommit but found $actualCommit."
}

$output = [System.IO.Path]::GetFullPath((Join-Path $root $OutputPath))
$outputDirectory = Split-Path -Parent $output
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null

$sourceFiles = @(
    Get-ChildItem (Join-Path $root "native") -Recurse -File |
        Where-Object { [System.IO.Path]::GetRelativePath($root, $_.FullName) -notmatch '^native[\\/]build[^\\/]*[\\/]' }
    Get-ChildItem $upstreamRoot -Recurse -File |
        Where-Object { $_.Name -ne ".git" }
    Get-Item (Join-Path $root "LICENSE")
    Get-Item (Join-Path $root "THIRD_PARTY_NOTICES.md")
) | Sort-Object { [System.IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/') }

$entries = foreach ($sourceFile in $sourceFiles) {
    $relativePath = [System.IO.Path]::GetRelativePath($root, $sourceFile.FullName).Replace('\', '/')
    [ordered]@{
        path = $relativePath
        sha256 = (Get-FileHash -LiteralPath $sourceFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

$manifest = [ordered]@{
    schemaVersion = 1
    package = "Nuked-SC55-Sharp"
    packageVersion = "0.7.0"
    nativeAbiVersion = 1
    nativeLanguageStandard = "C++23"
    upstream = [ordered]@{
        name = "Nuked-SC55"
        repository = "https://github.com/jcmoyer/Nuked-SC55"
        commit = $actualCommit
    }
    files = $entries
} | ConvertTo-Json -Depth 5

if (Test-Path -LiteralPath $output) {
    Remove-Item -LiteralPath $output
}

Add-Type -AssemblyName System.IO.Compression
$stream = [System.IO.File]::Open($output, [System.IO.FileMode]::CreateNew)
try {
    $archive = [System.IO.Compression.ZipArchive]::new($stream, [System.IO.Compression.ZipArchiveMode]::Create, $false)
    try {
        $timestamp = [System.DateTimeOffset]::new(2000, 1, 1, 0, 0, 0, [System.TimeSpan]::Zero)
        foreach ($sourceFile in $sourceFiles) {
            $relativePath = [System.IO.Path]::GetRelativePath($root, $sourceFile.FullName).Replace('\', '/')
            $entry = $archive.CreateEntry("Nuked-SC55-Sharp/$relativePath", [System.IO.Compression.CompressionLevel]::NoCompression)
            $entry.LastWriteTime = $timestamp
            $entryStream = $entry.Open()
            try {
                $input = [System.IO.File]::OpenRead($sourceFile.FullName)
                try {
                    $input.CopyTo($entryStream)
                }
                finally {
                    $input.Dispose()
                }
            }
            finally {
                $entryStream.Dispose()
            }
        }

        $manifestEntry = $archive.CreateEntry("Nuked-SC55-Sharp/native-source-manifest.json", [System.IO.Compression.CompressionLevel]::NoCompression)
        $manifestEntry.LastWriteTime = $timestamp
        $manifestStream = $manifestEntry.Open()
        try {
            $writer = [System.IO.StreamWriter]::new($manifestStream, [System.Text.UTF8Encoding]::new($false), 1024, $true)
            try {
                $writer.Write($manifest)
                $writer.Write("`n")
            }
            finally {
                $writer.Dispose()
            }
        }
        finally {
            $manifestStream.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}
finally {
    $stream.Dispose()
}

Write-Output $output
