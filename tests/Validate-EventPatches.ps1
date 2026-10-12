param(
    [string]$GameDir = 'E:\com3d2',
    [string]$PluginDll = (Join-Path $PSScriptRoot '..\bin\Release\COM3D2.Pregnancy.Plugin.dll')
)
$ErrorActionPreference = 'Stop'
Add-Type -LiteralPath (Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll')
$plugin = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($PluginDll)
$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'COM3D2x64_Data\Managed\Assembly-CSharp.dll'))
$first = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'COM3D2x64_Data\Managed\Assembly-CSharp-firstpass.dll'))
try {
    if ($plugin.MainModule.Types.Name -contains 'Patch_SetCommandCallback') { throw 'Old command hook still present' }
    $count = 0
    foreach ($patch in $plugin.MainModule.Types | Where-Object Name -Like 'Patch_Conception*') {
        $attribute = $patch.CustomAttributes | Where-Object {$_.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch'}
        $typeName = $attribute.ConstructorArguments[0].Value.FullName
        $methodName = $attribute.ConstructorArguments[1].Value
        $type = $game.MainModule.GetType($typeName)
        if (!$type) { $type = $first.MainModule.GetType($typeName) }
        if (!$type) { throw "Target type missing: $typeName" }
        $targets = @($type.Methods | Where-Object Name -EQ $methodName)
        if ($targets.Count -ne 1) { throw "Ambiguous/missing target: $typeName.$methodName" }
        $target = $targets[0]
        foreach ($method in $patch.Methods | Where-Object {$_.Name -in @('Prefix','Postfix','Finalizer')}) {
            foreach ($parameter in $method.Parameters) {
                $expected = switch -Regex ($parameter.Name) {
                    '^__instance$' { $type.FullName }
                    '^__result$' { $target.ReturnType.FullName }
                    '^__(\d+)$' { $target.Parameters[[int]$Matches[1]].ParameterType.FullName }
                    '^__state$' { $null }
                    default { throw "Unexpected patch argument: $($parameter.Name)" }
                }
                if ($expected -and $parameter.ParameterType.FullName -ne $expected) {
                    throw "Patch argument mismatch: $($patch.Name).$($parameter.Name)"
                }
            }
        }
        Write-Output "OK $($patch.Name) -> $typeName.$methodName"
        $count++
    }
    if ($count -ne 7) { throw "Expected 7 event patches, found $count" }
    $fast = $game.MainModule.GetType('ScriptManagerFast/BaseKagManagerFast')
    $parser = $game.MainModule.GetType('ScriptManagerFast/KagParserFast')
    foreach ($field in @('m_BaseKagMgr','m_KagParser','m_nNowIndex')) {
        if ($fast.Fields.Name -notcontains $field) { throw "Fast field missing: $field" }
    }
    if ($parser.Fields.Name -notcontains 'm_listLabel') { throw 'Fast label field missing' }
    Write-Output 'PASS: target signatures, fast fields and removal of old hook verified against local game assemblies.'
}
finally { $plugin.Dispose(); $game.Dispose(); $first.Dispose() }
