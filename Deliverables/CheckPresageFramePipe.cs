using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using TheLastWatch.Integrations;

class CheckPresageFramePipe
{
    static int Main(string[] args)
    {
        try
        {
            for(int iteration=0;iteration<3;iteration++)Check(args[0],args[1]);
            Console.WriteLine("UNITY_MONO_PRIVATE_COLOUR_PIPE: PASS (3 cycles, 90 full-size 720p frames per cycle)");return 0;
        }
        catch(Exception error){Console.WriteLine(error.GetType().Name+": "+error.Message);return 1;}
    }

    static void Check(string node,string receiver)
    {
        string name="MindSpacePresage-"+Guid.NewGuid().ToString("N");
        using(var pipe=PresagePrivatePipe.Create(name))
        {
            var security=pipe.GetAccessControl();
            var rules=security.GetAccessRules(true,true,typeof(SecurityIdentifier));
            // Verify the effective permissions, not Mono's AreAccessRulesProtected flag.
            // No inherited entries or group access may be present on this kernel object.
            if(rules.Count!=1)throw new Exception("Pipe DACL must grant access only to one user.");
            var rule=(PipeAccessRule)rules[0];
            var sid=(SecurityIdentifier)rule.IdentityReference;
            if(sid.Value!=PresagePrivatePipe.CurrentProcessUser()||rule.AccessControlType!=AccessControlType.Allow||rule.IsInherited||
                sid.IsWellKnown(WellKnownSidType.WorldSid)||sid.IsWellKnown(WellKnownSidType.AuthenticatedUserSid)||
                sid.IsWellKnown(WellKnownSidType.BuiltinUsersSid))throw new Exception("Pipe is accessible to a broad group.");
            var connect=pipe.WaitForConnectionAsync();
            var start=new ProcessStartInfo(node,"\""+receiver+"\" "+name)
            {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            using(var process=Process.Start(start))
            {
                var output=process.StandardOutput.ReadToEndAsync();
                var errors=process.StandardError.ReadToEndAsync();
                try
                {
                    if(!connect.Wait(5000))throw new Exception("Node connection timed out.");
                    var header=new byte[24];
                    Put(header,0,1280);Put(header,4,720);Put(header,8,5120);Put(header,12,2);
                    var pixels=new byte[1280*720*4];
                    for(int frame=0;frame<90;frame++)
                    {
                        Buffer.BlockCopy(BitConverter.GetBytes((frame+1)*33333d),0,header,16,8);
                        for(int i=0;i<pixels.Length;i++)pixels[i]=(byte)((i+frame)%251);
                        if(!Send(pipe,header,pixels).Wait(5000))throw new Exception("Colour frame write timed out.");
                    }
                    pipe.Dispose();
                    if(!process.WaitForExit(5000)||process.ExitCode!=0)throw new Exception("Node receiver failed.");
                    if(!output.Result.Contains("PRIVATE_COLOUR_PIPE: PASS"))throw new Exception("Colour verification missing.");
                }
                finally{if(!process.HasExited){process.Kill();process.WaitForExit();}}
            }
        }
    }
    static void Put(byte[] bytes,int offset,int value){Buffer.BlockCopy(BitConverter.GetBytes(value),0,bytes,offset,4);}
    static async Task Send(NamedPipeServerStream pipe,byte[] header,byte[] pixels)
    {
        await pipe.WriteAsync(header,0,7).ConfigureAwait(false);
        await pipe.WriteAsync(header,7,17).ConfigureAwait(false);
        await pipe.WriteAsync(pixels,0,pixels.Length).ConfigureAwait(false);
    }
}
