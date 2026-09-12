# Requires PowerShell 7, whose bundled Roslyn parser supplies syntax tokens.
# Offline guard for https://wiki.studiominus.nl/details/shadyCodeRejection.html
# This does not replace loading the mod with rejection enabled in the game.
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$forbidden = @('InteropServices','Diagnostics','Http','CodeDom','Application','Quit','UnityWebRequest','TextReader','TextWriter','BinaryReader','BinaryWriter','StreamReader','StreamWriter','StringReader','StringWriter','FileStream','IsolatedStorageFileStream','NetworkStream','PipeStream','UserPreferenceManager','WebRequest','WebClient','WebSocket','Socket','Steamworks','Process','DllImport','LoadFile','ReadFile','WWW','AppDomain','AssemblyBuilder','FromFile','OpenURL','LoadURL','RejectShadyCode','CreateType','File','FileInfo','Directory','DirectoryInfo','Assembly')
function Find-RejectedSyntax([string] $Source) {
    $syntax = [Microsoft.CodeAnalysis.CSharp.SyntaxFactory]::ParseCompilationUnit($Source, 0, $null)
    foreach ($error in $syntax.GetDiagnostics()) {
        if ($error.Severity.ToString() -eq 'Error') { "syntax: $error" }
    }
    foreach ($token in $syntax.DescendantTokens()) {
        if ($token.RawKind -eq [int][Microsoft.CodeAnalysis.CSharp.SyntaxKind]::ExternKeyword -or
            ($token.RawKind -eq [int][Microsoft.CodeAnalysis.CSharp.SyntaxKind]::IdentifierToken -and $forbidden -ccontains $token.ValueText)) {
            "forbidden token: $($token.ValueText)"
        }
    }
    foreach ($node in $syntax.DescendantNodes()) {
        if ($node -isnot [Microsoft.CodeAnalysis.CSharp.Syntax.UsingDirectiveSyntax]) { continue }
        if ($null -ne $node.Alias) { 'aliased using directive' }
        $name = $node.Name.ToString().Replace('global::', '').Replace(' ', '')
        if ($name -match '^(System\.(Security|Web)|UnityEngine\.Networking|Steamworks)(\.|$)') { "forbidden using: $name" }
    }
}
# Check the guard itself, including escaped identifiers and namespace prefixes.
foreach ($bad in @('using IO = System.IO;', 'using System.Security.Cryptography;', 'class C { extern void M(); }', 'class C { void M() { System.IO.F\u0069le.Delete("x"); } }')) {
    if (@(Find-RejectedSyntax $bad).Count -eq 0) { throw "Guard missed rejected syntax: $bad" }
}
if (@(Find-RejectedSyntax 'class C { string x = "File Assembly Process"; /* Diagnostics */ }').Count -ne 0) { throw 'Guard incorrectly scanned comments or strings as identifiers.' }
$modRoot = Join-Path $PSScriptRoot '../Mod'
$manifest = Get-Content -LiteralPath (Join-Path $modRoot 'mod.json') -Raw | ConvertFrom-Json
$failures = @()
foreach ($script in $manifest.Scripts) {
    $source = Get-Content -LiteralPath (Join-Path $modRoot $script) -Raw
    foreach ($finding in @(Find-RejectedSyntax $source)) { $failures += "${script}: $finding" }
}
if ($failures.Count -ne 0) { throw ($failures -join "`n") }
Write-Host "PASS documented Shady Code Rejection syntax rules for all $($manifest.Scripts.Count) manifest scripts (plus guard self-tests)."
