[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot '..\deploy\Deploy-Mod.ps1'
$tokens = $parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -ne 0) { throw "Deploy-Mod.ps1 parse errors: $($parseErrors -join '; ')" }
$names = @('Assert-GameReadmeSize','Add-UniquePath','Get-SteamRoots','ConvertFrom-SteamVdfPath','Get-SteamLibraryPaths','Test-PeoplePlaygroundInstall','Resolve-PeoplePlaygroundInstall')
$defs = foreach ($name in $names) {
    $found = $ast.Find({ param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq $name }, $true)
    if ($null -eq $found) { throw "Function not found: $name" }
    $found.Extent.Text
}
Invoke-Expression ($defs -join "`n")
Assert-GameReadmeSize ('a' * 5000)
Assert-GameReadmeSize (([string][char]0x00E9) * 2500)
foreach ($oversize in @(('a' * 5001), (([string][char]0x00E9) * 2501))) {
    try { Assert-GameReadmeSize $oversize; throw 'Oversize README was accepted.' }
    catch { if ($_.Exception.Message -notmatch 'ignores README.txt above 5000 bytes') { throw } }
}
$shippedReadme = Get-Content (Join-Path $PSScriptRoot '../../assets/README.txt') -Raw -Encoding UTF8
Assert-GameReadmeSize ($shippedReadme.Replace('{{GIT_COMMIT}}', '0123456789ab'))
Write-Host 'README byte-limit tests passed (5000-byte boundary, UTF-8, shipped text).'
$fixtureParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$root = Join-Path $fixtureParent ('person-connectome-deploy-test-' + [guid]::NewGuid().ToString('N'))
try {
New-Item -ItemType Directory -Path (Join-Path $root 'modern/steamapps'), (Join-Path $root 'legacy/steamapps'), (Join-Path $root 'game/People Playground_Data/Managed') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $root 'modern-lib'), (Join-Path $root 'legacy-lib') -Force | Out-Null
New-Item -ItemType File -Path (Join-Path $root 'game/People Playground_Data/Managed/Assembly-CSharp.dll') | Out-Null
$modernLibrary = (Join-Path $root 'modern-lib') -replace '\\','\\\\'
$legacyLibrary = (Join-Path $root 'legacy-lib') -replace '\\','\\\\'
Set-Content (Join-Path $root 'modern/steamapps/libraryfolders.vdf') ('"libraryfolders" { "0" { "path" "' + $modernLibrary + '" } }')
Set-Content (Join-Path $root 'legacy/steamapps/libraryfolders.vdf') ('"libraryfolders" { "0" "' + $legacyLibrary + '" }')
function Get-ItemProperty { [pscustomobject]@{ SteamPath = (Join-Path $root 'modern') } }
$registryRoots = @(Get-SteamRoots)
if ($registryRoots -notcontains (Join-Path $root 'modern')) { throw 'SteamPath registry fallback was not discovered.' }
function Get-SteamRoots { @((Join-Path $root 'modern'), (Join-Path $root 'legacy')) }
    $paths = @(Get-SteamLibraryPaths)
    if ($paths -notcontains (Join-Path $root 'modern-lib') -or $paths -notcontains (Join-Path $root 'legacy-lib')) { throw 'Modern or legacy VDF path was not discovered.' }
    $game = [IO.Path]::GetFullPath((Join-Path $root 'game'))
    if ((Resolve-PeoplePlaygroundInstall $game) -ne $game) { throw 'Explicit install did not take precedence.' }
    try { Resolve-PeoplePlaygroundInstall (Join-Path $root 'missing') | Out-Null; throw 'Invalid explicit install was accepted.' } catch { if ($_.Exception.Message -notmatch 'references were not found') { throw } }
    Write-Host 'Deploy discovery tests passed.'
} finally {
    $resolvedRoot = [IO.Path]::GetFullPath($root).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $resolvedParent = $fixtureParent.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if ($resolvedRoot.StartsWith($resolvedParent, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $root)) {
        Remove-Item -LiteralPath $root -Recurse -Force
    } else {
        throw "Refusing to remove fixture path outside the temporary parent: $root"
    }
}
