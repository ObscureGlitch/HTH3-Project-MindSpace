using System;
using System.Collections.Generic;
using TherapyGame.Editor;
using UnityEngine;

public static class DoorClearancePreflight
{
    private struct Triangle {public Vector3 a,b,c;}
    public static string Run()
    {
        var faces=new List<Triangle>();
        var geometry=MindSpaceExteriorGeometry.Build();
        var oldFrame=new MindSpaceExteriorGeometry.Batch();
        foreach(float x in new[]{-3.09f,-1.77f})oldFrame.Box(new Vector3(x,1.22f,-3),new Vector3(.07f,2.44f,.22f));
        oldFrame.Box(new Vector3(-2.43f,2.45f,-3),new Vector3(1.4f,.09f,.22f));
        oldFrame.Box(new Vector3(-2.43f,2.71f,-3.1f),new Vector3(1.26f,.58f,.2f));
        oldFrame.Box(new Vector3(-3.28f,1.5f,-3.1f),new Vector3(.44f,3,.2f));
        oldFrame.Box(new Vector3(-2.43f,-.001f,-4.3f),new Vector3(2.3f,.1f,2.3f));
        geometry.Add("Original frame and porch",oldFrame);
        foreach(var batch in geometry.Values)for(int i=0;i<batch.vertices.Count;i+=3)
        {
            Vector3 a=batch.vertices[i],b=batch.vertices[i+1],c=batch.vertices[i+2];
            if(Math.Max(a.x,Math.Max(b.x,c.x))< -4.575f||Math.Min(a.x,Math.Min(b.x,c.x))> -1.525f||
                Math.Max(a.y,Math.Max(b.y,c.y))< -.07f||Math.Min(a.y,Math.Min(b.y,c.y))>2.53f||
                Math.Max(a.z,Math.Max(b.z,c.z))< -5.07f||Math.Min(a.z,Math.Min(b.z,c.z))> -2.77f)continue;
            faces.Add(new Triangle{a=a,b=b,c=c});
        }
        int oldColliding=0,newChecks=0;
        for(int i=0;i<=420;i++)
        {
            bool oldHit=false;float yaw=i*.25f;
            foreach(var f in faces)
            {
                if(DoorSwingGeometry.Hits(f.a,f.b,f.c,new Vector3(-3.05f,0,-3.08f),new Vector3(.62f,1.2f,0),new Vector3(.62f,1.2f,.0325f),yaw))oldHit=true;
                newChecks++;
                if(DoorSwingGeometry.Hits(f.a,f.b,f.c,DoorSwingGeometry.Pivot,DoorSwingGeometry.Center,DoorSwingGeometry.Size*.5f+new Vector3(.004f,.004f,.004f),yaw))
                    throw new Exception("Repaired door still intersects geometry at "+yaw+" degrees.");
            }
            if(oldHit)oldColliding++;
        }
        if(oldColliding==0)throw new Exception("Preflight must reproduce the original clipping.");
        return "PASS: reproduced original clipping at "+oldColliding+" / 421 angles. Repaired door passed "+newChecks+" triangle/angle checks including 4mm clearance padding; "+faces.Count+" nearby reference triangles. No Unity, GPU or Play mode.";
    }
}
