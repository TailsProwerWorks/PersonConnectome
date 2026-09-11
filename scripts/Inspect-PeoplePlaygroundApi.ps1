[CmdletBinding()]
param(
    [string] $GameInstall = 'C:\Program Files (x86)\Steam\steamapps\common\People Playground'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$assemblyPath = Join-Path $GameInstall 'People Playground_Data\Managed\Assembly-CSharp.dll'
$assembly = [Reflection.Assembly]::LoadFrom($assemblyPath)
$typeNames = @('PersonBehaviour', 'LimbBehaviour', 'CirculationBehaviour', 'PhysicalBehaviour')

foreach ($typeName in $typeNames) {
    $type = $assembly.GetType($typeName, $false)
    if ($null -eq $type) {
        Write-Host "MISSING $typeName"
        continue
    }

    Write-Host "[$typeName]"
    $members = $type.GetMembers([Reflection.BindingFlags]'Public,Instance,Static') |
        Where-Object { $_.MemberType -in @('Field', 'Property', 'Method') } |
        Sort-Object Name, MemberType
    foreach ($member in $members) {
        if ($member.Name -match '(?i)liquid|fluid|material|poison|acid|blood|chemical|status|effect|wet|water|concentration|temperature|charge|fire|audio|sound') {
            Write-Host ("{0} {1}" -f $member.MemberType, $member.Name)
        }
    }
}

Write-Host '[Declared member types]'
foreach ($typeName in $typeNames) {
    $type = $assembly.GetType($typeName, $false)
    Write-Host "[$typeName]"
    foreach ($member in $type.GetMembers([Reflection.BindingFlags]'Public,Instance,Static') |
        Where-Object { $_.DeclaringType -eq $type -and $_.MemberType -in @('Field', 'Property') } |
        Sort-Object Name, MemberType) {
        $memberType = if ($member.MemberType -eq 'Field') { $member.FieldType.FullName } else { $member.PropertyType.FullName }
        Write-Host ("{0}: {1}" -f $member.Name, $memberType)
    }
}

Write-Host '[Focused member types]'
foreach ($spec in @(
        @{ Type = 'CirculationBehaviour'; Member = 'LiquidDistribution' },
        @{ Type = 'LimbBehaviour'; Member = 'LimbStatus' },
        @{ Type = 'LimbBehaviour'; Member = 'BloodLiquidType' },
        @{ Type = 'PersonBehaviour'; Member = 'ChosenMaterial' })) {
    $type = $assembly.GetType($spec.Type, $false)
    $member = $type.GetMember($spec.Member, [Reflection.BindingFlags]'Public,Instance,Static') | Select-Object -First 1
    if ($null -ne $member) {
        $memberType = if ($member.MemberType -eq 'Field') { $member.FieldType } else { $member.PropertyType }
        Write-Host ("{0}.{1}: {2}" -f $spec.Type, $spec.Member, $memberType.FullName)
    }
}

Write-Host '[Related public types]'
$relatedTypes = $assembly.GetTypes() |
    Where-Object { $_.Name -match '(?i)liquid|fluid|material|poison|acid|blood|chemical|status|effect|substance|odor|smell|scent|audio|sound|vision|raycast' } |
    Sort-Object FullName
foreach ($type in $relatedTypes) {
    Write-Host $type.FullName
    $members = $type.GetMembers([Reflection.BindingFlags]'Public,Instance,Static') |
        Where-Object { $_.MemberType -in @('Field', 'Property', 'Method') } |
        Sort-Object Name, MemberType
    foreach ($member in $members) {
        if ($member.Name -notmatch '^get_|^set_|^op_') {
            Write-Host ("  {0} {1}" -f $member.MemberType, $member.Name)
        }
    }
}

Write-Host '[Liquid type]'
$liquidType = $assembly.GetType('Liquid', $false)
if ($null -ne $liquidType) {
    foreach ($member in $liquidType.GetMembers([Reflection.BindingFlags]'Public,Instance,Static') | Where-Object { $_.MemberType -in @('Field', 'Property', 'Method') } | Sort-Object Name, MemberType) {
        if ($member.Name -notmatch '^get_|^set_|^op_') {
            $memberType = if ($member.MemberType -eq 'Field') { $member.FieldType.FullName } elseif ($member.MemberType -eq 'Property') { $member.PropertyType.FullName } else { $member.ReturnType.FullName }
            Write-Host ("{0} : {1}" -f $member.ToString(), $memberType)
        }
    }

    Write-Host '[Registered liquid identities]'
    $getAll = $liquidType.GetMethod('GetAll', [Reflection.BindingFlags]'Public,Static')
    $getIdentity = $liquidType.GetMethod('GetIdentity', [Reflection.BindingFlags]'Public,Static')
    if ($null -ne $getAll) {
        try {
            foreach ($liquid in $getAll.Invoke($null, $null)) {
                $identity = $getIdentity.Invoke($null, [object[]]@($liquid))
                Write-Host ("{0} | {1}" -f $identity, $liquid.GetDisplayName())
            }
        } catch {
            Write-Host ("Registry unavailable outside the running game: {0}" -f $_.Exception.GetType().Name)
        }
    }
}

Write-Host '[Liquid amount type]'
$distributionField = $assembly.GetType('CirculationBehaviour', $false).GetField('LiquidDistribution')
$amountType = $distributionField.FieldType.GetGenericArguments()[1]
Write-Host ("Runtime type: {0}" -f $amountType.FullName)
Write-Host ("Base: {0}; ValueType: {1}; IsEnum: {2}" -f $amountType.BaseType.FullName, $amountType.IsValueType, $amountType.IsEnum)
if ($null -ne $amountType) {
    foreach ($member in $amountType.GetMembers([Reflection.BindingFlags]'Public,NonPublic,Instance,Static') | Where-Object { $_.MemberType -in @('Field', 'Property', 'Method') } | Sort-Object Name, MemberType) {
        if ($member.Name -notmatch '^get_|^set_|^op_') {
            Write-Host ("{0}" -f $member.ToString())
        }
    }
}

Write-Host '[Declared public members]'
foreach ($typeName in $typeNames) {
    $type = $assembly.GetType($typeName, $false)
    Write-Host "[$typeName]"
    foreach ($member in $type.GetMembers([Reflection.BindingFlags]'Public,Instance,Static') |
        Where-Object { $_.DeclaringType -eq $type -and $_.MemberType -in @('Field', 'Property', 'Method') } |
        Sort-Object Name, MemberType) {
        if ($member.Name -notmatch '^get_|^set_|^op_') {
            Write-Host ("{0} {1}" -f $member.MemberType, $member.Name)
        }
    }
}
