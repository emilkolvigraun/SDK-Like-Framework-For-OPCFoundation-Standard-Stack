using System;
using System.Collections.Generic;
using Opc.Ua; 
using Opc.Ua.Client;

// https://stackoverflow.com/questions/30573689/opc-ua-minimal-code-that-browses-the-root-node-of-a-server

namespace OPC_UA.NET_stack_wrapper
{
    class Program
    {
        static void Main(string[] args)   
        {
            OpcServer server = new OpcServer();
            server.Start();

            Console.ReadKey();
            
            //DefaultLog log = new DefaultLog(); 
            //log.SetLogLevel(new List<LogLevel>() { LogLevel.DEBUG });

            //OpcClient client = new OpcClient("opc.tcp://192.168.1.237:48540");//, log: log);
            //MakeAction(client); 
 
            //LdsClient cl = new LdsClient("opc.tcp://127.0.0.1:48540");
            //foreach (KeyValuePair<string, List<string>> apd in cl.MapFindServers())
            //{
            //    string st = "[ ";
            //    apd.Value.ForEach(a => { st += a.ToString() + ", "; });
            //    Console.WriteLine(apd.Key + " " + st + " ]");
            //}
            //cl.FindServersOnNetwork();
            //Console.ReadKey();   
        }      
           
        private static void MakeAction(OpcClient client)
        {
            Console.WriteLine("c=Connect,\nd=Disconnect,\nw=write,\nmt=Moniter(true),\nmf=Monitor(false),\nb=Browse,\nmi=monitedItems,\ns=Subscribe,\nbs=browseServer,\nss=stopSubscription,\nre=References,\nq=quit\nWaiting for input:");
            string key = Console.ReadLine();

            if (key == "c")
            {
                client.Connect(); MakeAction(client);
            }
            else if (key == "w")
            {
                foreach (ReferenceDescription rd in client.BrowseServer())
                    if (rd.DisplayName.ToString() == "TestTempSensor")
                    {
                        client.WriteToNode(rd, 2);
                    }

                MakeAction(client);
            }
            else if (key == "r")
            {
                foreach (ReferenceDescription rd in client.BrowseServer())
                    if (rd.DisplayName.ToString() == "TestTempSensor")
                    {
                        foreach (DataValue v in client.ReadNode(rd))
                            Console.WriteLine(rd.DisplayName.ToString() + " : " + v.Value.ToString() + ", " + v.SourceTimestamp);
                    }

                MakeAction(client);
            }
            else if (key == "mi")
            {
                foreach (MonitoredItem item in client.GetMonitoredItems())
                    Console.WriteLine(item.DisplayName);
                MakeAction(client);
            } 
            else if (key == "bs")
            {
                Console.WriteLine("Browsing server:");
                foreach (ReferenceDescription rd in client.BrowseServer())
                {
                    //Console.WriteLine(rd.DisplayName.ToString() + " " + rd.NodeClass.ToString() + " " + rd.TypeDefinition.ToString() + " " + rd.NodeId.ToString() + "\n--- ");
                    if (true)
                    {
                        Console.WriteLine(rd.DisplayName.ToString() + " " + rd.NodeClass.ToString() + " " + rd.TypeDefinition.ToString() + " " + rd.NodeId.ToString());
                        foreach (DataValue v in client.ReadNode(rd))
                            Console.WriteLine(v.WrappedValue + " " + v.Value);
                        Console.WriteLine("---");
                    }
                }
                 
                MakeAction(client);
            }
            else if (key == "d")
            {
                client.Disconnect(); MakeAction(client);
            }
            else if (key == "s")
            {
                foreach (ReferenceDescription rd in client.BrowseServer())
                    if (rd.DisplayName.ToString() == "TestHumidSensor" || rd.DisplayName.ToString() == "TestTempSensor")
                        client.Subscribe(rd, allowTypes: NodeClass.Variable); MakeAction(client);
            }
            else if (key == "re")
            {
                foreach (KeyValuePair<ReferenceDescription, MonitoredItemNotificationEventHandler> item in client.GetReferences())
                {
                    Console.WriteLine(item.Key.DisplayName.ToString() + " " + item.Key.NodeClass.ToString());
                }
                MakeAction(client);
            }
            else if (key == "mt")
            {
                client.Monitor(); MakeAction(client);
            }
            else if (key == "mf")
            {
                client.Monitor(false); MakeAction(client);
            }
            else if (key == "b")
            {
                foreach (ReferenceDescription rd in client.BrowseNode())
                    Console.WriteLine(rd.DisplayName.ToString() + " " + rd.NodeClass.ToString() + " " + rd.TypeDefinition.ToString() + " " + rd.NodeId.ToString());
                MakeAction(client);
            }
            else if (key == "ss")
            {
                foreach (KeyValuePair<ReferenceDescription, MonitoredItemNotificationEventHandler> item in client.GetReferences())
                {
                    client.StopSubscription(item.Key);
                }
                MakeAction(client);
            }
            else if (key == "q")
            {
                // quit
                client.Disconnect();
            }
            else MakeAction(client);
        }
    }

    
} 
