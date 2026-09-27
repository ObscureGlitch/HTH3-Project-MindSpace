// Standalone numerical shim for CPU tests only. Never imported into Unity.
using System;
using System.Collections.Generic;
namespace UnityEngine {
 public struct Vector2 {
  public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;}
  public static Vector2 operator +(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
  public static Vector2 operator -(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
 }
 public struct Vector3 {
  public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
  public static Vector3 zero=>new Vector3();public static Vector3 up=>new Vector3(0,1,0);public static Vector3 down=>new Vector3(0,-1,0);
  public static Vector3 left=>new Vector3(-1,0,0);public static Vector3 right=>new Vector3(1,0,0);public static Vector3 forward=>new Vector3(0,0,1);public static Vector3 back=>new Vector3(0,0,-1);
  public float sqrMagnitude=>x*x+y*y+z*z;public Vector3 normalized=>this/(float)Math.Max(1e-20,Math.Sqrt(sqrMagnitude));
  public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
  public static Vector3 operator -(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
  public static Vector3 operator *(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);
  public static Vector3 operator /(Vector3 a,float b)=>a*(1/b);
  public static Vector3 Scale(Vector3 a,Vector3 b)=>new Vector3(a.x*b.x,a.y*b.y,a.z*b.z);
  public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
  public static Vector3 Cross(Vector3 a,Vector3 b)=>new Vector3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
  public static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a+(b-a)*t;
  public static float Distance(Vector3 a,Vector3 b)=>(float)Math.Sqrt((a-b).sqrMagnitude);
 }
 public struct Quaternion {
  private float x,y,z,w;
  public static Quaternion identity=>new Quaternion{w=1};
  public static Quaternion FromToRotation(Vector3 a,Vector3 b){
   a=a.normalized;b=b.normalized;float d=Vector3.Dot(a,b);
   if(d<-.99999f)return new Quaternion{x=1};
   Vector3 c=Vector3.Cross(a,b);float s=(float)Math.Sqrt((1+d)*2);
   return new Quaternion{x=c.x/s,y=c.y/s,z=c.z/s,w=s*.5f};
  }
  public static Vector3 operator *(Quaternion q,Vector3 v){
   var u=new Vector3(q.x,q.y,q.z);return u*(2*Vector3.Dot(u,v))+v*(q.w*q.w-Vector3.Dot(u,u))+Vector3.Cross(u,v)*(2*q.w);
  }
 }
 public struct Rect {
  public float x,y,width,height;public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;}
  public float xMin=>x;public float xMax=>x+width;public float yMin=>y;public float yMax=>y+height;
  public Vector2 center=>new Vector2(x+width*.5f,y+height*.5f);
  public static Rect MinMaxRect(float x0,float y0,float x1,float y1)=>new Rect(x0,y0,x1-x0,y1-y0);
  public bool Overlaps(Rect other)=>xMax>other.xMin&&xMin<other.xMax&&yMax>other.yMin&&yMin<other.yMax;
 }
 public static class Mathf {
  public const float PI=(float)Math.PI;
  public static float Min(float a,float b)=>Math.Min(a,b);public static float Max(float a,float b)=>Math.Max(a,b);
  public static float Abs(float a)=>Math.Abs(a);public static float Sin(float a)=>(float)Math.Sin(a);public static float Cos(float a)=>(float)Math.Cos(a);
 }
 public sealed class Mesh {
  public string name;public void SetVertices(List<Vector3> v){}public void SetTriangles(List<int> t,int i){}
  public void SetUVs(int i,List<Vector2> v){}public void RecalculateNormals(){}public void RecalculateBounds(){}
 }
}
