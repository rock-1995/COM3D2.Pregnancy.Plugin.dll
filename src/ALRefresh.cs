using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;

namespace COM3D2.Pregnancy.Plugin
{
    public static partial class BellyMorphController
    {
        static readonly Dictionary<int,VtxSettings> _maidShapes=new Dictionary<int,VtxSettings>();

        public static VtxSettings GetAppliedShape(Maid maid)
            => maid!=null && _maidShapes.TryGetValue(maid.GetHashCode(),out var p)?p.Copy():Shape.Copy();

        sealed class MaidShapeScope : IDisposable
        {
            readonly VtxSettings previous;
            internal MaidShapeScope(Maid maid){previous=Shape;Shape=GetAppliedShape(maid);}
            public void Dispose(){Shape=previous;}
        }

        // Automatic updates reuse the target maid's last applied settings. The
        // UI's current edit buffer must never change another maid's live pose.
        public static void RefreshProgress(Maid maid,float progress)
        {
            if(!IsValid(maid))return;
            using(new MaidShapeScope(maid))ApplyProgress(maid,progress);
        }

        sealed class RefreshStats
        {
            internal Maid Maid;
            internal string Reason;
            internal long Started,ContextTicks,DeformTicks,NormalTicks,BindingTicks;
            internal int ContextBuilds,ContextHits,Rebuilt,Reused,RestQueries,RestHits;
        }
        static RefreshStats _refreshStats,_lastRefreshStats;
        sealed class RefreshTrace : IDisposable
        {
            readonly RefreshStats previous,stats;
            readonly bool owner;
            internal RefreshTrace(Maid maid,string reason)
            {
                previous=_refreshStats;
                if(previous!=null && previous.Maid==maid){stats=previous;return;}
                owner=true;stats=new RefreshStats{Maid=maid,Reason=reason,Started=Stopwatch.GetTimestamp()};_refreshStats=stats;
            }
            public void Dispose()
            {
                if(!owner)return;
                _lastRefreshStats=stats;_refreshStats=previous;
                double ms=Milliseconds(Stopwatch.GetTimestamp()-stats.Started);
                if(ms<10 && !IsDebugMeshLoggingEnabled())return;
                _log.LogInfo(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "[RefreshPerf] maid={0} reason={1} total={2:F2}ms context={3:F2}ms({4} build/{5} hit) deform={6:F2}ms normals={7:F2}ms binding={8:F2}ms meshes={9} rebuild/{10} reuse surface={11} query/{12} reuse",
                    stats.Maid==null?0:stats.Maid.GetHashCode(),stats.Reason,ms,Milliseconds(stats.ContextTicks),stats.ContextBuilds,stats.ContextHits,
                    Milliseconds(stats.DeformTicks),Milliseconds(stats.NormalTicks),Milliseconds(stats.BindingTicks),stats.Rebuilt,stats.Reused,stats.RestQueries,stats.RestHits));
            }
        }
        static double Milliseconds(long ticks)=>ticks*1000.0/Stopwatch.Frequency;

        static int ExactVectors(Vector3[] values)
        {
            if(values==null)return 0;
            unchecked
            {
                int hash=17*31+values.Length;
                foreach(var v in values){hash=hash*31+v.x.GetHashCode();hash=hash*31+v.y.GetHashCode();hash=hash*31+v.z.GetHashCode();}
                return hash;
            }
        }
        static int ExactIndices(int[] values)
        {
            if(values==null)return 0;
            unchecked{int h=17*31+values.Length;foreach(int i in values)h=h*31+i;return h;}
        }
        static int RigSignature(SkinnedMeshRenderer renderer,bool includeWeights)
        {
            var bones=NativeBones(renderer);var binds=NativeBinds(renderer);
            unchecked
            {
                int h=17*31+bones.Length;
                foreach(var b in bones){h=h*31+(b==null?0:b.GetHashCode());h=h*31+(b==null?0:b.name.GetHashCode());}
                h=h*31+binds.Length;
                foreach(var bind in binds)for(int r=0;r<4;r++)for(int c=0;c<4;c++)h=h*31+bind[r,c].GetHashCode();
                if(includeWeights)foreach(var w in NativeWeights(renderer))
                {
                    h=h*31+w.boneIndex0;h=h*31+w.weight0.GetHashCode();h=h*31+w.boneIndex1;h=h*31+w.weight1.GetHashCode();
                    h=h*31+w.boneIndex2;h=h*31+w.weight2.GetHashCode();h=h*31+w.boneIndex3;h=h*31+w.weight3.GetHashCode();
                }
                return h;
            }
        }
        static int MeshStructureSignature(SkinnedMeshRenderer renderer)
        {
            unchecked{return (RigSignature(renderer,true)*31+ExactIndices(renderer.sharedMesh.triangles))*31+(UseOrdinaryClothing(renderer)?1:0);}
        }

        static void PruneDetachedBindings(Maid maid,List<SkinnedMeshRenderer> renderers)
        {
            var live=new HashSet<SkinnedMeshRenderer>(renderers);
            foreach(var pair in _alBindings.ToArray())
                if(pair.Value.Maid==maid && (pair.Key==null || !live.Contains(pair.Key) || pair.Key.sharedMesh!=pair.Value.Mesh))
                    ReleaseALBinding(pair.Key);
        }

        static void SyncContextMaps(ALContext context,List<SkinnedMeshRenderer> renderers)
        {
            var live=new HashSet<SkinnedMeshRenderer>();
            foreach(var r in renderers)
            {
                if(r==null || r.sharedMesh==null || ClassifyMesh(r)==MeshMorphClass.Ignore)continue;
                live.Add(r);int rig=RigSignature(r,false);
                if(context.MapSignatures.TryGetValue(r,out int previous) && previous==rig)continue;
                if(RestMap(r,context.Body,out var map))
                {context.Maps[r]=map*context.BodyToReference;context.MapSignatures[r]=rig;}
                else {context.Maps.Remove(r);context.MapSignatures.Remove(r);}
            }
            foreach(var r in context.Maps.Keys.ToArray())if(!live.Contains(r)){context.Maps.Remove(r);context.MapSignatures.Remove(r);}
        }

        static bool CanReuseAppliedMesh(MeshRecord record,Vector3[] current,Vector3[] normals,float progress,MeshMorphClass kind,int structure)
        {
            if(!record.RefreshReady || record.AppliedContext!=_alWorking || record.RefreshShapeSignature!=ComputeMorphBakeSignature(progress,kind) ||
                record.RefreshStructureSignature!=structure || record.AppliedExactSignature!=ExactVectors(current) || record.AppliedNormalSignature!=ExactVectors(normals))return false;
            var binding=ActiveBinding(record.SMR);
            return record.HadBinding?binding!=null && binding.Context==record.AppliedContext:binding==null;
        }
        static void RememberAppliedMesh(MeshRecord record,Vector3[] vertices,float progress,MeshMorphClass kind,int structure)
        {
            record.RefreshReady=true;record.AppliedContext=record.GrowthContext;
            record.RefreshShapeSignature=ComputeMorphBakeSignature(progress,kind);record.RefreshStructureSignature=structure;
            record.AppliedExactSignature=ExactVectors(vertices);record.AppliedNormalSignature=ExactVectors(record.Mesh.normals);
            record.HadBinding=ActiveBinding(record.SMR)!=null;
        }

        static bool TryFindRecordRestSurface(MeshRecord record,int vertex,Vector3 original,out BodySurfaceHit hit)
        {
            if(record.RestHits==null || record.RestHits.Length!=record.OrigVerts.Length)
            {record.RestHits=new BodySurfaceHit[record.OrigVerts.Length];record.RestHitState=new byte[record.OrigVerts.Length];}
            if(record.RestHitState[vertex]!=0)
            {hit=record.RestHits[vertex];if(_refreshStats!=null)_refreshStats.RestHits++;return record.RestHitState[vertex]==2;}
            if(_refreshStats!=null)_refreshStats.RestQueries++;
            bool found=TryFindClothingRestSurface(record.GrowthContext.Surface,original,record.GrowthContext.Frame.Profile.Span*.2f,out hit);
            record.RestHits[vertex]=hit;record.RestHitState[vertex]=(byte)(found?2:1);return found;
        }
    }
}
