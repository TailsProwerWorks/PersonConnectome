# Requires PowerShell 7. Compiles the repository sources with precisely the
# reference list recorded by the installed People Playground mod compiler.
# It intentionally does not invoke PPGModCompiler.Program.CompileMod: that is
# a private server-side protocol method which can write replies and consult
# compiler state. Roslyn gives the same binding check without touching the
# game, installed mods, compiler configuration, or network.
[CmdletBinding()]
param(
    [string] $GameInstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$modRoot = Join-Path $repositoryRoot 'src'

function Test-GameInstall([string] $Path) {
    return Test-Path -LiteralPath (Join-Path $Path 'People Playground_Data\Managed\Assembly-CSharp.dll') -PathType Leaf
}

function Resolve-GameInstall([string] $RequestedPath) {
    if (-not [String]::IsNullOrWhiteSpace($RequestedPath)) {
        $resolved = [IO.Path]::GetFullPath($RequestedPath)
        if (Test-GameInstall $resolved) { return $resolved }
        throw "People Playground references were not found under '$resolved'. Pass -GameInstall with the game install directory."
    }

    $steamRoots = [System.Collections.Generic.List[string]]::new()
    foreach ($registryKey in @('HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam')) {
        try {
            $properties = Get-ItemProperty -LiteralPath $registryKey -ErrorAction Stop
            foreach ($name in @('InstallPath', 'SteamPath')) {
                $value = $properties.PSObject.Properties[$name]
                if ($null -ne $value -and (Test-Path -LiteralPath $value.Value -PathType Container)) { [void] $steamRoots.Add([IO.Path]::GetFullPath($value.Value)) }
            }
        } catch { }
    }
    foreach ($programFiles in @([Environment]::GetFolderPath('ProgramFilesX86'), [Environment]::GetFolderPath('ProgramFiles'))) {
        $candidate = Join-Path $programFiles 'Steam'
        if (Test-Path -LiteralPath $candidate -PathType Container) { [void] $steamRoots.Add([IO.Path]::GetFullPath($candidate)) }
    }

    $libraries = [System.Collections.Generic.List[string]]::new()
    foreach ($steamRoot in $steamRoots | Select-Object -Unique) {
        [void] $libraries.Add($steamRoot)
        $vdf = Join-Path $steamRoot 'steamapps\libraryfolders.vdf'
        if (-not (Test-Path -LiteralPath $vdf -PathType Leaf)) { continue }
        foreach ($match in [regex]::Matches([IO.File]::ReadAllText($vdf), '(?im)"path"\s+"((?:\\.|[^"])*)"')) {
            $library = $match.Groups[1].Value.Replace('\\', '\').Replace('\"', '"')
            if (Test-Path -LiteralPath $library -PathType Container) { [void] $libraries.Add([IO.Path]::GetFullPath($library)) }
        }
    }

    foreach ($library in $libraries | Select-Object -Unique) {
        $candidate = Join-Path $library 'steamapps\common\People Playground'
        if (Test-GameInstall $candidate) {
            Write-Host "Auto-detected People Playground at: $candidate"
            return [IO.Path]::GetFullPath($candidate)
        }
    }

    throw 'Could not find a People Playground installation through Steam. Pass -GameInstall with the game install directory.'
}

function Get-CompilerReferences([string] $InstallPath) {
    $instructionsPath = Join-Path $InstallPath 'ppgModCompiler\last_instructions'
    if (-not (Test-Path -LiteralPath $instructionsPath -PathType Leaf)) {
        throw "The installed compiler instruction record was not found: $instructionsPath"
    }

    try {
        $instructions = Get-Content -LiteralPath $instructionsPath -Raw | ConvertFrom-Json
    } catch {
        throw "Could not parse installed compiler instruction record '$instructionsPath': $($_.Exception.Message)"
    }

    if ($instructions.RejectShadyCode -ne $true) {
        throw "The installed compiler instruction record does not have RejectShadyCode enabled. Refusing to claim rejection-compatible compilation."
    }

    $managedRoot = [IO.Path]::GetFullPath((Join-Path $InstallPath 'People Playground_Data\Managed')).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $references = [System.Collections.Generic.List[string]]::new()
    foreach ($recordedPath in @($instructions.AssemblyReferenceLocations)) {
        if ($recordedPath -isnot [string] -or [String]::IsNullOrWhiteSpace($recordedPath)) {
            throw 'The installed compiler instruction record contains an invalid assembly reference.'
        }

        # Treat paths from last_instructions as data: accept only existing DLLs
        # directly beneath this installation's Managed directory.
        $fullPath = [IO.Path]::GetFullPath($recordedPath)
        $parent = [IO.Path]::GetDirectoryName($fullPath)
        if (-not [String]::Equals($parent, $managedRoot, [StringComparison]::OrdinalIgnoreCase) -or
            -not [String]::Equals([IO.Path]::GetExtension($fullPath), '.dll', [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            throw "The installed compiler instruction record contains a reference outside this game's Managed directory or a missing file: $recordedPath"
        }

        if (@($references | Where-Object { [String]::Equals($_, $fullPath, [StringComparison]::OrdinalIgnoreCase) }).Count -eq 0) {
            [void] $references.Add($fullPath)
        }
    }

    if ($references.Count -eq 0) {
        throw 'The installed compiler instruction record did not contain assembly references.'
    }

    return $references.ToArray()
}

$GameInstall = Resolve-GameInstall $GameInstall
$references = @(Get-CompilerReferences $GameInstall)
$manifestPath = Join-Path $modRoot 'mod.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$sourcePaths = [System.Collections.Generic.List[string]]::new()
foreach ($script in @($manifest.Scripts)) {
    if ($script -isnot [string] -or [String]::IsNullOrWhiteSpace($script) -or [IO.Path]::IsPathRooted($script)) {
        throw "The repository manifest contains an invalid script path: $script"
    }

    $sourcePath = [IO.Path]::GetFullPath((Join-Path $modRoot $script))
    if (-not $sourcePath.StartsWith($modRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "The repository manifest script is missing or outside src: $script"
    }

    [void] $sourcePaths.Add($sourcePath)
}

# Keep the documented rejection guard part of this preflight. It reads only
# repository sources and catches forbidden syntax before binding against game APIs.
& (Join-Path $PSScriptRoot 'Test-ModSourceSafety.ps1')
if (-not $?) { throw 'The Shady Code Rejection source guard failed.' }

$compilerDirectory = Join-Path $GameInstall 'ppgModCompiler'
$compilerAssemblyPath = Join-Path $compilerDirectory 'PPGModCompiler.dll'
$codeAnalysisPath = Join-Path $compilerDirectory 'Microsoft.CodeAnalysis.dll'
$csharpPath = Join-Path $compilerDirectory 'Microsoft.CodeAnalysis.CSharp.dll'
if (-not (Test-Path -LiteralPath $compilerAssemblyPath -PathType Leaf) -or -not (Test-Path -LiteralPath $codeAnalysisPath -PathType Leaf) -or -not (Test-Path -LiteralPath $csharpPath -PathType Leaf)) {
    throw "The installed compiler Roslyn assemblies were not found under '$compilerDirectory'."
}

# Load the same Roslyn binaries shipped with the game's compiler. Loading may
# return an already-loaded compatible assembly; it never executes mod source.
try {
    [void] [Reflection.Assembly]::LoadFrom($codeAnalysisPath)
    [void] [Reflection.Assembly]::LoadFrom($csharpPath)
    $compilerAssembly = [Reflection.Assembly]::LoadFrom($compilerAssemblyPath)
} catch {
    throw "Could not load the installed compiler Roslyn assemblies: $($_.Exception.Message)"
}

$syntaxTrees = [System.Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]]::new()
foreach ($sourcePath in $sourcePaths) {
    [void] $syntaxTrees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($sourcePath), $null, $sourcePath))
}
$metadataReferences = [System.Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]]::new()
foreach ($referencePath in $references) {
    [void] $metadataReferences.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($referencePath))
}
$options = [Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new(
    [Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary)
$compilation = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create(
    'PersonConnectome.GameCompilerPreflight', $syntaxTrees, $metadataReferences, $options)

$output = [IO.MemoryStream]::new()
$emit = $compilation.Emit($output)
$output.Dispose()
$errors = @($emit.Diagnostics | Where-Object { $_.Severity -eq [Microsoft.CodeAnalysis.DiagnosticSeverity]::Error })
if ($errors.Count -ne 0) {
    throw ("People Playground compiler-reference preflight failed:`n" + (($errors | ForEach-Object { $_.ToString() }) -join "`n"))
}

$scannerType = $compilerAssembly.GetType('ScanUtils', $false)
if ($null -eq $scannerType) {
    throw 'The installed compiler does not expose the ScanUtils semantic scanner type.'
}
$scannerFlags = [Reflection.BindingFlags]'Public,Static'
$lowRiskScanner = $scannerType.GetMethod('ScanTreeLowRisk', $scannerFlags)
$highRiskScanner = $scannerType.GetMethod('ScanTreeHighRisk', $scannerFlags)
if ($null -eq $lowRiskScanner -or $null -eq $highRiskScanner -or
    $lowRiskScanner.GetParameters().Count -ne 1 -or $highRiskScanner.GetParameters().Count -ne 1) {
    throw 'The installed compiler semantic scanner API is unavailable or has an unexpected signature.'
}

foreach ($tree in $syntaxTrees) {
    $model = $compilation.GetSemanticModel($tree)
    foreach ($scanner in @($lowRiskScanner, $highRiskScanner)) {
        try {
            [void] $scanner.Invoke($null, [object[]] @($model))
        } catch [Reflection.TargetInvocationException] {
            $detail = if ($null -ne $_.Exception.InnerException) { $_.Exception.InnerException.Message } else { $_.Exception.Message }
            throw "Installed compiler semantic scanner $($scanner.DeclaringType.FullName).$($scanner.Name) rejected '$($tree.FilePath)': $detail"
        } catch {
            throw "Installed compiler semantic scanner $($scanner.DeclaringType.FullName).$($scanner.Name) could not run for '$($tree.FilePath)': $($_.Exception.Message)"
        }
    }
}

Write-Host "PASS compiled $($sourcePaths.Count) repository manifest scripts against $($references.Count) exact references from the installed People Playground compiler instruction record."
Write-Host 'PASS installed compiler semantic scanners ScanUtils.ScanTreeLowRisk and ScanUtils.ScanTreeHighRisk accepted every source semantic model; full loader not run.'
