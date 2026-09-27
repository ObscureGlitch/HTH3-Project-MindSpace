using System;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
class ReproducePresagePipe
{
    static void Main()
    {
        try
        {
            using(var pipe=TheLastWatch.Integrations.PresagePrivatePipe.Create("MindSpaceAclTest-"+Guid.NewGuid().ToString("N")))
            {
                Console.WriteLine("EXPLICIT_PRIVATE_ACL_CONSTRUCTOR_OK");
                var task=pipe.WaitForConnectionAsync();
                Console.WriteLine("EXPLICIT_PRIVATE_ACL_WAIT_STARTED");
            }
        }
        catch(Exception e){Console.WriteLine(e.ToString());}
        foreach(var options in new[]{PipeOptions.Asynchronous,PipeOptions.Asynchronous|(PipeOptions)0x20000000})
        {
            Console.WriteLine("OPTIONS="+options);
            try
            {
                using(var pipe=new NamedPipeServerStream("MindSpaceTest-"+Guid.NewGuid().ToString("N"),PipeDirection.InOut,1,PipeTransmissionMode.Byte,options))
                {
                    Console.WriteLine("CONSTRUCTOR_OK");
                    var task=pipe.WaitForConnectionAsync();
                    Console.WriteLine("ASYNC_WAIT_STARTED");
                }
            }
            catch(Exception e){Console.WriteLine(e.ToString());}
        }
    }
}
