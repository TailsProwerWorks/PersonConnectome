[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $GameInstall = 'C:\Program Files (x86)\Steam\steamapps\common\People Playground',
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [switch] $NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$modSource = Join-Path $repositoryRoot 'Mod'
$modProject = Join-Path $modSource 'PersonConnectome.Mod.csproj'
$managedDirectory = Join-Path $GameInstall 'People Playground_Data\Managed'
$targetDirectory = Join-Path $GameInstall 'Mods\PersonConnectome'

if (-not (Test-Path -LiteralPath $modProject -PathType Leaf)) {
    throw "Mod project was not found: $modProject"
}

if (-not (Test-Path -LiteralPath (Join-Path $managedDirectory 'Assembly-CSharp.dll') -PathType Leaf)) {
    throw "People Playground references were not found under '$managedDirectory'. Pass -GameInstall with the game's install directory."
}

if (-not $NoBuild) {
    Write-Host "Building the mod against the installed People Playground references..."
    & dotnet build $modProject --configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "The mod build failed with exit code $LASTEXITCODE."
    }
}

$manifest = Get-Content -LiteralPath (Join-Path $modSource 'mod.json') -Raw | ConvertFrom-Json
$files = @(
    Get-Item -LiteralPath (Join-Path $modSource 'mod.json')
)
foreach ($script in $manifest.Scripts) {
    $scriptPath = Join-Path $modSource $script
    if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
        throw "Manifest script was not found: $scriptPath"
    }

    $files += Get-Item -LiteralPath $scriptPath
}

# The game-side loader consumes only the PNG through ModAPI.LoadTexture. The
# raw FLYB/GZip payload remains a repository input for Build-ConnectomeCarrier.
$carrierPath = Join-Path $modSource 'connectome\malecns-v1.0.png'
if (-not (Test-Path -LiteralPath $carrierPath -PathType Leaf)) {
    throw "Connectome carrier was not found: $carrierPath"
}

$files += Get-Item -LiteralPath $carrierPath

if (-not [String]::IsNullOrWhiteSpace($manifest.ThumbnailPath)) {
    $thumbnailPath = Join-Path $modSource $manifest.ThumbnailPath
    if (-not (Test-Path -LiteralPath $thumbnailPath -PathType Leaf)) {
        throw "Manifest thumbnail was not found: $thumbnailPath"
    }

    $files += Get-Item -LiteralPath $thumbnailPath
}

if ($PSCmdlet.ShouldProcess($targetDirectory, 'deploy Person Connectome mod files')) {
    New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
    $targetConnectomeDirectory = Join-Path $targetDirectory 'connectome'
    New-Item -ItemType Directory -Path $targetConnectomeDirectory -Force | Out-Null

    foreach ($file in $files) {
        $relativePath = $file.FullName.Substring($modSource.Length).TrimStart('\', '/')
        $destination = Join-Path $targetDirectory $relativePath
        $destinationDirectory = Split-Path -Parent $destination
        New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
    }

    $staleRawPayload = Join-Path $targetDirectory 'connectome\malecns-v1.0.flyb.gz'
    if (Test-Path -LiteralPath $staleRawPayload -PathType Leaf) {
        if ($PSCmdlet.ShouldProcess($staleRawPayload, 'remove unused raw connectome payload')) {
            Remove-Item -LiteralPath $staleRawPayload -Force
        }
    }

    foreach ($file in $files) {
        $relativePath = $file.FullName.Substring($modSource.Length).TrimStart('\', '/')
        $destination = Join-Path $targetDirectory $relativePath
        $sourceHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        $targetHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
        if ($sourceHash -ne $targetHash) {
            throw "Hash verification failed for '$relativePath'."
        }
    }

    Write-Host "Deployed $($files.Count) used files to: $targetDirectory"
    Write-Host 'The deployed connectome carrier was verified byte-for-byte; the raw build input was not deployed.'
}
