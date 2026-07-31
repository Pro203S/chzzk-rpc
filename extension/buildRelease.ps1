$ErrorActionPreference = "Stop"

$extensionDirectory = $PSScriptRoot
$manifestPath = Join-Path $extensionDirectory "manifest.json"
$distPath = Join-Path $extensionDirectory "dist"
$artifactsDirectory = Join-Path $extensionDirectory "artifacts"

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$zipPath = Join-Path $artifactsDirectory "discheese-extension-v$($manifest.version).zip"

Push-Location $extensionDirectory

try {
    & bun run build

    if ($LASTEXITCODE -ne 0) {
        throw "Extension build failed with exit code $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $distPath -PathType Container)) {
        throw "Build output directory was not created: $distPath"
    }

    New-Item -ItemType Directory -Path $artifactsDirectory -Force | Out-Null

    Add-Type -AssemblyName System.IO.Compression

    $zipStream = [System.IO.File]::Open(
        $zipPath,
        [System.IO.FileMode]::Create,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None
    )

    try {
        $archive = [System.IO.Compression.ZipArchive]::new(
            $zipStream,
            [System.IO.Compression.ZipArchiveMode]::Create,
            $false
        )

        try {
            $files = @(
                Get-Item -LiteralPath $manifestPath
                Get-ChildItem -LiteralPath $distPath -File -Recurse
            )

            foreach ($file in $files) {
                if ($file.FullName -eq $manifestPath) {
                    $entryName = "manifest.json"
                }
                else {
                    $relativePath = $file.FullName.Substring($distPath.Length + 1)
                    $entryName = "dist/" + $relativePath.Replace("\", "/")
                }

                $entry = $archive.CreateEntry(
                    $entryName,
                    [System.IO.Compression.CompressionLevel]::Optimal
                )
                $entryStream = $entry.Open()
                $fileStream = $file.OpenRead()

                try {
                    $fileStream.CopyTo($entryStream)
                }
                finally {
                    $fileStream.Dispose()
                    $entryStream.Dispose()
                }
            }
        }
        finally {
            $archive.Dispose()
        }
    }
    finally {
        $zipStream.Dispose()
    }

    Write-Host "Chrome extension package created: $zipPath"
}
finally {
    Pop-Location
}
