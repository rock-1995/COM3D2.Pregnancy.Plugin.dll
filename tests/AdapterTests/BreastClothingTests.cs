using UnityEngine;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void RunBreastClothingTests()
    {
        Check(IsBreastBoneName("mune_l") && IsBreastBoneName("mune_r_sub"),"native jiggle bones excluded");
        Check(!IsBreastBoneName("bip01 spine1a") && !IsBreastBoneName("bip01 spine1"),"spine is not a breast exclusion bone");
        // Same geometric problem at different mesh scales. A close unchanged
        // chest face must win over a moving abdomen face behind it.
        foreach(float size in new[]{.003f,.01f,.1f})
        {
            var c=new BodyAnchorContext{CellSize=size};
            var m=new BodyAnchorMesh{
                Original=new[]{new Vector3(0,0,0),new Vector3(size,0,0),new Vector3(0,size,0),
                    new Vector3(0,0,-size),new Vector3(size,0,-size),new Vector3(0,size,-size)},
                Morphed=new Vector3[6],Valid=Enumerable.Repeat(true,6).ToArray(),Affected=new bool[6],
                BreastExcluded=new[]{true,true,true,false,false,false}};
            m.Original.CopyTo(m.Morphed,0);
            for(int i=3;i<6;i++){m.Morphed[i]+=new Vector3(0,0,size*.5f);m.Affected[i]=true;}
            c.Meshes.Add(m);
            AddClothingRestTriangle(c,0,0,1,2,m.Original[0],m.Original[1],m.Original[2]);
            AddClothingRestTriangle(c,0,3,4,5,m.Original[3],m.Original[4],m.Original[5]);
            var p=new Vector3(size*.2f,size*.2f,size*.05f);
            Check(TryGetBreastClothingTarget(c,p,size*2,1,out var target,out bool excluded),"full rest surface found");
            Check(excluded && Vector3.Distance(target,p)==0,"unchanged chest stays at native position");
            m.BreastExcluded[1]=false;m.Affected[1]=true;
            m.Morphed[1]+=new Vector3(0,0,size*.4f);
            Check(TryGetBreastClothingTarget(c,p,size*2,1,out target,out excluded),"mixed boundary found");
            Check(!excluded && target.z>p.z,"mixed chest-abdomen triangle carries fabric");
            Check(Vector3.Distance(target,p)<size*.4f,"boundary transport remains within local deformation");
            Check(TryGetBreastClothingTarget(c,p,size*2,0,out target,out excluded),"zero multiplier found");
            Check(Vector3.Distance(target,p)<size*1e-5f,"zero multiplier leaves cloth unchanged");
            Check(!TryGetBreastClothingTarget(c,new Vector3(size*10,0,0),size,1,out _,out _),"remote chest does not own fabric");
        }
        Console.WriteLine($"PASS: {tests} breast-boundary assertions (native bone mask, unchanged chest, mixed face, mesh scale, multiplier).");
    }
}
