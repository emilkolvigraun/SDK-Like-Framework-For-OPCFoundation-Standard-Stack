/**
* @author Emil Stubbe Kolvig-Raun
* @email emilkolvigraun@gmail.com
* @date - 09/28/2020
*/

namespace OPC_UA.NET_stack_wrapper
{
    /// <summary>
    /// DefaultVariables contains variables that determine important program settings.
    /// Adjust these to customi´ze application.
    /// </summary>
    public static class DefaultVariables
    {
        /// <summary>
        /// The default application name used by ConfigUtils if not provided as parameter.
        /// </summary>
        public const string DefaultApplicationName = "(default)";

        /// <summary>
        /// The default operation timeout. Various functionality default to this setting.
        /// </summary>
        public const int DefaultOperationTimeout = 15000; 

        /// <summary>
        /// Default session timeout, for server and client configurations.
        /// </summary>
        public const int DefaultSessionTimeout = 60000;

        /// <summary>
        /// Determines whether any applicaiotn certificate should be validated when created
        /// </summary>
        public const bool DefaultCheckAppCert = false;

        /// <summary>
        /// Determines whether a ApplicationInstance must auto accept untrusted certificates. Use this with caution.
        /// </summary>
        public const bool DefaultAutoAcceptUntrustedCertificates = true;

        /// <summary>
        /// Determiens the publishing interval for a subscription session.
        /// </summary>
        public const int DefaultPublishingInterval = 1000;

        /// <summary>
        /// Sets the interval for a KeepALiveHandler method, which the OpcClient implements to maintain the connection.
        /// </summary>
        public const int DefaultKeepAliveInterval = 5000;

        /// <summary>
        /// Determines how many times a client should try to reconnect if unsuccessful before setting Operating to false.
        /// </summary>
        public const int DefaultMaxConnectionRetries = 10;

        /// <summary>
        /// Sets the default minimum sized key for certifcates.
        /// </summary>
        public const ushort DefaultMinimumSizedKey = 0; 

        /// <summary>
        /// Sets the default key size for certificates.
        /// </summary>
        public const ushort DefaultRequiredSizedKey = 2048;
        
        /// <summary>
        /// Is a server configuration the limits the number of references to obtain from a node during browse.
        /// </summary>
        public const int DefaultMaxBrowseContinuationPoints = 10000;

        /// <summary>
        /// Server configuration that determines maxmimal session timeout, before closing/forgetting connection.
        /// </summary>
        public const int DefaultMaxSessionTimeout = 3600000;

        /// <summary>
        /// Sever configuration the determines minimal session timeout.
        /// </summary>
        public const int DefaultMinSessionTimeout = 360000;

        /// <summary>
        /// Server configuration that limits the amount of ongoing sessions at a given timeframe.
        /// </summary>
        public const int DefaultMaxSessionCount = 1000;

        /// <summary>
        /// Server confifuration the sets the maximal publishing interval.
        /// </summary>
        public const int DefaultMaxPublishingInterval = 15000;

        /// <summary>
        /// Server configuration that limits the number of messages to maintain in queue.
        /// </summary>
        public const int DefaultMaxMessageQueueSize = 1000;

        /// <summary>
        /// Server configuration that limits the maximal notification queue size. 
        /// </summary>
        public const int DefaultMaxNotificationQueueSize = 100000;

        /// <summary>
        /// Server configuration that sets the maximal notifactions per publish, e.g 1 filled queue would require 10 publishings.
        /// </summary>
        public const int DefaultMaxNotificationsPerPublish = 10000;

        /// <summary>
        /// Server configuration that limits the number of allowed publish counts
        /// </summary>
        public const int DefaultMaxPublishRequestCount = 100000;

        /// <summary>
        /// Server configuration that sets the maximal number of subscriptions at a time.
        /// </summary>
        public const int DefaultMaxSubscriptionCount = 10000;

        /// <summary>
        /// Server configuration that determines, for how long a single subscription can operate.
        /// </summary>
        public const int DefaultMaxSubscriptionLifetime = 999999999;
   }
}
