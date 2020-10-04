using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;
using System;

/**
* @author Emil Stubbe Kolvig-Raun
* @email emilkolvigraun@gmail.com
* @date - 09/28/2020
*/

namespace OPC_UA.NET_stack_wrapper
{
     
   
    class OpcServer
    {

        ApplicationInstance Application;
        ApplicationConfiguration Configuration;
        StandardServer Server;
           
        public OpcServer()
        {
            Configuration = ConfigUtils.CreateAppConfig(ApplicationType.Server, applicationName: "(OpcServer)");
            Configuration.ApplicationUri = "opc.tcp://localhost:48580/"; 
            Application = ConfigUtils.BuildAppInstance(keySize: DefaultVariables.DefaultMinimumSizedKey);
            Application.ConfigSectionName = "Opc.ua.Server";

            Server = new StandardServer();

            foreach (EndpointDescription ed in Server.GetEndpoints())
                Console.WriteLine(ed.EndpointUrl);
        }

        public void Start()
        {
            Application.Start(Server).Wait();
        }
    }
}
