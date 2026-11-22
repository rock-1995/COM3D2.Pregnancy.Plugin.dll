param([string]$GameDir='E:\com3d2', [Parameter(Mandatory=$true)][string]$PluginDll)
$ErrorActionPreference='Stop'
Add-Type -LiteralPath (Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll')
$plugin=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($PluginDll)
$game=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'COM3D2x64_Data\Managed\Assembly-CSharp.dll'))
try {
    $count=0
    foreach($patch in $plugin.MainModule.Types | Where-Object { $_.Name -like 'Patch_NativeAppearance_*' -or $_.Name -eq 'Patch_TMorphFixBlendValues' }) {
        $attribute=$patch.CustomAttributes | Where-Object {$_.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch'}
        if($patch.Name -eq 'Patch_NativeAppearance_PartLoad') {
            $type=$game.MainModule.GetType('TBodySkin')
            $target=@($type.Methods | Where-Object {$_.Name -eq 'Load' -and $_.Parameters.Count -eq 11})
        } else {
            $type=$game.MainModule.GetType($attribute.ConstructorArguments[0].Value.FullName)
            $target=@($type.Methods | Where-Object Name -EQ $attribute.ConstructorArguments[1].Value)
        }
        if($target.Count -ne 1){throw "Missing/ambiguous native method for $($patch.Name)"}
        foreach($method in $patch.Methods | Where-Object Name -In @('Prefix','Postfix','Finalizer')) {
            foreach($parameter in $method.Parameters) {
                $expected=switch($parameter.Name) {
                    '__instance' {$type.FullName}
                    '__result' {$target[0].ReturnType.FullName}
                    '__state' {$parameter.ParameterType.FullName.TrimEnd('&')}
                    '__exception' {'System.Exception'}
                    default {($target[0].Parameters | Where-Object Name -EQ $parameter.Name).ParameterType.FullName}
                }
                $actual=$parameter.ParameterType.FullName.TrimEnd('&')
                if(!$expected -or $actual -ne $expected){throw "Signature mismatch: $($patch.Name).$($parameter.Name)"}
            }
        }
        $count++
    }
    if($count -ne 9){throw "Expected 8 lifecycle patches plus the native morph patch; got $count"}
    $fields=@(
        @('Maid','AllProcProp2Fase','System.Int32'),
        @('TMorph','BlendValues','System.Single[]'),
        @('TMorph','BlendValuesCHK','System.Single[]'),
        @('TMorph','m_vTmpVert','UnityEngine.Vector3[]'),
        @('TMorph','m_vTmpNorm','UnityEngine.Vector3[]'),
        @('TMorph','m_bMorph','System.Boolean'),
        @('TMorph','smr_src','UnityEngine.SkinnedMeshRenderer')
    )
    foreach($entry in $fields) {
        $field=$game.MainModule.GetType($entry[0]).Fields | Where-Object Name -EQ $entry[1]
        if(!$field -or $field.FieldType.FullName -ne $entry[2]) {throw "Native field mismatch: $($entry[0]).$($entry[1])"}
    }
    Write-Output "PASS: $count Harmony targets and $($fields.Count) native fields match the installed game."
} finally {$plugin.Dispose();$game.Dispose()}
