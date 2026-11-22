param([string]$GameDir='E:\com3d2', [Parameter(Mandatory=$true)][string]$PluginDll)
$ErrorActionPreference='Stop'
Add-Type -LiteralPath (Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll')
$plugin=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($PluginDll)
$game=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'COM3D2x64_Data\Managed\Assembly-CSharp.dll'))
try {
    $count=0
    foreach($patch in $plugin.MainModule.Types | Where-Object Name -Like 'PregnancyMaker*Patch') {
        $attribute=$patch.CustomAttributes | Where-Object {$_.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch'}
        $type=$game.MainModule.GetType($attribute.ConstructorArguments[0].Value.FullName)
        $target=@($type.Methods | Where-Object Name -EQ $attribute.ConstructorArguments[1].Value)
        if($target.Count -ne 1){throw "Missing or ambiguous native target for $($patch.Name)"}
        foreach($method in $patch.Methods | Where-Object Name -In @('Prefix','Postfix')) {
            foreach($parameter in $method.Parameters) {
                if($parameter.Name.StartsWith('___')) {
                    $field=$type.Fields | Where-Object Name -EQ $parameter.Name.Substring(3)
                    if(!$field -or $parameter.ParameterType.FullName -ne 'System.Object') {
                        throw "Invalid private-panel injection: $($patch.Name).$($parameter.Name)"
                    }
                    $panel=$game.MainModule.GetType($field.FieldType.FullName)
                    $grid=$panel.Fields | Where-Object Name -EQ 'goGrid'
                    if(!$grid -or $grid.FieldType.FullName -ne 'UnityEngine.GameObject') {throw 'Native slider panel grid changed'}
                    continue
                }
                $expected=if($parameter.Name -eq '__instance') {$type.FullName}
                    else {($target[0].Parameters | Where-Object Name -EQ $parameter.Name).ParameterType.FullName}
                if(!$expected -or $parameter.ParameterType.FullName -ne $expected) {
                    throw "Signature mismatch: $($patch.Name).$($parameter.Name)"
                }
            }
        }
        Write-Output "OK $($patch.Name) -> $($type.FullName).$($target[0].Name)"
        $count++
    }
    if($count -ne 5){throw "Expected 5 maker patches, got $count"}
    $scene=$game.MainModule.GetType('SceneEdit')
    $confirm=$scene.Methods | Where-Object Name -EQ 'OnEndDlgOk'
    if(-not ($confirm.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'CoWaitPutCloth'})) {
        throw 'Native accepted-edit lifecycle changed; recheck commit hook'
    }
    $cancel=$scene.Methods | Where-Object Name -EQ 'OnEndDlgCancel'
    if($cancel.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'OnEndScene'}) {
        throw 'Native dialog cancel now exits the edit; recheck preview lifecycle'
    }
    $maker=$plugin.MainModule.GetType('COM3D2.Pregnancy.Plugin.PregnancyMaker')
    foreach($type in @($maker)+@($maker.NestedTypes)) {
        foreach($method in $type.Methods | Where-Object HasBody) {
            foreach($instruction in $method.Body.Instructions) {
                if($instruction.OpCode.Code -eq 'Stfld' -and $instruction.Operand.DeclaringType.FullName -eq 'MaidProp') {
                    throw 'Maker writes a persistent native property'
                }
                if($instruction.Operand -is [Mono.Cecil.MethodReference] -and $instruction.Operand.Name -in @('SetProp','PresetSet','PresetSave','PresetSaveNotWriteFile','SerializeProp','SerializeMultiColor','SetPartsColor')) {
                    throw 'Maker uses a native appearance/preset write API'
                }
            }
        }
    }
    $changed=$maker.Methods | Where-Object Name -EQ 'Changed'
    if(-not ($changed.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'SetPreview'})) {
        throw 'Maker slider is not using memory-only preview'
    }
    if($changed.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.Name -eq 'PregnancyAppearance' -and $_.Operand.Name -in @('Set','CommitPreview')}) {
        throw 'Maker slider commits before native confirmation'
    }
    Write-Output 'PASS: installed-game APIs, five Harmony targets, accepted-edit lifecycle and preview-only slider writes verified. In-game layout has not been tested.'
} finally {$plugin.Dispose();$game.Dispose()}
