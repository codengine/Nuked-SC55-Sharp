[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PackagePath,
    [switch]$RequireAllRids
)

$ErrorActionPreference = "Stop"
$package = [System.IO.Path]::GetFullPath($PackagePath)
if (-not (Test-Path -LiteralPath $package)) {
    throw "Package not found: $package"
}

Add-Type -AssemblyName System.IO.Compression
$archive = [System.IO.Compression.ZipFile]::OpenRead($package)
try {
    $paths = @($archive.Entries | ForEach-Object FullName)
    $required = @(
        "LICENSE",
        "README.md",
        "THIRD_PARTY_NOTICES.md",
        "lib/netstandard2.1/NukedSC55Sharp.dll",
        "lib/netstandard2.1/NukedSC55Sharp.xml",
        "lib/net8.0/NukedSC55Sharp.dll",
        "lib/net8.0/NukedSC55Sharp.xml",
        "lib/net10.0/NukedSC55Sharp.dll",
        "lib/net10.0/NukedSC55Sharp.xml",
        "native-source/Nuked-SC55-Sharp-native-source.zip"
    )
    if ($RequireAllRids) {
        $required += @(
            "runtimes/win-x64/native/NukedSC55Sharp.Native.dll",
            "runtimes/win-arm64/native/NukedSC55Sharp.Native.dll",
            "runtimes/linux-x64/native/libNukedSC55Sharp.Native.so",
            "runtimes/linux-arm64/native/libNukedSC55Sharp.Native.so",
            "runtimes/osx-x64/native/libNukedSC55Sharp.Native.dylib",
            "runtimes/osx-arm64/native/libNukedSC55Sharp.Native.dylib"
        )
    }

    $missing = @($required | Where-Object { $_ -notin $paths })
    if ($missing.Count -ne 0) {
        throw "Package is missing required entries: $($missing -join ', ')"
    }

    $knownRids = @("win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64")
    $unexpectedNative = @($paths | Where-Object {
        $_ -match '^runtimes/([^/]+)/native/' -and $Matches[1] -notin $knownRids
    })
    if ($unexpectedNative.Count -ne 0) {
        throw "Package has native assets under unsupported RIDs: $($unexpectedNative -join ', ')"
    }

    $sourceEntry = $archive.GetEntry("native-source/Nuked-SC55-Sharp-native-source.zip")
    $memory = [System.IO.MemoryStream]::new()
    try {
        $sourceStream = $sourceEntry.Open()
        try {
            $sourceStream.CopyTo($memory)
        }
        finally {
            $sourceStream.Dispose()
        }
        $memory.Position = 0
        $sourceArchive = [System.IO.Compression.ZipArchive]::new($memory, [System.IO.Compression.ZipArchiveMode]::Read, $true)
        try {
            $sourcePaths = @($sourceArchive.Entries | ForEach-Object FullName)
            foreach ($requiredSource in @(
                "Nuked-SC55-Sharp/native-source-manifest.json",
                "Nuked-SC55-Sharp/native/CMakeLists.txt",
                "Nuked-SC55-Sharp/native/src/nuked_sc55_sharp.cpp",
                "Nuked-SC55-Sharp/external/Nuked-SC55/CMakeLists.txt",
                "Nuked-SC55-Sharp/external/Nuked-SC55/LICENSE",
                "Nuked-SC55-Sharp/LICENSE",
                "Nuked-SC55-Sharp/THIRD_PARTY_NOTICES.md"
            )) {
                if ($requiredSource -notin $sourcePaths) {
                    throw "Native source archive is missing $requiredSource."
                }
            }
        }
        finally {
            $sourceArchive.Dispose()
        }
    }
    finally {
        $memory.Dispose()
    }
}
finally {
    $archive.Dispose()
}

Write-Output "Package layout verified: $package"
