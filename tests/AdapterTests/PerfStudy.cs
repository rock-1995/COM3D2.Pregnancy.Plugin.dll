using System.Diagnostics;
using System.Text.Json;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
using NM=System.Numerics.Matrix4x4;
using GM=COM3D2.Pregnancy.Plugin.Growth.Numerics.Matrix4x4;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void RunPerfStudy(string[] args)
    {
        Directory.CreateDirectory(args[1]);Shape=new VtxSettings{LowerTransitionStart=.02f,LowerTransitionWidth=.60f,UpperTransitionStart=.10542166f,UpperTransitionWidth=.5965462f,AxisPullLow=.15f,AxisPullHigh=.8f,AxisPullAngle=60};var reports=new List<object>();
        var body=ReadModel(args[2],"body-real");var cloth=ReadModel(args[3],"wear-real");var maid=new Maid();
        var renderers=new List<SkinnedMeshRenderer>{body,cloth};
        object Measure(string name,Action action,int n=10)
        {
            action();action();var timings=new double[n];long bytes=0;
            for(int i=0;i<n;i++)
            {
                long alloc=GC.GetAllocatedBytesForCurrentThread(),stamp=Stopwatch.GetTimestamp();action();
                timings[i]=Stopwatch.GetElapsedTime(stamp).TotalMilliseconds;bytes+=GC.GetAllocatedBytesForCurrentThread()-alloc;
            }
            Array.Sort(timings);var row=new{name,n,medianMs=timings[n/2],p95Ms=timings[(int)((n-1)*.95)],bytesPerCall=bytes/(double)n};
            reports.Add(row);Console.WriteLine(JsonSerializer.Serialize(row));return row;
        }
        Measure("full body calibration (uncached)",()=>{_alContexts.Remove(maid.GetHashCode());PrepareALContext(maid,renderers,1);});
        Measure("body calibration cache hit",()=>PrepareALContext(maid,renderers,1),100);
        var rb=new MeshRecord{SMR=body,Mesh=body.sharedMesh,OrigVerts=body.sharedMesh.vertices};Vector3[] bv=null;
        Measure("body deformation only",()=>{Check(TryDeformAL(body,rb,MeshMorphClass.Body,1,out bv,out _),"perf body");});
        Measure("body binding rebuild",()=>InstallALBinding(maid,rb,bv,1));
        var rc=new MeshRecord{SMR=cloth,Mesh=cloth.sharedMesh,OrigVerts=cloth.sharedMesh.vertices};Vector3[] cv=null;
        Measure("dress deformation + surface matching + repair",()=>{Check(TryDeformAL(cloth,rc,MeshMorphClass.OuterCloth,1,out cv,out _),"perf dress");},5);
        Measure("dress binding rebuild",()=>InstallALBinding(maid,rc,cv,1),5);
        Measure("two renderers pose update",()=>UpdateALBindings(maid),1500);
        Measure("4x4 inverse alone",()=>GM.Invert(GM.Identity,out _),1500);
        var context=_alWorking;var sb=context.Spine;var spineRest=sb.localToWorldMatrix;int frame=0;
        Measure("two renderers moving pose update",()=>{sb.localToWorldMatrix=new(){Rows=spineRest.Rows*NM.CreateRotationX(MathF.Sin(frame++*.03f)*.15f)};UpdateALBindings(maid);},1500);
        var inventory=_alBindings.Values.Select(b=>new{name=b.Mesh.name,vertices=b.Mesh.vertexCount,nativeBones=b.Bones.Length,virtualSlots=b.Recipes.Length,paletteMatrices=b.Palette.Length,palettePayloadBytes=b.Palette.Length*64}).ToArray();
        File.WriteAllText(Path.Combine(args[1],"performance.json"),JsonSerializer.Serialize(new{runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,unityEngine=false,notes="Real port math; Unity stubs. Native bindposes upload, hierarchy scan, rendering, normals and game hook frequency are not measured.",inventory,measurements=reports},new JsonSerializerOptions{WriteIndented=true}));
        ReleaseALBindings(maid);
    }
}
