# Run with Windows PowerShell (powershell.exe): the installed legacy Harmony
# library targets .NET Framework when executing its reflection helpers.
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
        if($attribute.ConstructorArguments.Count -gt 2) {
            $signature=@($attribute.ConstructorArguments[2].Value | ForEach-Object {$_.Value.FullName}) -join '|'
            $target=@($target | Where-Object {(@($_.Parameters | ForEach-Object {$_.ParameterType.FullName}) -join '|') -eq $signature})
        }
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
    if($count -ne 5){throw "Expected 5 body-change patches, got $count"}
    $body=$plugin.MainModule.GetType('COM3D2.Pregnancy.Plugin.BodyChanges')
    foreach($method in $body.Methods | Where-Object HasBody) {
        foreach($instruction in $method.Body.Instructions) {
            if($instruction.OpCode.Code -eq 'Stfld' -and $instruction.Operand.DeclaringType.FullName -eq 'MaidProp'){throw 'Runtime writes a persistent MaidProp field'}
            if($instruction.Operand -is [Mono.Cecil.MethodReference] -and $instruction.Operand.Name -in @('SetProp','PresetSet','SerializeProp','SerializeMultiColor','SetPartsColor')){throw 'Runtime uses persistent appearance mutation API'}
        }
    }
    $nativeRender = $game.MainModule.GetType('InfinityColorTextureCache').Methods | Where-Object {
        $_.Name -eq 'UpdateTexture' -and (@($_.Parameters | ForEach-Object {$_.ParameterType.FullName}) -join '|') -eq 'UnityEngine.Texture|MaidParts/PARTS_COLOR|UnityEngine.RenderTexture'
    }
    $paletteCalls=@($nativeRender.Body.Instructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq 'MaidParts' -and $_.Operand.Name -eq 'GetPartsColorTableTex'
    })
    if($paletteCalls.Count -ne 1){throw "Expected one native rendering palette read, got $($paletteCalls.Count)"}

    # Execute the compiled production transpiler over instructions from the real
    # installed method. This reads managed IL only; no Unity/game code is run.
    Add-Type -LiteralPath (Join-Path $GameDir 'COM3D2x64_Data\Managed\UnityEngine.dll')
    Add-Type -LiteralPath (Join-Path $GameDir 'BepInEx\core\0Harmony.dll')
    [void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'BepInEx\core\BepInEx.dll'))
    $runtimeGameAssembly=[Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'COM3D2x64_Data\Managed\Assembly-CSharp.dll'))
    $runtimePlugin=[Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $PluginDll))
    $runtimeGame=$runtimeGameAssembly.ManifestModule
    $opcodes=@{}
    foreach($field in [Reflection.Emit.OpCodes].GetFields([Reflection.BindingFlags]'Public,Static')) {
        $opcode=$field.GetValue($null)
        $opcodes[$opcode.Name]=$opcode
    }
    $inputInstructions=[System.Collections.Generic.List[HarmonyLib.CodeInstruction]]::new()
    $before=[System.Collections.Generic.List[object]]::new()
    foreach($instruction in $nativeRender.Body.Instructions) {
        $operand=$instruction.Operand
        if($operand -is [Mono.Cecil.MethodReference]) {$operand=$runtimeGame.ResolveMethod($operand.MetadataToken.ToInt32())}
        elseif($operand -is [Mono.Cecil.FieldReference]) {$operand=$runtimeGame.ResolveField($operand.MetadataToken.ToInt32())}
        $code=[HarmonyLib.CodeInstruction]::new($opcodes[$instruction.OpCode.Name],$operand)
        $inputInstructions.Add($code)
        $before.Add([pscustomobject]@{Opcode=$code.opcode;Operand=$code.operand;Instruction=$code;Labels=$code.labels;Blocks=$code.blocks})
    }
    $transpiler=$runtimePlugin.GetType('COM3D2.Pregnancy.Plugin.Patch_BodyChangeColor').GetMethod('Transpiler',[Reflection.BindingFlags]'Static,NonPublic')
    $arguments=[object[]]::new(1)
    $arguments[0]=$inputInstructions
    $after=@($transpiler.Invoke($null,$arguments))
    if($after.Count -ne $before.Count){throw 'Color transpiler changed instruction count'}
    $replacements=0
    $blits=0
    for($index=0;$index -lt $after.Count;$index++) {
        $old=$before[$index]
        $new=$after[$index]
        if(![object]::ReferenceEquals($old.Instruction,$new) -or ![object]::ReferenceEquals($old.Labels,$new.labels) -or ![object]::ReferenceEquals($old.Blocks,$new.blocks)){throw 'Color transpiler discarded instruction or control-flow metadata'}
        if($old.Operand -is [Reflection.MethodInfo] -and $old.Operand.DeclaringType.FullName -eq 'MaidParts' -and $old.Operand.Name -eq 'GetPartsColorTableTex') {
            if($new.opcode -ne [Reflection.Emit.OpCodes]::Call -or $new.operand.DeclaringType.FullName -ne 'COM3D2.Pregnancy.Plugin.BodyChanges' -or $new.operand.Name -ne 'RenderColorTexture'){throw 'Native palette read was not redirected to the pregnancy renderer'}
            $replacements++
        } elseif($old.Opcode -ne $new.opcode -or ![object]::Equals($old.Operand,$new.operand)) {throw 'Color transpiler modified an unrelated instruction'}
        if($new.operand -is [Reflection.MethodInfo] -and $new.operand.DeclaringType.FullName -eq 'UnityEngine.Graphics' -and $new.operand.Name -eq 'Blit') {$blits++}
    }
    if($replacements -ne 1 -or $blits -ne 1){throw "Expected one palette replacement and one original blit, got $replacements / $blits"}
    Write-Output 'PASS: production color transpiler redirects the sole native palette read; original render call and all other instructions are unchanged.'
    Write-Output 'PASS: 5 Harmony targets verified against installed game; runtime contains no persistent appearance writes.'
} finally {$plugin.Dispose();$game.Dispose()}
