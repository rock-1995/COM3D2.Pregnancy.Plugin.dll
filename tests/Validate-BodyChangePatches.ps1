param([string]$GameDir='E:\com3d2', [Parameter(Mandatory=$true)][string]$PluginDll)
$ErrorActionPreference='Stop'
Add-Type -LiteralPath (Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll')
$plugin=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($PluginDll)
$game=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'COM3D2x64_Data\Managed\Assembly-CSharp.dll'))
try {
    $count=0
    foreach($patch in $plugin.MainModule.Types | Where-Object Name -Like 'Patch_BodyChange*') {
        $attribute=$patch.CustomAttributes | Where-Object {$_.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch'}
        $type=$game.MainModule.GetType($attribute.ConstructorArguments[0].Value.FullName)
        $target=@($type.Methods | Where-Object Name -EQ $attribute.ConstructorArguments[1].Value)
        if($target.Count -ne 1){throw "Missing/ambiguous method for $($patch.Name)"}
        foreach($method in $patch.Methods | Where-Object Name -In @('Prefix','Postfix')) {
            foreach($parameter in $method.Parameters) {
                $expected=switch($parameter.Name) {
                    '__instance' {$type.FullName}
                    '__result' {$target[0].ReturnType.FullName}
                    default {($target[0].Parameters | Where-Object Name -EQ $parameter.Name).ParameterType.FullName}
                }
                $actual=$parameter.ParameterType.FullName.TrimEnd('&')
                if(!$expected -or $actual -ne $expected){throw "Signature mismatch: $($patch.Name).$($parameter.Name)"}
            }
        }
        Write-Output "OK $($patch.Name) -> $($type.FullName).$($target[0].Name)"
        $count++
    }
    if($count -ne 4){throw "Expected 4 new patches, got $count"}
    $body=$plugin.MainModule.GetType('COM3D2.Pregnancy.Plugin.BodyChanges')
    foreach($method in $body.Methods | Where-Object HasBody) {
        foreach($instruction in $method.Body.Instructions) {
            if($instruction.OpCode.Code -eq 'Stfld' -and $instruction.Operand.DeclaringType.FullName -eq 'MaidProp'){throw 'Runtime writes a persistent MaidProp field'}
            if($instruction.Operand -is [Mono.Cecil.MethodReference] -and $instruction.Operand.Name -in @('SetProp','PresetSet','SerializeProp','SerializeMultiColor','SetPartsColor')){throw 'Runtime uses persistent appearance mutation API'}
        }
    }
    Write-Output 'PASS: 4 Harmony targets verified against installed game; runtime contains no persistent appearance writes.'
} finally {$plugin.Dispose();$game.Dispose()}
