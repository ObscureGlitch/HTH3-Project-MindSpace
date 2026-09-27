using System;
using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TheLastWatch.Integrations
{
    public static class PresagePrivatePipe
    {
        public static NamedPipeServerStream Create(string name)
        {
            // Unity's Windows Mono does not implement WindowsIdentity.User/Owner, which
            // PipeOptions.CurrentUserOnly calls internally. Use the process token's SID
            // and an explicit protected DACL; never fall back to a public/default pipe.
            if(System.Environment.OSVersion.Platform!=PlatformID.Win32NT)
                return new NamedPipeServerStream(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
            // Mono also ignores PipeSecurity on its managed constructor. Supply the DACL
            // directly to Windows at creation: no temporary public pipe, no relaxed fallback.
            // Native SDDL avoids Windows-only managed assemblies absent from Unity's API profile.
            IntPtr descriptor;uint descriptorLength;
            if(!ConvertStringSecurityDescriptor("D:P(A;;GA;;;"+CurrentProcessUser()+")",1,out descriptor,out descriptorLength))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var attributes=new SecurityAttributes{Length=Marshal.SizeOf(typeof(SecurityAttributes)),
                    Descriptor=descriptor,InheritHandle=0};
                // DUPLEX | OVERLAPPED | FIRST_PIPE_INSTANCE; BYTE | REJECT_REMOTE_CLIENTS.
                var handle=CreateNamedPipe("\\\\.\\pipe\\"+name,0x40080003,0x00000008,1,65536,65536,0,ref attributes);
                if(handle.IsInvalid){int error=Marshal.GetLastWin32Error();handle.Dispose();throw new Win32Exception(error);}
                try{return new NamedPipeServerStream(PipeDirection.InOut,true,false,handle);}
                catch{handle.Dispose();throw;}
            }
            finally{LocalFree(descriptor);}
        }

        internal static string CurrentProcessUser()
        {
            IntPtr token;
            if(!OpenProcessToken(GetCurrentProcess(),0x0008,out token))throw new Win32Exception(Marshal.GetLastWin32Error());
            IntPtr information=IntPtr.Zero;
            try
            {
                int length;
                GetTokenInformation(token,1,IntPtr.Zero,0,out length); // TokenUser; query buffer size.
                if(length<=0)throw new Win32Exception(Marshal.GetLastWin32Error());
                information=Marshal.AllocHGlobal(length);
                if(!GetTokenInformation(token,1,information,length,out length))throw new Win32Exception(Marshal.GetLastWin32Error());
                // TOKEN_USER starts with SID_AND_ATTRIBUTES; its first field points to the SID.
                IntPtr text;
                if(!ConvertSidToStringSid(Marshal.ReadIntPtr(information),out text))throw new Win32Exception(Marshal.GetLastWin32Error());
                try{return Marshal.PtrToStringUni(text);}
                finally{LocalFree(text);}
            }
            finally
            {
                if(information!=IntPtr.Zero)Marshal.FreeHGlobal(information);
                CloseHandle(token);
            }
        }

        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
        [DllImport("advapi32.dll",EntryPoint="ConvertSidToStringSidW",CharSet=CharSet.Unicode,SetLastError=true)]
        [return:MarshalAs(UnmanagedType.Bool)] private static extern bool ConvertSidToStringSid(IntPtr sid,out IntPtr text);
        [DllImport("advapi32.dll",EntryPoint="ConvertStringSecurityDescriptorToSecurityDescriptorW",CharSet=CharSet.Unicode,SetLastError=true)]
        [return:MarshalAs(UnmanagedType.Bool)] private static extern bool ConvertStringSecurityDescriptor(string text,uint revision,out IntPtr descriptor,out uint length);
        [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes
        { public int Length; public IntPtr Descriptor; public int InheritHandle; }
        [DllImport("kernel32.dll",EntryPoint="CreateNamedPipeW",CharSet=CharSet.Unicode,SetLastError=true)]
        private static extern SafePipeHandle CreateNamedPipe(string name,uint openMode,uint pipeMode,uint instances,
            uint outBuffer,uint inBuffer,uint timeout,ref SecurityAttributes attributes);
        [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);
        [DllImport("advapi32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(IntPtr process,uint access,out IntPtr token);
        [DllImport("advapi32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetTokenInformation(IntPtr token,int kind,IntPtr information,int length,out int needed);
    }
}
