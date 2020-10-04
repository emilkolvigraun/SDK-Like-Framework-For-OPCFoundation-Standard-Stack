using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Gds.Client;
using System;
using System.Collections.Generic;

/**
* @author Emil Stubbe Kolvig-Raun
* @email emilkolvigraun@gmail.com
* @date - 10/02/2020
*/
  
namespace OPC_UA.NET_stack_wrapper
{
    /// <summary>
    /// The LocalDiscoveryClient is used to connect to a Server (or ClientAndServer) with LDS capability, and acquire registered servers. This class is a wrapper for the LocalDiscoveryServerClient.
    /// </summary>
    class LdsClient
    { 
        /// <summary>
        /// Application holds the current ApplicationInstance for the LDSClient.
        /// </summary>
        ApplicationInstance Application;

        /// <summary>
        /// ClientConfigurtation holds a GlobalDiscoveryClientConfiguration to be used by the ApplicationConfiguration
        /// </summary>
        GlobalDiscoveryClientConfiguration ClientConfiguration;

        /// <summary>
        /// LDS is the LocalDiscoveryServerClient
        /// </summary>
        LocalDiscoveryServerClient LDS;
        
        /// <summary>
        /// Keeps a variable to return the current client endpoint
        /// </summary>
        private string URL;
        
        /// <summary>
        /// LdsClient is a client for interacting the a LocalDiscoveryServer.
        /// <para>@endpoint : string, the URL of the LDS server</para>
        /// </summary>
        public LdsClient(string endpoint) 
        {
            // calls initialize, similar to ResetLDSCPurpose
            Initialize(endpoint);
        }   


        /// <summary>
        /// Initialize includes the logic which should normally be in the constructor, however in light of ResetLDSCPurpose, it exists outside of the constructor.
        /// This method initializes the objects nessecary for an LDS client to operate, and includes an application configuration and an application instance.
        /// <para>@endpoint : string</para>
        /// </summary>
        private void Initialize(string endpoint)
        {
            // assign the endpoint to the URL variable
            URL = endpoint; 

            // use ConfigUtils to generate a Client type aplication configuration
            ConfigUtils.CreateAppConfig(ApplicationType.Client, applicationName: "(GdsClient)");

            // use ConfigUtils to assign an ApplicationInstance to the Application variable
            Application = ConfigUtils.BuildAppInstance(keySize: DefaultVariables.DefaultMinimumSizedKey);

            // assign section name
            Application.ConfigSectionName = "(GdsClient)";

            // include the GlobalDiscoveryClientConfiguration into the ApplicationInstance and assign to Client configuration variable, in light of discovery url
            ClientConfiguration = Application.ApplicationConfiguration.ParseExtension<GlobalDiscoveryClientConfiguration>();

            // If the client configuration is null, because the extension was not parsed
            if (ClientConfiguration == null)

                // create a simple GDCConfiguration
                ClientConfiguration = new GlobalDiscoveryClientConfiguration()
                {
                    // assign the endpoint url to the discovery url
                    GlobalDiscoveryServerUrl = URL
                };

            // initialize the LocalDiscoveryServerClient with the application configuration obtainted from the application instance
            LDS = new LocalDiscoveryServerClient(Application.ApplicationConfiguration);
        }

        /// <summary>
        /// Reassign the endpoint and generate a new application, without creating a new LdsClient object.
        /// <para>@endpoint : string</para>
        /// </summary>
        public void ResetLDSCPurpose(string endpoint)
        {
            // call initialize and input endpoint as argument
            Initialize(endpoint);
        }

        /// <summary>
        /// FindServers() extracts and returns all servers which has been registered to the LDS.
        /// <para>Example:</para>
        /// <code>FindServers().ForEach(s => s.DiscoveryUrls.ForEach(d => urls.Add(d)));</code>
        /// </summary>
        /// <returns>A list of ApplicationDescription</returns>
        public List<ApplicationDescription> FindServers()
        {
            // use the LDS to find servers with the client configuration as input
            // endpoint transport profile uri is set to null
            return LDS.FindServers(ClientConfiguration.GlobalDiscoveryServerUrl, null);
        }

        /// <summary>
        /// Use the connection with the LDS to promt it to scan for servers of the same LAN. This only works if the LDS supports it.
        /// <para>Returns at maximal 1000 servers</para>
        /// </summary>
        /// <returns>List of ServerOnNetwork, empty if unsupported or none is found</returns>
        public List<ServerOnNetwork> FindServersOnNetwork()
        {
            // create output variable
            DateTime n;
            try
            {   
                // execute find servers on network using the GDS url and limit to 1000 servers
                // transport uri and filters are null
                List<ServerOnNetwork> servers = LDS.FindServersOnNetwork(ClientConfiguration.GlobalDiscoveryServerUrl, null, 0, 1000, null, out n);

                // if successful, return servers
                return servers;
            } 
            // if service is unsupported, do not crash
            catch (ServiceResultException)
            {
                // rather, return empty list
                return new List<ServerOnNetwork>();
            }
        }

        /** Helper Methods **/
        
        /// <summary>
        /// Utilizes FindServers to return a list of urls. These urls represent the endpoint of the registered servers.
        /// </summary>
        /// <returns>List of string, empty if none is found</returns>
        public List<string> FindServersUrl()
        {
            // initialize an empty list to contain strings
            List<string> urls = new List<string>();

            // foreach server and foreach discovery urls, add to the list of urls
            FindServers().ForEach(s => s.DiscoveryUrls.ForEach(d => urls.Add(d)));

            // return list of urls
            return urls;
        }

        /// <summary>
        /// Utilizes FindServers to return a list of application names. These names represent the configured names of the registered servers.
        /// </summary>
        /// <returns>List of string, empty if none is found</returns>
        public List<string> FindServersAppName()
        {
            // initialize a an empty list to contain strings
            List<string> names = new List<string>();

            // foreach server, add the application name to the list of names
            FindServers().ForEach(s => names.Add(s.ApplicationName.ToString()));

            // return the list of names
            return names;
        }

        /// <summary>
        /// Utilizes FindServers to return a dictionary that maps each discovered url to the associated configurations. This includes application names, type and uri.
        /// </summary>
        /// <returns>dictionary where key is string and value is list of strings, empty if none is found</returns>
        public Dictionary<string, List<string>> MapFindServers()
        {
            // initialize dictionary to include the servers
            Dictionary<string, List<string>> servers = new Dictionary<string, List<string>>();

            // foreach server
            FindServers().ForEach(s => {
                // and foreach discovery url, add the configurations to the list and add the list to the key as a value, and add the key/value pair to the dictionary
                s.DiscoveryUrls.ForEach(d => servers.Add(d, new List<string>() { s.ApplicationName.ToString(), s.ApplicationType.ToString(), s.ApplicationUri.ToString() }));
            });

            // return dictionary
            return servers;
        } 
    }
} 
