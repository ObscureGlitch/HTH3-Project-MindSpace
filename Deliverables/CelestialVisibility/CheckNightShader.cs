using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

public static class CheckNightShader
{
    [DllImport("d3dcompiler_47.dll",CharSet=CharSet.Ansi,CallingConvention=CallingConvention.StdCall)]
    private static extern int D3DCompile(byte[] source,UIntPtr length,string name,IntPtr defines,IntPtr include,
        string entry,string target,uint flags,uint flags2,out IntPtr code,out IntPtr errors);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate IntPtr BufferPointer(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate UIntPtr BufferSize(IntPtr self);
    private static string BlobText(IntPtr blob)
    {
        if(blob==IntPtr.Zero)return "";
        IntPtr table=Marshal.ReadIntPtr(blob);
        var pointer=(BufferPointer)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(table,IntPtr.Size*3),typeof(BufferPointer));
        var size=(BufferSize)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(table,IntPtr.Size*4),typeof(BufferSize));
        return Marshal.PtrToStringAnsi(pointer(blob),(int)size(blob).ToUInt64()).TrimEnd('\0');
    }
    public static string Run(string shaderFolder,string liveFolder)
    {
        string shared=File.ReadAllText(Path.Combine(shaderFolder,"WellnessSkySampling.hlsl"));
        foreach(string include in new[]{"WellnessNightSky.hlsl","WellnessDistantLandscape.hlsl","WellnessMoon.hlsl"})
        {
            string path=Path.Combine(shaderFolder,include);
            if(!File.Exists(path))path=Path.Combine(liveFolder,include);
            shared=shared.Replace("#include \""+include+"\"",File.ReadAllText(path));
        }
        string constants="\ncbuffer Test : register(b0) { float4 _NightEffects,_AuroraShape; float4 _MeteorHeads[3], _MeteorTangents[3], _MeteorSides[3]; float night; float storm; float3 sunDirection; };\n";
        string wrapper=
            "float4 main(float4 pixel:SV_POSITION,float3 direction:TEXCOORD0):SV_Target { return float4(WellnessSky(normalize(direction),"+
            "float3(.008,.014,.032),float3(.035,.053,.085),float3(.02,.03,.04),sunDirection,night,storm,0),1); }";
        byte[] source=Encoding.UTF8.GetBytes(constants+shared+wrapper);IntPtr code,errors;
        int result=D3DCompile(source,new UIntPtr((uint)source.Length),"SharedNightSkyCpuCompile",IntPtr.Zero,IntPtr.Zero,"main","ps_5_0",1u<<11,0,out code,out errors);
        try
        {
            string messages=BlobText(errors);
            if(result<0)throw new Exception("DirectX shader compile failed: "+messages);
            return "PASS: exact shared sun/moon/sky/aurora/meteor/horizon HLSL compiles to DirectX 11 pixel-shader bytecode on CPU. Used by both sky and pond. No device or rendering context created.\n"+messages;
        }
        finally{if(code!=IntPtr.Zero)Marshal.Release(code);if(errors!=IntPtr.Zero)Marshal.Release(errors);}
    }
}
