using Opc.Ua;
using Opc.Ua.Configuration;
using System;

/**
* @author Emil Stubbe Kolvig-Raun
* @email emilkolvigraun@gmail.com
* @date - 09/28/2020
*/

namespace OPC_UA.NET_stack_wrapper
{ 
    /// <summary>
    /// ConfigUtils is a static class, that allows easy creation of ApplicationConfigurations as well as ApplicationInstances.
    /// </summary>
    public static class ConfigUtils   
    { 
        /// <summary>
        /// CurrentConfiguration holds the last application configuration.
        /// </summary>
        private static ApplicationConfiguration CurrentConfiguration;

        /// <summary>
        /// CurrentType holds the last application type.
        /// </summary>
        private static ApplicationType CurrentType;

        /// <summary>
        /// CurrentAppName holds the last application name. Note that it might default to DefaultVariables.
        /// </summary>
        private static string CurrentAppName;

        /// <summary>
        /// CreateAppConfig creates an application configuration and binds it to CurrentConfiguration.
        /// <para>@type : ApplicationType</para>
        /// <para>@operationTimeout : int, defaults to DefaultVariables.</para>
        /// <para>@sessionTimeout : int, defaults to DefaultVariables.</para>
        /// <para>@applicationName : string, defaults to DefaultVariables, "(default)".</para>
        /// </summary>
        /// <returns>ApplicationConfiguration, wither Server or Client. Also supports (ClientAndServer).</returns>
        public static ApplicationConfiguration CreateAppConfig(ApplicationType type, int operationTimeout = DefaultVariables.DefaultOperationTimeout, 
            int sessionTimeout = DefaultVariables.DefaultSessionTimeout, string applicationName = DefaultVariables.DefaultApplicationName)
        {
            // initialize an empty application configuration
            ApplicationConfiguration config = new ApplicationConfiguration()
            {
                // set applicatio name
                ApplicationName = applicationName,

                // generate uniform resource name based on host name and application name
                ApplicationUri = Utils.Format(@"urn:{0}:" + applicationName, System.Net.Dns.GetHostName()),

                // assign the application configuration type
                ApplicationType = type,

                // create the security configuration to
                SecurityConfiguration = new SecurityConfiguration
                {
                    // assign runtime store path for application certificate
                    ApplicationCertificate = new CertificateIdentifier {
                        StoreType = @"Directory",
                        StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\MachineDefault",
                        SubjectName = applicationName },

                    // assign path for trusted certificates
                    TrustedIssuerCertificates = new CertificateTrustList {
                        StoreType = @"Directory",
                        StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Certificate Authorities" },

                    // and for peers
                    TrustedPeerCertificates = new CertificateTrustList {
                        StoreType = @"Directory",
                        StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Applications" },

                    // and for rejected certificates
                    RejectedCertificateStore = new CertificateTrustList {
                        StoreType = @"Directory",
                        StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\RejectedCertificates" },

                    // auto accept untrusted certificates (all client become trusted, default to DefaultVariables, true)
                    AutoAcceptUntrustedCertificates = DefaultVariables.DefaultAutoAcceptUntrustedCertificates
                },  
                 
                // assign default transport configurations
                TransportConfigurations = new TransportConfigurationCollection(), 

                // assign transport quotas, default to default settings but assign operation timeout
                TransportQuotas = new TransportQuotas { OperationTimeout = operationTimeout },

                // for client connections assign session timeout
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = sessionTimeout },

                // use fault trace configuration
                TraceConfiguration = new TraceConfiguration()
            };

            // if the application type is of type Server or ClientAndServer, assign server configuration
            if (ApplicationType.Server == type || ApplicationType.ClientAndServer == type)

                // create server configuration, default settings to DefaultVariables
                config.ServerConfiguration = new ServerConfiguration()
                {
                    // limits the maximum number of results per request, that is gained from a reference node
                    MaxBrowseContinuationPoints = DefaultVariables.DefaultMaxBrowseContinuationPoints,

                    // setting session timeout for connecting clients that are unresponsive, before connection is closed
                    MaxSessionTimeout = DefaultVariables.DefaultMaxSessionTimeout,

                    // minimal session timeout before considering connection close
                    MinSessionTimeout = DefaultVariables.DefaultMinSessionTimeout,

                    // setting the max session count, e.g the maximal number of client connected at a time
                    MaxSessionCount = DefaultVariables.DefaultMaxSessionCount,

                    // setting the maximal publishing interval, to limit the amount of memory a server has to store for publishings
                    MaxPublishingInterval = DefaultVariables.DefaultMaxPublishingInterval,

                    // setting the maximal queue size, if above is requested return equal to bound
                    MaxMessageQueueSize = DefaultVariables.DefaultMaxMessageQueueSize,

                    // limiting the number of notifications to store on each server cycle
                    MaxNotificationQueueSize = DefaultVariables.DefaultMaxNotificationQueueSize,

                    // limiting the amount of data to return per publish
                    MaxNotificationsPerPublish = DefaultVariables.DefaultMaxNotificationsPerPublish,

                    // setting the maximal allowed publishes per server
                    MaxPublishRequestCount = DefaultVariables.DefaultMaxPublishRequestCount,

                    // limiting the amount of allowed subscriptions to maintain
                    MaxSubscriptionCount = DefaultVariables.DefaultMaxSubscriptionCount,

                    // determining for how long a subscription can survive
                    MaxSubscriptionLifetime = DefaultVariables.DefaultMaxSubscriptionLifetime
                }; 
            
            // validate the application configuration
            config.Validate(type).GetAwaiter().GetResult();

            // if default configuration is to trust all certifiactes
            if (config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
            { 
                // define accept to also equal BadCertificateUntrusted
                // to make sure that the connection is created from current side
                config.CertificateValidator.CertificateValidation += (s, e) => {  
                    e.Accept = (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted); 
                };
            }  

            // assign the configuration to CurrentConfiguration
            CurrentConfiguration = config;

            // assign the type to CurrentType
            CurrentType = type;

            // assign the name to CurrentAppName
            CurrentAppName = applicationName;

            // return ApplicationConfiguration
            return config;
        }

        /// <summary>
        /// CreateAppInstance creates an application instance using an ApplicationConfiguration.
        /// <para>@config : ApplicationConfiguration</para>
        /// <para>@checkApplicationCert : bool, defaults to DefaultVariables.</para>
        /// <para>@applicationName : string</para>
        /// <para>@keySize : ushort, defaults to DefaultVariables.</para>
        /// </summary>
        /// <returns>ApplicationInstance</returns>
        public static ApplicationInstance CreateAppInstance(ApplicationConfiguration config, bool checkApplicationCert = DefaultVariables.DefaultCheckAppCert,
            ushort keySize = DefaultVariables.DefaultRequiredSizedKey)
        {

            // create the new application instance using the configs from ApplicationConfiguration
            ApplicationInstance application = new ApplicationInstance
            { 
                // reuse the name from the config
                ApplicationName = config.ApplicationName,

                // reuse the type from the config
                ApplicationType = config.ApplicationType, 
                
                // assign the config to the instance
                ApplicationConfiguration = config 
            };

            // validate the instance certificate, to issue later
            application.CheckApplicationInstanceCertificate(checkApplicationCert, keySize).GetAwaiter().GetResult();

            // return the application instance 
            return application;
        }

        /// <summary>
        /// BuildAppInstance utilizes the CurrentConfiguration to automatically create the application instance.
        /// CreateAppConfig must have been called prior to BuildAppInstance.
        /// <para>@checkApplicationCert : bool, defaults to DefaultVariables.</para>
        /// <para>@keySize : ushort, defaults to DefaultVariables.</para>
        /// </summary> 
        /// <returns>ApplicationInstance, throws ArgumentNullException is CurrentConfiguration is null</returns>
        public static ApplicationInstance BuildAppInstance(bool checkApplicationCert = DefaultVariables.DefaultCheckAppCert, ushort keySize = DefaultVariables.DefaultRequiredSizedKey)
        {
            if (CurrentConfiguration == null) throw new ArgumentNullException("ApplicationConfiguration");
            return CreateAppInstance(CurrentConfiguration, checkApplicationCert, keySize);
        }  
         
        /// <summary>
        /// Helper method to return the CurrentConfiguration.
        /// </summary>
        /// <returns>ApplicationConfiguration, throws ArgumeentNullException is CurrentConfiguration is null</returns>
        public static ApplicationConfiguration GetCurrentAppConfig()
        {
            // validate wether CurrentConfiguration is null, if true throw exception
            if (CurrentConfiguration == null) throw new ArgumentNullException("ApplicationConfiguration");

            // else, return CurrentConfiguration
            return CurrentConfiguration;
        }

        /// <summary>
        /// Helper method to retrieve the CurrentApplicationType.
        /// </summary>
        /// <returns>ApplicationType, null if unassigned</returns>
        public static ApplicationType GetCurrentAppType()
        {
            // return CurrentType, ApplicationType
            return CurrentType;
        } 

        /// <summary>
        /// Helper method to retrieve the CurrentApplicationName.
        /// </summary>
        /// <returns>string, null if unassigned</returns>
        public static string GetCurrentAppName()
        {
            return CurrentAppName;
        }
    } 
}
