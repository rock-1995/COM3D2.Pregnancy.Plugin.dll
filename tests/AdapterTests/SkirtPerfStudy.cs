using System.Diagnostics;
using System.Text.Json;
using UnityEngine;
using NM=System.Numerics.Matrix4x4;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void RunSkirtPerfStudy(string[] args)
    {
        Directory.CreateDirectory(args[1]);var reports=new List<object>();
        foreach(var path in args.Skip(3))
        {
            var body=ReadModel(args[2],"body");var cloth=ReadModel(path,Path.GetFileNameWithoutExtension(path));var maid=new Maid();
            PrepareALContext(maid,new(){body,cloth},1);var c=_alWorking;
            var rec=new MeshRecord{SMR=cloth,Mesh=cloth.sharedMesh,OrigVerts=cloth.sharedMesh.vertices};
            TryDeformAL(cloth,rec,MeshMorphClass.OuterCloth,1,out var residual,out _);
            cloth.sharedMesh.vertices=residual;InstallALBinding(maid,rec,residual,1);var b=_alBindings[cloth];
            var rest=b.Bones.Select(x=>x.localToWorldMatrix).ToArray();var bodyRest=body.bones.Select(x=>x.localToWorldMatrix).ToArray();
            var poses=new List<float[][]>();
            for(int pose=0;pose<5;pose++)
            {
                var shift=new Matrix4x4{Rows=NM.CreateRotationY(.31f*pose)*NM.CreateTranslation(pose*.4f,pose*.1f,-pose*.2f)};
                for(int i=0;i<body.bones.Length;i++)body.bones[i].localToWorldMatrix=shift*bodyRest[i];
                for(int i=0;i<b.Bones.Length;i++)b.Bones[i].localToWorldMatrix=shift*new Matrix4x4{Rows=NM.CreateRotationX(IsDrapeBone(b.Bones[i])?MathF.Sin(i*.31f)*pose*.17f:0)}*rest[i];
                UpdateALBinding(b);
                poses.Add(residual.Select((v,i)=>{var p=SkinPoint(cloth,v,i);return new[]{p.x,p.y,p.z};}).ToArray());
            }
            object Measure(bool moving)
            {
                const int n=1000;for(int k=0;k<30;k++)UpdateALBinding(b);
                var times=new double[n];long bytes=0;
                for(int k=0;k<n;k++)
                {
                    if(moving)for(int i=0;i<b.Bones.Length;i++)if(IsDrapeBone(b.Bones[i]))
                        b.Bones[i].localToWorldMatrix=new Matrix4x4{Rows=NM.CreateRotationX(MathF.Sin(k*.03f+i*.2f)*.12f)}*rest[i];
                    var allocated=GC.GetAllocatedBytesForCurrentThread();long start=Stopwatch.GetTimestamp();
                    UpdateALBinding(b);UpdateALBinding(b); // Both runtime hooks observing the same completed pose.
                    times[k]=Stopwatch.GetElapsedTime(start).TotalMilliseconds;bytes+=GC.GetAllocatedBytesForCurrentThread()-allocated;
                }
                Array.Sort(times);return new{moving,medianMs=times[n/2],p95Ms=times[n*95/100],bytesPerPair=bytes/(double)n};
            }
            var row=new{model=cloth.name,nativeBones=b.Bones.Length,palette=b.Palette.Length,recipes=b.SkirtRecipes.Length,stationary=Measure(false),moving=Measure(true)};
            reports.Add(row);Console.WriteLine(JsonSerializer.Serialize(row));
            File.WriteAllText(Path.Combine(args[1],cloth.name+"-poses.json"),JsonSerializer.Serialize(poses));ReleaseALBindings(maid);
        }
        File.WriteAllText(Path.Combine(args[1],"performance.json"),JsonSerializer.Serialize(new{notes="Managed Unity stubs; both pose hooks per sample. Excludes Unity upload/rendering and is not game FPS.",reports},new JsonSerializerOptions{WriteIndented=true}));
    }
}
