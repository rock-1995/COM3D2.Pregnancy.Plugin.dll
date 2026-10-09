using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
using NVector=COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;
using NMatrix=COM3D2.Pregnancy.Plugin.Growth.Numerics.Matrix4x4;

namespace COM3D2.Pregnancy.Plugin
{
    public static partial class BellyMorphController
    {
        public static void LogShapeParameters(VtxSettings settings)
        {
            _log.LogInfo("[ALGrowth] ===== Current shape parameters =====");
            foreach(var field in typeof(VtxSettings).GetFields(BindingFlags.Public|BindingFlags.Instance))
                _log.LogInfo("[ALGrowth] "+field.Name+" = "+Convert.ToString(field.GetValue(settings),CultureInfo.InvariantCulture));
        }
        public static string ExportSkirtVertexMorphDump(Maid maid)=>ExportMorphDiagnostic(maid,"Skirt");
        public static string ExportUpperClothVertexMorphDump(Maid maid)=>ExportMorphDiagnostic(maid,"Upper");
        public static string ExportNavelAccessoryTriangleDump(Maid maid)=>ExportMorphDiagnostic(maid,"Navel");
        static bool IsPantsDiagnostic(SkinnedMeshRenderer r)=>ContainsAny(GetMeshId(r),"zubon","pants","psnts");
        static string DiagnosticNumber(float value)=>value.ToString("R",CultureInfo.InvariantCulture);
        static void DiagnosticCells(TextWriter writer,params object[] values)
        {
            bool first=true;
            foreach(var value in values)
            {
                if(!first)writer.Write(',');first=false;
                string cell=value is float?DiagnosticNumber((float)value):Convert.ToString(value,CultureInfo.InvariantCulture);
                writer.Write('"');writer.Write((cell??"").Replace("\"","\"\""));writer.Write('"');
            }
            writer.WriteLine();
        }
        static Vector3 DiagnosticSkin(Vector3 point,BoneWeight w,Transform[] bones,Matrix4x4[] binds)
        {
            var result=Vector3.zero;
            void Add(int index,float weight){if(weight>0&&index>=0&&index<bones.Length&&index<binds.Length&&bones[index]!=null)result+=(bones[index].localToWorldMatrix*binds[index]).MultiplyPoint3x4(point)*weight;}
            Add(w.boneIndex0,w.weight0);Add(w.boneIndex1,w.weight1);Add(w.boneIndex2,w.weight2);Add(w.boneIndex3,w.weight3);return result;
        }
        static string DiagnosticBone(Transform[] bones,int i)=>i>=0&&i<bones.Length&&bones[i]!=null?bones[i].name:"";
        static string ExportMorphDiagnostic(Maid maid,string selection)
        {
            if(maid==null)throw new ArgumentNullException("maid");
            string root=Path.Combine(BepInEx.Paths.PluginPath,"Pregnancy"+selection+"Dumps");Directory.CreateDirectory(root);
            string directory=Path.Combine(root,SafeDumpName(GetMaidName(maid))+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));Directory.CreateDirectory(directory);
            _alContexts.TryGetValue(maid.GetHashCode(),out var context);
            using(var info=new StreamWriter(Path.Combine(directory,"parameters.txt"),false,Encoding.UTF8))
            {
                info.WriteLine("AL shape diagnostics; read-only snapshot, no Apply/Reset.");
                info.WriteLine("Selection="+selection+"; activeProgress="+DiagnosticNumber(GetActiveProgress(maid))+"; storedProgress="+DiagnosticNumber(PregnancyManager.GetProgress(maid)));
                foreach(var field in typeof(VtxSettings).GetFields(BindingFlags.Public|BindingFlags.Instance))info.WriteLine(field.Name+"="+Convert.ToString(field.GetValue(Shape),CultureInfo.InvariantCulture));
            }
            using(var csv=new StreamWriter(Path.Combine(directory,"vertices.csv"),false,Encoding.UTF8))
            using(var manifest=new StreamWriter(Path.Combine(directory,"meshes.csv"),false,Encoding.UTF8))
            using(var boundary=new StreamWriter(Path.Combine(directory,"navel-boundary.csv"),false,Encoding.UTF8))
            using(var planes=new StreamWriter(Path.Combine(directory,"navel-planes.csv"),false,Encoding.UTF8))
            {
                csv.WriteLine("mesh,renderer,class,index,source,original_x,original_y,original_z,applied_x,applied_y,applied_z,current_x,current_y,current_z,delta_x,delta_y,delta_z,original_ref_x,original_ref_y,original_ref_z,applied_ref_x,applied_ref_y,applied_ref_z,current_world_x,current_world_y,current_world_z,virtual_alpha,torso_ownership,thigh_restore,breast_excluded,bone0,weight0,bone1,weight1,bone2,weight2,bone3,weight3");
                manifest.WriteLine("mesh,renderer,class,active,enabled,vertices,source,status");
                boundary.WriteLine("accessory,body,sample,triangle,a,b,c,bary_a,bary_b,bary_c,original_ref_x,original_ref_y,original_ref_z,morphed_ref_x,morphed_ref_y,morphed_ref_z,last_world_x,last_world_y,last_world_z");
                planes.WriteLine("accessory,plane,center_x,center_y,center_z,right_x,right_y,right_z,up_x,up_y,up_z,normal_x,normal_y,normal_z,radius,centerY");
                foreach(var renderer in CollectTargetRenderers(maid))
                {
                    if(renderer==null||renderer.sharedMesh==null)continue;var kind=ClassifyMesh(renderer);
                    bool match=selection=="Navel"?kind==MeshMorphClass.NavelAccessory:kind==MeshMorphClass.OuterCloth && (selection=="Skirt"?IsSkirtLikeMesh(renderer):!IsSkirtLikeMesh(renderer)&&!IsPantsDiagnostic(renderer));
                    if(!match)continue;
                    var mesh=renderer.sharedMesh;var current=mesh.vertices;var rec=FindRecord(maid,renderer);string source="not-applied";
                    Vector3[] original=rec?.OrigVerts,applied=rec?.LastNewV;
                    if(original!=null&&applied!=null)source="mesh-record";
                    else foreach(var pair in _morphBaseBakeStates)if(pair.Value.Renderer==renderer&&pair.Value.Mesh==mesh){original=pair.Value.OriginalVerts;applied=pair.Value.BakedVerts;source="TMorph-base";break;}
                    bool known=original!=null&&applied!=null&&original.Length==current.Length&&applied.Length==current.Length;
                    if(!known){original=current;applied=current;source="not-applied";}
                    var binding=ActiveBinding(renderer);var navel=binding?.Navel??rec?.Navel;
                    string status=selection!="Navel"?(known?"recorded":"no-deformation-record"):navel!=null?"boundary-plane":"no-plane-check-navel-detection-and-mapping";
                    DiagnosticCells(manifest,mesh.name,GetTransformPath(renderer.transform),kind,renderer.gameObject.activeInHierarchy,renderer.enabled,current.Length,source,status);
                    var weights=NativeWeights(renderer);var bones=NativeBones(renderer);var liveWeights=mesh.boneWeights;var liveBones=renderer.bones;var liveBinds=mesh.bindposes;
                    NMatrix map=NMatrix.Identity;bool mapped=context!=null&&context.Maps.TryGetValue(renderer,out map);
                    for(int i=0;i<current.Length;i++)
                    {
                        var o=original[i];var d=applied[i];var v=current[i];var delta=d-o;
                        var ro=mapped?U(NVector.Transform(N(o),map)):new Vector3(float.NaN,float.NaN,float.NaN);
                        var rd=mapped?U(NVector.Transform(N(d),map)):ro;
                        var world=i<liveWeights.Length?DiagnosticSkin(v,liveWeights[i],liveBones,liveBinds):new Vector3(float.NaN,float.NaN,float.NaN);
                        var w=i<weights.Length?weights[i]:new BoneWeight();
                        float alpha=navel!=null?1:rec?.GrowthContext!=null&&rec.LastNewV!=null?Attachment(rec,i,rec.GrowthContext.Stage):0;
                        DiagnosticCells(csv,mesh.name,renderer.name,kind,i,source,o.x,o.y,o.z,d.x,d.y,d.z,v.x,v.y,v.z,delta.x,delta.y,delta.z,ro.x,ro.y,ro.z,rd.x,rd.y,rd.z,world.x,world.y,world.z,alpha,
                            rec?.TorsoOwnership!=null?rec.TorsoOwnership[i]:float.NaN,rec?.ThighGuardRestore!=null?rec.ThighGuardRestore[i]:0,rec?.BreastExcluded!=null&&rec.BreastExcluded[i],
                            DiagnosticBone(bones,w.boneIndex0),w.weight0,DiagnosticBone(bones,w.boneIndex1),w.weight1,DiagnosticBone(bones,w.boneIndex2),w.weight2,DiagnosticBone(bones,w.boneIndex3),w.weight3);
                    }
                    if(navel==null)continue;
                    for(int k=0;k<NavelSamples;k++)
                    {var o=navel.OriginalRing[k];var m=navel.MorphedRing[k];var w=navel.PosedRing[k];var bary=navel.Barycentric[k];
                        DiagnosticCells(boundary,mesh.name,navel.Source.Mesh.name,k,navel.Triangles[k],navel.Indices[navel.SampleIndices[k*3]],navel.Indices[navel.SampleIndices[k*3+1]],navel.Indices[navel.SampleIndices[k*3+2]],bary.X,bary.Y,bary.Z,o.X,o.Y,o.Z,m.X,m.Y,m.Z,w.X,w.Y,w.Z);}
                    WriteNavelPlane(planes,mesh.name,"original-reference",navel.OriginalFrame,navel);
                    WriteNavelPlane(planes,mesh.name,"baked-reference",navel.BakedFrame,navel);
                    if(binding!=null)WriteNavelPlane(planes,mesh.name,"last-world",navel.LastFrame,navel);
                }
            }
            return directory;
        }
        static void WriteNavelPlane(TextWriter writer,string name,string kind,NMatrix m,NavelAttachment a)
            =>DiagnosticCells(writer,name,kind,m.M41,m.M42,m.M43,m.M11,m.M12,m.M13,m.M21,m.M22,m.M23,m.M31,m.M32,m.M33,a.Radius,a.CenterY);
    }
}
