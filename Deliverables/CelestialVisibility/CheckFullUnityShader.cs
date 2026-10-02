using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

public static class CheckFullUnityShader
{
    [DllImport("d3dcompiler_47.dll",CharSet=CharSet.Ansi,CallingConvention=CallingConvention.StdCall)]
    private static extern int D3DCompile(byte[] source,UIntPtr length,string name,IntPtr defines,IntPtr include,string entry,string target,uint flags,uint flags2,out IntPtr code,out IntPtr errors);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate IntPtr BlobPointer(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate UIntPtr BlobSize(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall,CharSet=CharSet.Ansi)] private delegate int OpenInclude(IntPtr self,int type,string name,IntPtr parent,out IntPtr data,out uint bytes);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CloseInclude(IntPtr self,IntPtr data);
    private static string ReadBlob(IntPtr blob)
    {
        if(blob==IntPtr.Zero)return "";IntPtr v=Marshal.ReadIntPtr(blob);
        var ptr=(BlobPointer)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(v,IntPtr.Size*3),typeof(BlobPointer));
        var len=(BlobSize)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(v,IntPtr.Size*4),typeof(BlobSize));
        return Marshal.PtrToStringAnsi(ptr(blob),(int)len(blob).ToUInt64()).TrimEnd('\0');
    }
    public static string Run(string project,string shaderFile,string entry,string replacementRoot)
    {
        var includes=new Dictionary<IntPtr,string>();var failures=new List<string>();int count=0;
        OpenInclude open=(IntPtr self,int type,string name,IntPtr parent,out IntPtr data,out uint bytes)=>
        {
            data=IntPtr.Zero;bytes=0;
            try
            {
                string path;
                if(name.StartsWith("Packages/"))
                {
                    string[] parts=name.Split('/');
                    string package=Directory.GetDirectories(Path.Combine(project,"Library/PackageCache"),parts[1]+"@*").Single();
                    path=Path.Combine(package,string.Join("/",parts.Skip(2).ToArray()));
                }
                else if(name.StartsWith("Assets/"))path=Path.Combine(project,name);
                else path=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(includes.ContainsKey(parent)?includes[parent]:shaderFile),name));
                string readPath=path;
                string assets=Path.GetFullPath(Path.Combine(project,"Assets/TherapyGame"))+Path.DirectorySeparatorChar;
                if(path.StartsWith(assets,StringComparison.OrdinalIgnoreCase))
                {
                    string replacement=Path.Combine(replacementRoot,path.Substring(assets.Length));
                    if(File.Exists(replacement))readPath=replacement;
                }
                byte[] content=Encoding.UTF8.GetBytes(File.ReadAllText(readPath).Replace("#include_with_pragmas","#include"));
                data=Marshal.AllocHGlobal(content.Length);Marshal.Copy(content,0,data,content.Length);bytes=(uint)content.Length;
                includes[data]=path;count++;return 0;
            }
            catch(Exception e){failures.Add(name+": "+e.Message);return unchecked((int)0x80004005);}
        };
        CloseInclude close=(self,data)=>{Marshal.FreeHGlobal(data);includes.Remove(data);return 0;};
        IntPtr table=Marshal.AllocHGlobal(IntPtr.Size*2),handler=Marshal.AllocHGlobal(IntPtr.Size);
        Marshal.WriteIntPtr(table,0,Marshal.GetFunctionPointerForDelegate(open));Marshal.WriteIntPtr(table,IntPtr.Size,Marshal.GetFunctionPointerForDelegate(close));Marshal.WriteIntPtr(handler,table);
        string file=File.ReadAllText(shaderFile);int start=file.IndexOf("HLSLPROGRAM",StringComparison.Ordinal)+11,end=file.IndexOf("ENDHLSL",start,StringComparison.Ordinal);
        string stage=entry=="Vert"?"VERTEX":"FRAGMENT";
        string defines="#define SHADER_API_D3D11 1\n#define UNITY_COMPILER_HLSL 1\n#define SHADER_TARGET 30\n#define UNITY_VERSION 600630\n#define SHADER_STAGE_"+stage+" 1\n#define SHADER_API_DESKTOP 1\n#define UNITY_ENABLE_REFLECTION_BUFFERS 1\n#define UNITY_LIGHTMAP_FULL_HDR 1\n#define UNITY_SPECCUBE_BLENDING 1\n#define UNITY_SPECCUBE_BOX_PROJECTION 1\n#define UNITY_USE_DITHER_MASK_FOR_ALPHABLENDED_SHADOWS 1\n";
        byte[] source=Encoding.UTF8.GetBytes(defines+file.Substring(start,end-start));IntPtr code=IntPtr.Zero,errors=IntPtr.Zero;
        try
        {
            int result=D3DCompile(source,new UIntPtr((uint)source.Length),shaderFile,IntPtr.Zero,handler,entry,entry=="Vert"?"vs_5_0":"ps_5_0",1u<<12,0,out code,out errors);
            string message=ReadBlob(errors);
            if(result<0)throw new Exception("Full Unity source compile failed: "+message+"\n"+string.Join("\n",failures));
            return "PASS: "+Path.GetFileName(shaderFile)+" "+entry+" with "+count+" actual Unity/URP includes compiled on CPU.\n"+message;
        }
        finally
        {
            if(code!=IntPtr.Zero)Marshal.Release(code);if(errors!=IntPtr.Zero)Marshal.Release(errors);
            foreach(IntPtr ptr in includes.Keys)Marshal.FreeHGlobal(ptr);
            Marshal.FreeHGlobal(handler);Marshal.FreeHGlobal(table);GC.KeepAlive(open);GC.KeepAlive(close);
        }
    }
}
