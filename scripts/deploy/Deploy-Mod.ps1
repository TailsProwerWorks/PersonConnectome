[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $GameInstall,
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [switch] $NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$modSource = Join-Path $repositoryRoot 'src'
$assetRoot = Join-Path $repositoryRoot 'assets'
$modProject = Join-Path $modSource 'PersonConnectome.Mod.csproj'

function Assert-GameReadmeSize([string] $Content) {
    # ModLoader.LoadModAt ignores README.txt above 5000 bytes (not 5 KiB).
    # Match the UTF-8 without BOM encoding used for the deployed file.
    $byteCount = [Text.Encoding]::UTF8.GetByteCount($Content)
    if ($byteCount -gt 5000) {
        throw "The generated mod README is $byteCount UTF-8 bytes; People Playground ignores README.txt above 5000 bytes. Shorten assets/README.txt before deploying."
    }
}

function Add-UniquePath([System.Collections.Generic.List[string]] $Paths, [string] $Path) {
    if ([String]::IsNullOrWhiteSpace($Path)) {
        return
    }

    try {
        $normalized = [IO.Path]::GetFullPath($Path)
    } catch {
        return
    }

    if (-not $Paths.Contains($normalized) -and (Test-Path -LiteralPath $normalized -PathType Container)) {
        [void]$Paths.Add($normalized)
    }
}

function Get-SteamRoots {
    $roots = [System.Collections.Generic.List[string]]::new()
    $registryKeys = @(
        'HKCU:\Software\Valve\Steam',
        'HKLM:\SOFTWARE\Valve\Steam',
        'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam',
        'HKCU:\Software\WOW6432Node\Valve\Steam'
    )

    foreach ($registryKey in $registryKeys) {
        try {
            $properties = Get-ItemProperty -LiteralPath $registryKey -ErrorAction Stop
            foreach ($propertyName in @('InstallPath', 'SteamPath')) {
                $property = $properties.PSObject.Properties[$propertyName]
                if ($null -ne $property) { Add-UniquePath $roots ([string]$property.Value) }
            }
        } catch {
            # Registry keys are optional; Steam may be installed through another view.
        }
    }

    foreach ($programFilesRoot in @(
        [Environment]::GetFolderPath('ProgramFilesX86'),
        [Environment]::GetFolderPath('ProgramFiles')
    )) {
        if (-not [String]::IsNullOrWhiteSpace($programFilesRoot)) { Add-UniquePath $roots (Join-Path $programFilesRoot 'Steam') }
    }

    return $roots.ToArray()
}

function ConvertFrom-SteamVdfPath([string] $Value) {
    return $Value.Replace('\\', '\').Replace('\"', '"')
}

function Get-SteamLibraryPaths {
    $libraries = [System.Collections.Generic.List[string]]::new()
    $steamRoots = @(Get-SteamRoots)

    foreach ($steamRoot in $steamRoots) {
        Add-UniquePath $libraries $steamRoot
        $libraryFile = Join-Path $steamRoot 'steamapps\libraryfolders.vdf'
        if (-not (Test-Path -LiteralPath $libraryFile -PathType Leaf)) {
            continue
        }

        try {
            $vdf = [IO.File]::ReadAllText($libraryFile)
            foreach ($match in [regex]::Matches($vdf, '(?im)"path"\s+"((?:\\.|[^"])*)"')) {
                Add-UniquePath $libraries (ConvertFrom-SteamVdfPath $match.Groups[1].Value)
            }
            foreach ($match in [regex]::Matches($vdf, '(?im)"\d+"\s+"((?:\\.|[^"])*)"')) {
                Add-UniquePath $libraries (ConvertFrom-SteamVdfPath $match.Groups[1].Value)
            }
        } catch {
            Write-Warning "Could not read Steam library file '$libraryFile': $($_.Exception.Message)"
        }
    }

    return $libraries.ToArray()
}

function Test-PeoplePlaygroundInstall([string] $Path) {
    $assemblyPath = Join-Path $Path 'People Playground_Data\Managed\Assembly-CSharp.dll'
    return Test-Path -LiteralPath $assemblyPath -PathType Leaf
}

function Resolve-PeoplePlaygroundInstall([string] $RequestedPath) {
    if (-not [String]::IsNullOrWhiteSpace($RequestedPath)) {
        $explicitPath = [IO.Path]::GetFullPath($RequestedPath)
        if (Test-PeoplePlaygroundInstall $explicitPath) {
            return $explicitPath
        }

        throw "People Playground references were not found under '$explicitPath'. Expected People Playground_Data\Managed\Assembly-CSharp.dll."
    }

    $libraries = @(Get-SteamLibraryPaths)
    foreach ($library in $libraries) {
        $candidate = Join-Path $library 'steamapps\common\People Playground'
        if (Test-PeoplePlaygroundInstall $candidate) {
            Write-Host "Auto-detected People Playground at: $candidate"
            return $candidate
        }
    }

    $searched = if ($libraries.Count -gt 0) { $libraries -join '; ' } else { 'no Steam libraries were found' }
    throw "Could not find a People Playground installation through Steam ($searched). Pass -GameInstall with the game's install directory."
}

$GameInstall = Resolve-PeoplePlaygroundInstall $GameInstall
$managedDirectory = Join-Path $GameInstall 'People Playground_Data\Managed'
$targetDirectory = Join-Path $GameInstall 'Mods\PersonConnectome'
$manifestSourcePath = Join-Path $modSource 'mod.json'
$readmeSourcePath = Join-Path $assetRoot 'README.txt'

if (-not (Test-Path -LiteralPath $modProject -PathType Leaf)) {
    throw "Mod project was not found: $modProject"
}

if (-not (Test-Path -LiteralPath (Join-Path $managedDirectory 'Assembly-CSharp.dll') -PathType Leaf)) {
    throw "People Playground references were not found under '$managedDirectory'. Pass -GameInstall with the game's install directory."
}

if (-not $NoBuild -and -not $WhatIfPreference) {
    Write-Host "Building the mod against the installed People Playground references..."
    & dotnet build $modProject --configuration $Configuration "-p:PeoplePlaygroundInstall=$GameInstall"
    if ($LASTEXITCODE -ne 0) {
        throw "The mod build failed with exit code $LASTEXITCODE."
    }
}

$gitCommit = (& git -C $repositoryRoot rev-parse --short=12 HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [String]::IsNullOrWhiteSpace($gitCommit)) {
    throw "Could not determine the current Git commit for the deployed build."
}
elseif ($WhatIfPreference -and -not $NoBuild) { Write-Host "WhatIf: would build the mod with PeoplePlaygroundInstall=$GameInstall" }

$manifest = Get-Content -LiteralPath $manifestSourcePath -Raw | ConvertFrom-Json
$readmeContent = Get-Content -LiteralPath $readmeSourcePath -Raw -Encoding UTF8
if (-not $readmeContent.Contains('{{GIT_COMMIT}}')) {
    throw "The mod README does not contain the {{GIT_COMMIT}} build marker."
}

$readmeContent = $readmeContent.Replace('{{GIT_COMMIT}}', $gitCommit)
Assert-GameReadmeSize $readmeContent
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
$carrierPath = Join-Path $assetRoot 'connectome\malecns-v1.0.png'
if (-not (Test-Path -LiteralPath $carrierPath -PathType Leaf)) {
    throw "Connectome carrier was not found: $carrierPath"
}

$files += [pscustomobject]@{ SourcePath = $carrierPath; RelativePath = 'connectome\malecns-v1.0.png' }

if (-not [String]::IsNullOrWhiteSpace($manifest.ThumbnailPath)) {
    $thumbnailPath = Join-Path $assetRoot 'thumb.png'
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
