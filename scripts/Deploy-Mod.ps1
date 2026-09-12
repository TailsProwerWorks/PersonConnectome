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
$manifestSourcePath = Join-Path $modSource 'mod.json'
$readmeSourcePath = Join-Path $modSource 'README.txt'

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

$gitCommit = (& git -C $repositoryRoot rev-parse --short=12 HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [String]::IsNullOrWhiteSpace($gitCommit)) {
    throw "Could not determine the current Git commit for the deployed build."
}

$manifest = Get-Content -LiteralPath $manifestSourcePath -Raw | ConvertFrom-Json
$readmeContent = Get-Content -LiteralPath $readmeSourcePath -Raw
if (-not $readmeContent.Contains('{{GIT_COMMIT}}')) {
    throw "The mod README does not contain the {{GIT_COMMIT}} build marker."
}

$readmeContent = $readmeContent.Replace('{{GIT_COMMIT}}', $gitCommit)
$generatedReadmePath = Join-Path ([IO.Path]::GetTempPath()) ('person-connectome-readme-' + [guid]::NewGuid().ToString('N') + '.txt')
[IO.File]::WriteAllText($generatedReadmePath, $readmeContent, [Text.UTF8Encoding]::new($false))
$files = @(
    [pscustomobject]@{ SourcePath = $manifestSourcePath; RelativePath = 'mod.json' }
    [pscustomobject]@{ SourcePath = $generatedReadmePath; RelativePath = 'README.txt' }
)
foreach ($script in $manifest.Scripts) {
    $scriptPath = Join-Path $modSource $script
    if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
        throw "Manifest script was not found: $scriptPath"
    }

    $files += [pscustomobject]@{ SourcePath = $scriptPath; RelativePath = $script }
}

# The game-side loader consumes only the PNG through ModAPI.LoadTexture. The
# raw FLYB/GZip payload remains a repository input for Build-ConnectomeCarrier.
$carrierPath = Join-Path $modSource 'connectome\malecns-v1.0.png'
if (-not (Test-Path -LiteralPath $carrierPath -PathType Leaf)) {
    throw "Connectome carrier was not found: $carrierPath"
}

$files += [pscustomobject]@{ SourcePath = $carrierPath; RelativePath = 'connectome\malecns-v1.0.png' }

if (-not [String]::IsNullOrWhiteSpace($manifest.ThumbnailPath)) {
    $thumbnailPath = Join-Path $modSource $manifest.ThumbnailPath
    if (-not (Test-Path -LiteralPath $thumbnailPath -PathType Leaf)) {
        throw "Manifest thumbnail was not found: $thumbnailPath"
    }

$files += [pscustomobject]@{ SourcePath = $thumbnailPath; RelativePath = $manifest.ThumbnailPath }
}

try {
if ($PSCmdlet.ShouldProcess($targetDirectory, 'deploy Person Connectome mod files')) {
    New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
    $targetConnectomeDirectory = Join-Path $targetDirectory 'connectome'
    New-Item -ItemType Directory -Path $targetConnectomeDirectory -Force | Out-Null

    foreach ($file in $files) {
        $destination = Join-Path $targetDirectory $file.RelativePath
        $destinationDirectory = Split-Path -Parent $destination
        New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
        Copy-Item -LiteralPath $file.SourcePath -Destination $destination -Force
    }

    $staleRawPayload = Join-Path $targetDirectory 'connectome\malecns-v1.0.flyb.gz'
    if (Test-Path -LiteralPath $staleRawPayload -PathType Leaf) {
        if ($PSCmdlet.ShouldProcess($staleRawPayload, 'remove unused raw connectome payload')) {
            Remove-Item -LiteralPath $staleRawPayload -Force
        }
    }

    $verificationFailures = @()
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        $verificationFailures = @()
        foreach ($file in $files) {
            $relativePath = $file.RelativePath
            $destination = Join-Path $targetDirectory $relativePath
            if (-not (Test-Path -LiteralPath $destination -PathType Leaf)) {
                $verificationFailures += "$relativePath (missing)"
                continue
            }

            $sourceHash = (Get-FileHash -LiteralPath $file.SourcePath -Algorithm SHA256).Hash
            $targetHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
            if ($sourceHash -ne $targetHash) {
                $verificationFailures += "$relativePath (source $sourceHash; target $targetHash)"
            }
        }

        if ($verificationFailures.Count -eq 0) {
            break
        }

        if ($attempt -lt 3) {
            Write-Host "Deployment verification found $($verificationFailures.Count) mismatch(es); retrying copy ($attempt/3)..."
            foreach ($file in $files) {
                $destination = Join-Path $targetDirectory $file.RelativePath
                Copy-Item -LiteralPath $file.SourcePath -Destination $destination -Force
            }
            Start-Sleep -Milliseconds 100
        }
    }

    if ($verificationFailures.Count -ne 0) {
        throw "Deployment verification failed after 3 attempts: $($verificationFailures -join '; ')"
    }

    Write-Host "Deployed $($files.Count) used files to: $targetDirectory"
    Write-Host "Embedded Git commit: $gitCommit"
    Write-Host "Verified all $($files.Count) deployed files against their source hashes."
    Write-Host 'The deployed connectome carrier was verified byte-for-byte; the raw build input was not deployed.'
}

}
finally {
    if (Test-Path -LiteralPath $generatedReadmePath -PathType Leaf) {
        [IO.File]::Delete($generatedReadmePath)
    }
}
