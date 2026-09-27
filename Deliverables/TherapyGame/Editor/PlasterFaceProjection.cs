using System;

namespace TherapyGame.Editor
{
    // Fit each planar face separately. Nearest triangles at a bevel can belong to
    // a different UV island even when their normals point partly toward this face.
    public static class PlasterFaceProjection
    {
        public sealed class Map
        {
            public double X,Y,U,V,UX,UY,VX,VY;
            public double[] At(double x,double y)=>new[]{U+UX*(x-X)+UY*(y-Y),V+VX*(x-X)+VY*(y-Y)};
        }
        public static Map Fit(double[,] samples)
        {
            int n=samples.GetLength(0);
            if(n<3||samples.GetLength(1)!=4)throw new ArgumentException("At least three x/y/u/v samples required.");
            var map=new Map();
            for(int i=0;i<n;i++){map.X+=samples[i,0];map.Y+=samples[i,1];map.U+=samples[i,2];map.V+=samples[i,3];}
            map.X/=n;map.Y/=n;map.U/=n;map.V/=n;
            double xx=0,xy=0,yy=0,xu=0,yu=0,xv=0,yv=0;
            for(int i=0;i<n;i++)
            {
                double x=samples[i,0]-map.X,y=samples[i,1]-map.Y,u=samples[i,2]-map.U,v=samples[i,3]-map.V;
                xx+=x*x;xy+=x*y;yy+=y*y;xu+=x*u;yu+=y*u;xv+=x*v;yv+=y*v;
            }
            double determinant=xx*yy-xy*xy;
            if(double.IsNaN(determinant)||determinant<=1e-12)throw new ArgumentException("Degenerate planar face.");
            map.UX=(xu*yy-yu*xy)/determinant;map.UY=(yu*xx-xu*xy)/determinant;
            map.VX=(xv*yy-yv*xy)/determinant;map.VY=(yv*xx-xv*xy)/determinant;
            for(int i=0;i<n;i++)
            {
                var uv=map.At(samples[i,0],samples[i,1]);
                if(double.IsNaN(uv[0])||double.IsNaN(uv[1])||Math.Abs(uv[0]-samples[i,2])>.002||Math.Abs(uv[1]-samples[i,3])>.002)
                    throw new ArgumentException("Face crosses lightmap islands; do not extrapolate.");
            }
            return map;
        }
        public static string Checks()
        {
            int checks=0;
            foreach(double width in new[]{.2,3,5.3})foreach(double height in new[]{.2,3,5.3})
            {
                var samples=new double[25,4];int i=0;
                for(int y=0;y<5;y++)for(int x=0;x<5;x++)
                {
                    double px=(x-2)*width/4,py=(y-2)*height/4;
                    samples[i,0]=px;samples[i,1]=py;samples[i,2]=.4+.02*px-.01*py;samples[i,3]=.6+.03*px+.04*py;i++;
                }
                var map=Fit(samples);
                for(int y=-3;y<=3;y++)for(int x=-3;x<=3;x++)
                {
                    double px=x*width/4,py=y*height/4;var uv=map.At(px,py);
                    if(Math.Abs(uv[0]-(.4+.02*px-.01*py))>1e-10||Math.Abs(uv[1]-(.6+.03*px+.04*py))>1e-10)throw new Exception("Face projection regression.");
                    checks++;
                }
            }
            bool rejected=false;try{Fit(new double[3,4]);}catch(ArgumentException){rejected=true;}
            if(!rejected)throw new Exception("Degenerate face was accepted.");
            return "PASS: "+checks+" planar projection checks, including narrow wall edges, rotated UVs, extrapolation and degenerate-face rejection.\n";
        }
    }
}
