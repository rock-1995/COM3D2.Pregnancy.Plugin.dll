// Extracted verbatim from production; method names prefixed to isolate legacy fixture classification.
using UnityEngine;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController {
static readonly string[] RuntimeFaceKeywords =
        {
            "face", "head", "eye", "mayu", "tooth", "teeth",
            "tongue", "lip", "nose", "ear"
        };
static bool RuntimeIsFaceSlot(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            string lower = name.ToLowerInvariant();
            if (lower.Contains("wear")) return false;
            foreach (string kw in RuntimeFaceKeywords)
                if (lower.Contains(kw)) return true;

            return false;
        }
static string RuntimeGetMeshId(SkinnedMeshRenderer smr)
        {
            if (smr == null) return string.Empty;
            return (smr.name + "/" + smr.gameObject.name).ToLowerInvariant();
        }
static bool RuntimeContainsAny(string value, params string[] patterns)
        {
            foreach (string pattern in patterns)
                if (value.Contains(pattern)) return true;
            return false;
        }
static MeshMorphClass RuntimeClassifyMesh(SkinnedMeshRenderer smr)
        {
            string id = RuntimeGetMeshId(smr);
            if (string.IsNullOrEmpty(id)) return MeshMorphClass.Ignore;
            if (RuntimeIsFaceSlot(id)) return MeshMorphClass.Ignore;
            if (id.Contains("moza")) return MeshMorphClass.Ignore;
            if (id.Contains("accheso")) return MeshMorphClass.NavelAccessory;
            if (RuntimeContainsAny(id, "body", "base", "karada", "inmou", "nip", "under")) return MeshMorphClass.Body;
            if (RuntimeContainsAny(id, "bra", "pants", "psnts", "stkg", "mizugi", "zurashi")) return MeshMorphClass.InnerCloth;
            if (RuntimeContainsAny(id, "wear", "onep", "skrt", "zubon", "skirt", "mekure", "accsenaka")) return MeshMorphClass.OuterCloth;

            return MeshMorphClass.Ignore;
        }
}
