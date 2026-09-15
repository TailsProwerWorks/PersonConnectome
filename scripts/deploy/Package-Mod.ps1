[CmdletBinding()]
param(
    [string] $OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$modSource = Join-Path $repositoryRoot 'src'
$assetRoot = Join-Path $repositoryRoot 'assets'
$manifestSourcePath = Join-Path $modSource 'mod.json'
$readmeSourcePath = Join-Path $assetRoot 'README.txt'

function Assert-GameReadmeSize([string] $Content) {
    $byteCount = [Text.Encoding]::UTF8.GetByteCount($Content)
    if ($byteCount -gt 5000) {
        throw "The generated mod README is $byteCount UTF-8 bytes; People Playground ignores README.txt above 5000 bytes."
    }
}

function Resolve-ModSourcePath([string] $RelativePath) {
    if ([String]::IsNullOrWhiteSpace($RelativePath) -or [IO.Path]::IsPathRooted($RelativePath)) {
        throw "Manifest contains an invalid relative source path: '$RelativePath'."
    }

    $normalized = $RelativePath.Replace('/', '\')
    $sourceRoot = $modSource.TrimEnd('\') + '\'
    $fullPath = [IO.Path]::GetFullPath((Join-Path $modSource $normalized))
    if (-not $fullPath.StartsWith($sourceRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Manifest source path escapes src: '$RelativePath'."
    }

    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "Manifest script was not found: $fullPath"
    }

    return $fullPath
}

function Add-PackageFile([System.Collections.Generic.List[object]] $Files, [string] $SourcePath, [string] $RelativePath) {
    if ([String]::IsNullOrWhiteSpace($RelativePath)) {
        throw 'A package file is missing its destination path.'
    }

    if (-not (Test-Path -LiteralPath $SourcePath -PathType Leaf)) {
        throw "Package source file was not found: $SourcePath"
    }

    [void]$Files.Add([pscustomobject]@{
            SourcePath   = $SourcePath
            RelativePath = $RelativePath.Replace('/', '\')
        })
}

if (-not (Test-Path -LiteralPath $manifestSourcePath -PathType Leaf)) {
    throw "Mod manifest was not found: $manifestSourcePath"
}

if (-not (Test-Path -LiteralPath $readmeSourcePath -PathType Leaf)) {
    throw "Mod README source was not found: $readmeSourcePath"
}

$manifest = Get-Content -LiteralPath $manifestSourcePath -Raw | ConvertFrom-Json
$readmeContent = Get-Content -LiteralPath $readmeSourcePath -Raw -Encoding UTF8
if (-not $readmeContent.Contains('{{GIT_COMMIT}}')) {
    throw 'The mod README does not contain the {{GIT_COMMIT}} build marker.'
}

$gitCommit = (& git -C $repositoryRoot rev-parse --short=12 HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [String]::IsNullOrWhiteSpace($gitCommit)) {
    throw 'Could not determine the current Git commit for the package.'
}

$readmeContent = $readmeContent.Replace('{{GIT_COMMIT}}', $gitCommit)
Assert-GameReadmeSize $readmeContent

$files = [System.Collections.Generic.List[object]]::new()
Add-PackageFile $files $manifestSourcePath 'mod.json'

$temporaryReadmePath = Join-Path ([IO.Path]::GetTempPath()) ('person-connectome-package-readme-' + [guid]::NewGuid().ToString('N') + '.txt')
[IO.File]::WriteAllText($temporaryReadmePath, $readmeContent, [Text.UTF8Encoding]::new($false))
Add-PackageFile $files $temporaryReadmePath 'README.txt'

foreach ($script in @($manifest.Scripts)) {
    $scriptPath = Resolve-ModSourcePath ([string]$script)
    Add-PackageFile $files $scriptPath ([string]$script)
}

$carrierPath = Join-Path $assetRoot 'connectome\malecns-v1.0.png'
Add-PackageFile $files $carrierPath 'connectome\malecns-v1.0.png'

if (-not [String]::IsNullOrWhiteSpace([string]$manifest.ThumbnailPath)) {
    Add-PackageFile $files (Join-Path $assetRoot 'thumb.png') ([string]$manifest.ThumbnailPath)
}

if ([String]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repositoryRoot 'artifacts\PersonConnectome-Mod.zip'
}
elseif (-not [IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath = Join-Path $repositoryRoot $OutputPath
}

$outputPath = [IO.Path]::GetFullPath($OutputPath)
$stagingParent = Join-Path ([IO.Path]::GetTempPath()) ('person-connectome-package-' + [guid]::NewGuid().ToString('N'))
$packageDirectory = Join-Path $stagingParent 'PersonConnectome'

try {
    New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
    foreach ($file in $files) {
        $destination = Join-Path $packageDirectory $file.RelativePath
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath $file.SourcePath -Destination $destination -Force
    }

    New-Item -ItemType Directory -Path (Split-Path -Parent $outputPath) -Force | Out-Null
    Compress-Archive -Path $packageDirectory -DestinationPath $outputPath -CompressionLevel Optimal -Force

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($outputPath)
    try {
        $expectedEntries = @($files | ForEach-Object { 'PersonConnectome/' + $_.RelativePath.Replace('\', '/') } | Sort-Object)
        $actualEntries = @($archive.Entries | Where-Object { -not $_.FullName.EndsWith('/') } | ForEach-Object { $_.FullName } | Sort-Object)
        if (($expectedEntries -join "`n") -ne ($actualEntries -join "`n")) {
            throw "Package contents do not match the deployed mod file set. Expected $($expectedEntries.Count) files, found $($actualEntries.Count)."
        }
    }
    finally {
        $archive.Dispose()
    }

    Write-Host "Packaged $($files.Count) deployed files to: $outputPath"
    Write-Host "Embedded Git commit: $gitCommit"
}
finally {
    if (Test-Path -LiteralPath $temporaryReadmePath -PathType Leaf) {
        [IO.File]::Delete($temporaryReadmePath)
    }
    if (Test-Path -LiteralPath $stagingParent -PathType Container) {
        Remove-Item -LiteralPath $stagingParent -Recurse -Force
    }
}
