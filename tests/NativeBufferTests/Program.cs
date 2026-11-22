using COM3D2.Pregnancy.Plugin;
using UnityEngine;

static class Program
{
    static int checks;
    static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception("FAILED: " + message); }
    static void X(float actual, float expected, string message) => Check(Math.Abs(actual - expected) < .000001f, message);
    static SkinnedMeshRenderer Renderer(Maid maid, float first, bool enabled = true)
    {
        var renderer = new SkinnedMeshRenderer { sharedMesh = new Mesh
            { vertices = [new(first), new(2), new(3)], normals = [new(1), new(1), new(1)] }, enabled = enabled };
        maid.Renderers.Add(renderer); return renderer;
    }
    static void Main()
    {
        var maid = new Maid();
        var body = Renderer(maid, 1);
        var hidden = Renderer(maid, 3, false);
        var bodyMorph = new TMorph(maid, body);
        var hiddenMorph = new TMorph(maid, hidden);
        Check(BellyMorphController.IsNativeMorphRelevant(bodyMorph), "body registered");
        Check(BellyMorphController.IsNativeMorphRelevant(hiddenMorph), "hidden garment registered");
        body.gameObject.activeInHierarchy = false;
        Check(BellyMorphController.BodyVisibleForTests(body), "native visible body contributes before its object becomes active");
        body.gameObject.activeInHierarchy = true;
        bodyMorph.bodyskin.boVisible = false;
        Check(!BellyMorphController.BodyVisibleForTests(body), "native hidden body is excluded even while its object is still active");
        bodyMorph.bodyskin.boVisible = true;
        body.enabled = false;
        Check(!BellyMorphController.BodyVisibleForTests(body), "disabled renderer is excluded despite a visible native slot");
        body.enabled = true;
        var unmapped = new SkinnedMeshRenderer();
        Check(BellyMorphController.BodyVisibleForTests(unmapped), "unmapped enabled active body retains legacy visibility fallback");
        unmapped.gameObject.activeInHierarchy = false;
        Check(!BellyMorphController.BodyVisibleForTests(unmapped), "unmapped inactive body is excluded by legacy fallback");
        unmapped.gameObject.activeInHierarchy = true;
        unmapped.enabled = false;
        Check(!BellyMorphController.BodyVisibleForTests(unmapped), "unmapped disabled body is excluded by legacy fallback");
        Check(!BellyMorphController.NativeMorphWillChange(bodyMorph), "stable native values do not request rebuild");
        BellyMorphController.ApplyNativeAppearance(maid, .5f);
        X(body.sharedMesh.vertices[0].x, 6, "final body deformation produced");
        X(hidden.sharedMesh.vertices[0].x, 8, "hidden garment completed during build");
        Check(BellyMorphController.IncludedInactive, "production build explicitly includes hidden models");
        X(bodyMorph.m_vOriVert[0].x, 1, "original native vertices remain clean");
        X(bodyMorph.m_vOriNorm[0].x, 1, "original native normals remain clean");
        body.sharedMesh.vertices = [new(-99), new(2), new(3)];
        body.sharedMesh.normals = [new(-99), new(1), new(1)];
        bodyMorph.ResetBlendValues();
        X(body.sharedMesh.vertices[0].x, 6, "native Reset restores final pregnancy vertices");
        X(body.sharedMesh.normals[0].x, 1.5f, "native Reset restores final pregnancy normals");
        hidden.sharedMesh.vertices = [new(-99), new(2), new(3)];
        hiddenMorph.ResetBlendValues();
        X(hidden.sharedMesh.vertices[0].x, 8, "visibility restore does not revert a precomputed hidden garment");

        bodyMorph.SetBlend(.5f);
        Check(BellyMorphController.NativeMorphWillChange(bodyMorph), "changed native shape requests rebuild");
        bodyMorph.FixBlendValues();
        X(body.sharedMesh.vertices[0].x, 2, "next native Fix starts from clean original plus native delta");
        X(body.sharedMesh.normals[0].x, 1.1f, "next native normals exclude previous pregnancy correction");
        Check(!BellyMorphController.NativeMorphWillChange(bodyMorph), "native comparison cache is committed");
        BellyMorphController.ApplyNativeAppearance(maid, .5f);
        X(body.sharedMesh.vertices[0].x, 7, "pregnancy applied once after native change");
        BellyMorphController.ApplyNativeAppearance(maid, .5f);
        X(body.sharedMesh.vertices[0].x, 7, "repeat output synchronization does not accumulate deformation");
        bodyMorph.ResetBlendValues();
        X(body.sharedMesh.vertices[0].x, 7, "native Reset retains newly customized pregnancy result");

        BellyMorphController.ApplyNativeAppearance(maid, 0);
        X(body.sharedMesh.vertices[0].x, 2, "zero belly restores current native customized base");
        bodyMorph.ResetBlendValues();
        X(body.sharedMesh.vertices[0].x, 2, "zero belly synchronizes reset buffers instead of resurrecting old belly");
        BellyMorphController.ApplyNativeAppearance(maid, .7f);
        BellyMorphController.ForgetNativeAppearance(maid);
        Check(BellyMorphController.RegisteredMorphCount == 0, "slot/unload cleanup removes old native renderer references");
        bodyMorph.ResetBlendValues();
        X(body.sharedMesh.vertices[0].x, 2, "cleanup leaves native reset buffer clean");
        BellyMorphController.IsNativeMorphRelevant(bodyMorph);
        BellyMorphController.IsNativeMorphRelevant(hiddenMorph);
        BellyMorphController.ApplyNativeAppearance(maid, .2f);
        X(body.sharedMesh.vertices[0].x, 4, "same Maid instance accepts next slot progress without old delta");
        X(hidden.sharedMesh.vertices[0].x, 5, "same Maid hidden garment uses next slot progress");
        Console.WriteLine($"Native buffer tests passed: {checks}");
    }
}
