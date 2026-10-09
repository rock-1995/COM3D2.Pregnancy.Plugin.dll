using System.Collections.Generic;
using UnityEngine;
namespace COM3D2.Pregnancy.Plugin
{
 public static partial class BellyMorphController
 {
  static readonly Dictionary<int,int> _meshInventorySignatures=new Dictionary<int,int>();
	private static void MaybeLogMeshInventory(Maid maid, List<SkinnedMeshRenderer> renderers, string reason)
	{
		if (!IsDebugMeshLoggingEnabled())
		{
			if (maid != null)
			{
				_meshInventorySignatures.Remove(maid.GetHashCode());
			}
		}
		else
		{
			if (maid == null || renderers == null)
			{
				return;
			}
			int hashCode = maid.GetHashCode();
			int num = ComputeMeshInventorySignature(renderers);
			if (_meshInventorySignatures.TryGetValue(hashCode, out var value) && value == num)
			{
				return;
			}
			_meshInventorySignatures[hashCode] = num;
			_log.LogInfo("[BellyMesh] inventory maid=" + GetMaidName(maid) + " reason=" + reason + $" count={renderers.Count}" + $" signature={num}");
			for (int i = 0; i < renderers.Count; i++)
			{
				SkinnedMeshRenderer skinnedMeshRenderer = renderers[i];
				if (skinnedMeshRenderer == null)
				{
					_log.LogInfo("[BellyMesh] item maid=" + GetMaidName(maid) + $" index={i}" + " null=1");
					continue;
				}
				Mesh sharedMesh = skinnedMeshRenderer.sharedMesh;
				MeshMorphClass meshMorphClass = ClassifyMesh(skinnedMeshRenderer);
				_log.LogInfo("[BellyMesh] item maid=" + GetMaidName(maid) + $" index={i}" + " id=" + GetMeshId(skinnedMeshRenderer) + $" class={meshMorphClass}" + " renderer=" + skinnedMeshRenderer.name + " go=" + skinnedMeshRenderer.gameObject.name + " path=" + GetTransformPath(skinnedMeshRenderer.transform) + " mesh=" + ((sharedMesh != null) ? sharedMesh.name : string.Empty) + $" meshId={((sharedMesh != null) ? sharedMesh.GetInstanceID() : 0)}" + $" verts={((sharedMesh != null) ? sharedMesh.vertexCount : 0)}" + $" bones={((skinnedMeshRenderer.bones != null) ? skinnedMeshRenderer.bones.Length : 0)}" + $" enabled={skinnedMeshRenderer.enabled}" + $" activeSelf={skinnedMeshRenderer.gameObject.activeSelf}" + $" activeInHierarchy={skinnedMeshRenderer.gameObject.activeInHierarchy}");
			}
		}
	}

	private static int ComputeMeshInventorySignature(List<SkinnedMeshRenderer> renderers)
	{
		int num = 17;
		if (renderers == null)
		{
			return num;
		}
		num = num * 31 + renderers.Count;
		for (int i = 0; i < renderers.Count; i++)
		{
			SkinnedMeshRenderer skinnedMeshRenderer = renderers[i];
			if (skinnedMeshRenderer == null)
			{
				num *= 31;
				continue;
			}
			Mesh sharedMesh = skinnedMeshRenderer.sharedMesh;
			num = num * 31 + skinnedMeshRenderer.GetInstanceID();
			num = num * 31 + ((sharedMesh != null) ? sharedMesh.GetInstanceID() : 0);
			num = num * 31 + ((sharedMesh != null) ? sharedMesh.vertexCount : 0);
			num = num * 31 + (skinnedMeshRenderer.enabled ? 1 : 0);
			num = num * 31 + (skinnedMeshRenderer.gameObject.activeSelf ? 1 : 0);
			num = num * 31 + (skinnedMeshRenderer.gameObject.activeInHierarchy ? 1 : 0);
			num = num * 31 + GetMeshId(skinnedMeshRenderer).GetHashCode();
			num = num * 31 + ((sharedMesh != null && sharedMesh.name != null) ? sharedMesh.name.GetHashCode() : 0);
		}
		return num;
	}

	private static string GetTransformPath(Transform transform)
	{
		if (transform == null)
		{
			return string.Empty;
		}
		List<string> list = new List<string>();
		Transform transform2 = transform;
		while (transform2 != null && list.Count < 32)
		{
			list.Add(transform2.name);
			transform2 = transform2.parent;
		}
		list.Reverse();
		return string.Join("/", list.ToArray());
	}
 }
}
