using System;
using System.Collections.Generic;
using System.Linq;
using COM3D2.Pregnancy.Plugin.Growth.Numerics;
namespace COM3D2.Pregnancy.Plugin.Growth;

// Rest-space field shared by visible skin and clothing; no posed coordinates.
internal sealed class MaterialBlendField
{
    private readonly record struct CellKey(int X,int Y,int Z);
    private readonly record struct Point(Vector3 Position,float Weight);
    private readonly float _cell;
    private readonly Dictionary<CellKey,List<Point>> _points=new();
    internal MaterialBlendField(float span){_cell=Scalar.Max(.0001f,span*.10f);}
    private CellKey Cell(Vector3 v)=>new CellKey((int)Scalar.Floor(v.X/_cell),(int)Scalar.Floor(v.Y/_cell),(int)Scalar.Floor(v.Z/_cell));
    internal void Add(Vector3 p,float alpha)
    {var cell=Cell(p);if(!_points.TryGetValue(cell,out var points))_points[cell]=points=new();points.Add(new Point(p,alpha));}
    internal float Sample(Vector3 p,float fallback)
    {
        var (cx,cy,cz)=Cell(p);float sum=0,value=0,min=_cell*_cell*4,nearest=fallback;
        for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++)for(int z=-2;z<=2;z++)
        if(_points.TryGetValue(new CellKey(cx+x,cy+y,cz+z),out var points))
            foreach(var point in points)
            {
                float d=Vector3.DistanceSquared(p,point.Position);
                if(d<1e-12f)return point.Weight;
                if(d<min){min=d;nearest=point.Weight;}
                if(d>_cell*_cell)continue;
                float w=1/Scalar.Max(d,_cell*_cell*.001f);sum+=w;value+=w*point.Weight;
            }
        return sum>0?value/sum:nearest;
    }
}
