using Opc.Ua;
using Opc.Ua.Client;
using System;
using System.Collections.Generic;
using System.Linq;

/**
* @author Emil Stubbe Kolvig-Raun
* @email emilkolvigraun@gmail.com
* @date - 09/28/2020
*/

namespace OPC_UA.NET_stack_wrapper
{
    /// <summary>The Simple SDK OPC UA Client.
    /// <para>@endpoint : string</para> 
    /// <para>@maxRetries : int, limits the amount of times that the client will try to reconnect. If maxRetries is set to -1, it will continue to try to reconnect infinitely.</para> 
    /// <para>@log : AbstractLog, if none is provided the client defaults to DefaultLog which prints to console.</para> 
    /// If maxRetries is not set, the client defaults to DefaultMaxConnectionRetries in DefaultVariables.
    /// <para>Note that the default monitor handler outputs publishings to the log under tag MSG.</para> 
    /// </summary> 
    class OpcClient
    {
        /// <summary>
        /// Dictionary that contains any node that the client is subscribing to, associated with the particular callback
        /// </summary>
        private Dictionary<ReferenceDescription, MonitoredItemNotificationEventHandler> References;

        /// <summary>
        /// The callback used by the client if none is specified
        /// </summary>
        private MonitoredItemNotificationEventHandler DefaultNotificationCallback;

        /// <summary>
        /// The keep alive callback used by the client
        /// </summary>
        private KeepAliveEventHandler DefaultKeepAliveCallback;

        /// <summary>
        /// The client configuration, including certifacte and handle instructions
        /// </summary>
        private ApplicationConfiguration ClientConfiguration;

        /// <summary>
        /// The client session
        /// </summary>
        private Session ClientSession;

        /// <summary>
        /// The subscription session
        /// </summary>
        private Subscription SubscriptionSession;

        /// <summary>
        /// The item used to monitor server status
        /// </summary>
        private MonitoredItem MonitorServerStatusItem;

        /// <summary>
        /// The URL represents the endpoint
        /// </summary>
        private string URL;

        /// <summary>
        /// MaxRetries is the number of maximal allowed retries before the client stops operating
        /// </summary>
        private int MaxRetries;

        /// <summary>
        /// The current number of retries performed
        /// </summary>
        private int CurrentRetries;

        /// <summary>
        /// The bool used to indicate whether the client is operating successfully
        /// </summary>
        private bool Operating;

        /// <summary>
        /// Controls how often the server is set to publish values to the client
        /// </summary>
        private int CurrentPublishingInterval = DefaultVariables.DefaultPublishingInterval;

        /// <summary>
        /// The Log used to provide feedback. Implement a class that implements ILog, see DefaultLog for reference.
        /// </summary>
        private ILog Log;

       
        public OpcClient(string endpoint, 
            int maxRetries = DefaultVariables.DefaultMaxConnectionRetries,
            ILog log = null)
        {
            // operating is set to true, to indicate that the client is successfully running
            Operating = true;

            // current retries is initialized to 0
            CurrentRetries = 0;

            // endpoint is assigned to URL
            URL = endpoint;

            // max retries 
            MaxRetries = maxRetries; 

            if (log == null) Log = new DefaultLog();
            else Log = log; 

            // initialize empty dictionary
            References = new Dictionary<ReferenceDescription, MonitoredItemNotificationEventHandler>();

            // assign the default callbacks
            DefaultKeepAliveCallback = new KeepAliveEventHandler(DefaultKeepAliveCallbackMethod);
            DefaultNotificationCallback = new MonitoredItemNotificationEventHandler(DefaultCallbackMethod);

            // create the application client type instance with certificate
            ClientConfiguration = ConfigUtils.CreateAppConfig(ApplicationType.Client);
            ConfigUtils.BuildAppInstance();

            Log.Log(LogLevel.INFO, "Initialized client on endpoint: " + URL);
        } 

        /// <summary>Reconnects the client. Returns true to indicate success and false to indicate failure.
        /// <para>CurrentRetries is reset</para> 
        /// <para>Operating is set to true</para> 
        /// <para>References are not reset.</para> 
        /// First disconnects the client and resets all sessions. Then reconnects and resubscribes to all nodes that are still intact on the server.
        /// </summary>
        public bool Reconnect() 
        {
            // reset all sessions
            Disconnect();

            // reset retry count and set operating to true
            CurrentRetries = 0;
            Operating = true;

            // reconnect and resubscribe
            // return bool to indicate success
            return ResubscribeReferences();
        }

        /// <summary>Connects the client to the OPC UA server endpoint provided during initialization.
        /// <para>Params:</para> 
        /// <para>@callback : KeepAliveEventHandler</para> 
        /// <para>@timeout : uint = DefaultVariables.DefaultSessionTimeout</para> 
        /// <para>@keepAliveInterval : int = DefaultVariables.DefaultKeepAliveInterval</para> 
        /// The callback is used to keep the client alive or to act when the connection is bad. If no callback is provided, the client uses the default.
        /// </summary>
        public bool Connect(KeepAliveEventHandler callback = null, uint timeout = DefaultVariables.DefaultSessionTimeout, int keepAliveInterval = DefaultVariables.DefaultKeepAliveInterval)  
        { 
            try  
            {
                // initializes the endpoint by verifying that the destination exists
                // this will fail if the URL is wrong or the server is down
                EndpointDescription endpoint = CoreClientUtils.SelectEndpoint(URL, useSecurity: true, DefaultVariables.DefaultOperationTimeout);

                // creates the client session with the timeout provided. The session is asynchronous an runs in the background.
                ClientSession = Session.Create(ClientConfiguration, new ConfiguredEndpoint(null, endpoint, EndpointConfiguration.Create(ClientConfiguration)), false, "", timeout, null, null).GetAwaiter().GetResult();

                // if no callback is assigned, use the default keep alive event handler
                if (callback == null) callback = new KeepAliveEventHandler(DefaultKeepAliveCallbackMethod);

                // else, assign the default keep alive handler to the provided callback
                else DefaultKeepAliveCallback = callback;

                // add the callback to the client session
                ClientSession.KeepAlive += callback;    

                // start the session with the provided keep alive interval
                ClientSession.KeepAliveInterval = keepAliveInterval;
                
                // reset current retries to 0
                CurrentRetries = 0;

                // set operating to true
                Operating = true;

                Log.Log(LogLevel.INFO, "Connected client to endpoint: " + URL);

                // return true 
                return Operating;
            } 

            // if the client is not able to establish a connection, retry MacxRetries amount of times
            catch (Exception e) when (e is ArgumentNullException || e is ServiceResultException)
            {
                // increment retry count
                CurrentRetries += 1;

                // Add to log
                Log.Log(LogLevel.ERROR, "Failed to connect client to endpoint: " + URL + ", current retries: " + CurrentRetries);

                // if retry count is equal to max retries
                if (CurrentRetries >= MaxRetries)
                {
                    // reset all sessions
                    Disconnect();

                    // set operating to false
                    Operating = false;
                     
                    Log.Log(LogLevel.FATAL, "Client on endpoint: " + URL + " is no longer operating.");

                    // return false
                    return Operating;
                } 

                // set operating to false
                Operating = false;

                // or else try again
                return Connect();
            }
        }

        /// <summary>Disconnects the client from the server and resets the subscription session.
        /// <code>
        /// Operating is set to false
        /// </code>
        /// <code>
        /// SubscriptionSession is cleared.
        /// </code>
        /// <code>
        /// ClientSession is cleared.
        /// </code>
        /// <code>
        /// References are not cleared.
        /// </code>
        /// </summary>
        public void Disconnect()
        {
            // If the client session is already null
            // just make sure to set the subscription to null as well
            if (ClientSession == null)
            {
                SubscriptionSession = null;
                return; 
            }
            try
            { 
                // free, reset and clear unmanaged resources from client session
                ClientSession.Dispose();

                // if the subscription session is running
                // remove it from the client session
                if (SubscriptionSession != null && ClientSession.Connected)
                {
                    // remove the subscription from the client session
                    ClientSession.RemoveSubscription(SubscriptionSession);

                    // free, reset and clear unmanaged resources from subscription session
                    SubscriptionSession.Dispose();
                }

                // shut down client connection
                ClientSession.Close();

                // reset subscription session and client session
                SubscriptionSession = null;
                ClientSession = null;
            } 
            catch (Exception e) 
            { 
                // TODO: add to log
                Log.Log(LogLevel.WARN, "While trying to disconnect client from endpoint: " + URL + " experienced: " + e.Message);

                // if an exception is caught
                // reset both subscription and client session
                SubscriptionSession = null;
                ClientSession = null;
            }

            Log.Log(LogLevel.INFO, "Disconnected client from endpoint: " + URL);
        }

        /// <summary>Resets the purpose of the client but resetting References and reassigning the endpoint. 
        /// <para>Params:</para> 
        /// <para>@endpoint : string</para> 
        /// </summary> 
        public void ResetEndpoint(string endpoint)
        {
            // resets Operating to true
            Operating = true;

            // resets retries to 0
            CurrentRetries = 0;

            string t_endpoint = URL;
            // ressigns the endpoint
            URL = endpoint;

            // clears references
            References.Clear();

            // disconnects the client
            Disconnect();

            Log.Log(LogLevel.INFO, "Reset endpoint from " + t_endpoint + " to " + URL);
        }

        /// <summary>Returns the endpoint.
        /// </summary> 
        public string GetEndpoint()
        {
            // return URL, is a string
            return URL;
        }

        /// <summary>Indicates whether the client is currently operating successfully.
        /// </summary> 
        public bool IsOperatingSuccessfully()
        {
            // returns boolean operating
            return Operating;
        }


        /// <summary>Connection returns the current client connection associated with a boolean. The boolean indicates status. If the boolean is false, then ClientSession is null. The value of the boolean is inherited from Operating, which indicates whether the client is operating successfully.
        /// </summary>
        public KeyValuePair<bool, Session> Connection()
        {
            return new KeyValuePair<bool, Session>(Operating, ClientSession);
        } 

        /// <summary>StopSubscription stops the subscription to the input node reference.
        /// <para>Params:</para> 
        /// <para>@nodeRef : ReferenceDescription</para> 
        /// </summary>
        public void StopSubscription(ReferenceDescription nodeRef)
        {
            // if the client is operating successfully, a session is established and a subscription session is running
            // also, make sure that items are actually being monitored
            if(Operating && ClientSession != null && SubscriptionSession != null && SubscriptionSession.MonitoredItemCount > 0)
            {
                // initialise a handler for the dictionary operation output
                MonitoredItemNotificationEventHandler callback;

                // try to get the reference from the References list
                // if successful, set success to true and assign the reference to callback
                bool success = References.TryGetValue(nodeRef, out callback);
                ReferenceDescription tempNodeRef = nodeRef;

                // If the node cannot be found in references, because it does not have the
                // same serialization
                if (!success)

                    // try to find it manually
                    tempNodeRef = GetReference(nodeRef.DisplayName.ToString(), nodeRef.NodeId.ToString());

                // could the node not be found manually,
                // revert back to the ond 
                if (tempNodeRef != null)
                    // if the items exists in references, it is currently being monitored
                    // iterate the monitored items in the subscription session
                    foreach (MonitoredItem item in SubscriptionSession.MonitoredItems.ToList())
                    {
                        // once the monitored item is found
                        if (item.DisplayName == nodeRef.DisplayName)
                        {  
                            // remove the item from the subscription session
                            SubscriptionSession.RemoveItem(item);

                            // update the subscription session
                            ApplyChanges();  

                            // remove the node reference from References
                            References.Remove(tempNodeRef);

                            // Add to log 
                            Log.Log(LogLevel.INFO, "Cancelled subscription to " + tempNodeRef.DisplayName.ToString() + " on client with endpoint: " + URL);

                            // break the forloop
                            break;
                        } 
                    }
                
            }
        }

        /// <summary>Subscribe to the input node references. 
        /// <para>Params:</para> 
        /// <para>@nodeRef : ReferenceDescription</para> 
        /// <para>@callback : MonitoredItemNotificationEventHandler = null</para> 
        /// <para>@allowTypes : NodeClass = NodeClass.Unspecified</para> 
        /// <para>@publishingInterval : int = DefaultVariables.DefaultPublishingInterval, is only relevant if this is the first method called before any subscription.</para> 
        /// Only subscribes if the node is of the allowed types. If allowTypes is unspecified, all nodes are allowed.
        /// </summary>
        public void Subscribe(ReferenceDescription nodeRef, MonitoredItemNotificationEventHandler callback = null, NodeClass allowTypes = NodeClass.Unspecified, int publishingInterval = -1) 
        {
            SetPublishingInterval(publishingInterval);

            // verify that the client is operating successfully by performing a sanity check with the input publishing interval
            // verify also, that the node type corresponds with the allowed types. If allowed types are unspecified, then allow all types.
            if (SanityCheck() && (allowTypes == NodeClass.Unspecified || allowTypes == nodeRef.NodeClass))
            {
                // acquire the correct callback. If no callback is provided, use the default callback.
                callback = GetCallback(callback);

                // update References
                UpdateReferences(nodeRef, callback);

                // create monitored item and add the callback as notification
                // add the item to the subscription session
                SubscriptionSession.AddItem(CreateMonitoredItem(nodeRef, callback)); 

                // update subscription session
                ApplyChanges();

                // Add to log
                Log.Log(LogLevel.INFO, "Started subscribing to " + nodeRef.DisplayName.ToString() + " on client with endpoint: " + URL);
            }  
        } 

        /// <summary>Subscribe to the given collection of node references. 
        /// <para>Params:</para> 
        /// <para>@nodeRefs : ReferenceDescriptionCollection</para> 
        /// <para>@callback : MonitoredItemNotificationEventHandler = null</para> 
        /// <para>@allowTypes : NodeClass = NodeClass.Unspecified</para> 
        /// <para>@publishingInterval : int = DefaultVariables.DefaultPublishingInterval, is only relevant if this is the first method called before any subscription.</para> 
        /// Only subscribes if the node is of the allowed types. If allowTypes is unspecified, all nodes are allowed.
        /// </summary>
        public void Subscribe(ReferenceDescriptionCollection nodeRefs, MonitoredItemNotificationEventHandler callback = null, NodeClass allowTypes = NodeClass.Unspecified, int publishingInterval = -1)
        {
            // update current publishing interval
            SetPublishingInterval(publishingInterval);

            // perform sanity check with the input publishing interval
            // to only ever subscribe if the client is operating successfully
            if (SanityCheck())
            {
                // obtains the correct callback, in case of null default MonitoredItemNotificationEvent method is selected
                callback = GetCallback(callback);

                // initializes a new list of items to subscribe to
                var items = new List<MonitoredItem>(); 

                // iterates the reference collection to validate types
                foreach (ReferenceDescription rd in nodeRefs)
                {
                    // if the current node reference is of the correct type of allowTypes is unspecified
                    if (allowTypes == NodeClass.Unspecified || rd.NodeClass == allowTypes)
                    {
                        // create a monitored item and add it to items
                        items.Add(CreateMonitoredItem(rd, callback));

                        // update References
                        UpdateReferences(rd, callback);

                        // Add to log
                        Log.Log(LogLevel.INFO, "Started subscribing to " + rd.DisplayName.ToString() + " on client with endpoint: " + URL);
                    } 
                }  

                // if any monitored items are added to items...
                if (items.Count > 0)
                {
                    // add the monitored items to the subscription session
                    SubscriptionSession.AddItems(items);

                    // update the subscription session
                    ApplyChanges();
                }
            }
        }

        /// <summary>Monitor enables monitoring of server status. 
        /// <para>Params:</para> 
        /// <para>@monitor : bool = true</para> 
        /// <para>@callback : MonitoredItemNotificationEventHandler = null</para> 
        /// <para>@publishingInterval : int = -1, is only relevant if this is the first method called before any subscription. If equal to -1, defaults to DefaultVariable.</para> 
        /// Can be enabled/disabled by assigning monitor to true or false. If no assignment is done, Monitor will start. The command, including callback is stored for later. Thus, upon reconnect, monitoring will be initialised again unless disabled.
        /// </summary>
        public void Monitor(bool monitor = true, MonitoredItemNotificationEventHandler callback = null, int publishingInterval = -1)
        {
            // update the current publishing interval
            SetPublishingInterval(publishingInterval);

            // performs a sanity check with the input publishing interval
            // thus, if no subscription session was ever created, it will
            // be created with the input interval.
            if (SanityCheck())//TODO: add to log 

                // if monitor is false, monitoring must be disabled
                // however, only if ever previously initialised
                if (!monitor && MonitorServerStatusItem != null)
                {
                    // remove the item from the subscription session
                    SubscriptionSession.RemoveItem(MonitorServerStatusItem);

                    // update the subscription session
                    SubscriptionSession.ApplyChanges();

                    // monitoring is now disabled
                    MonitorServerStatusItem = null; 

                    // Add to log
                    Log.Log(LogLevel.INFO, "Stopped monitoring server status on client with endpoint: " + URL);
                }  

                // if monitoring is activated, but was previously activated
                // it is currently being activated again. Thus, reset the current subscription.
                else if (monitor && MonitorServerStatusItem != null && callback == null) 
                {
                    // remove the item from the subscription session
                    SubscriptionSession.RemoveItem(MonitorServerStatusItem);

                    // add the item to reset
                    SubscriptionSession.AddItem(MonitorServerStatusItem);

                    // update the subscription session
                    ApplyChanges();

                    // Add to log
                    Log.Log(LogLevel.INFO, "Started monitoring server status on client with endpoint: " + URL);
                }  
                else
                {
                    // remove the item from the subscription session
                    if (MonitorServerStatusItem != null) SubscriptionSession.RemoveItem(MonitorServerStatusItem);

                    // if no callback is given as input, use the default
                    if (callback == null) callback = new MonitoredItemNotificationEventHandler(DefaultCallbackMethod);

                    // create the particular monitoring item, same for all OPC servers
                    MonitoredItem item = new MonitoredItem(SubscriptionSession.DefaultItem) { DisplayName = "ServerStatusCurrentTime", StartNodeId = "i=2258" };

                    // add the callback
                    item.Notification += callback;

                    // assign the item to MonitorServerStatusItem, such that it is enabled
                    MonitorServerStatusItem = item;

                    // add the item to the subscription session
                    SubscriptionSession.AddItem(item); 

                    // update the subscription session
                    ApplyChanges();

                    // Add to log
                    Log.Log(LogLevel.INFO, "Started monitoring server status on client with endpoint: " + URL);
                } 
        }

        /** Utility */

        /// <summary>BrowseNode extracts all immediate children of the input node
        /// <para>Params:</para> 
        /// <para>@objectToBrowse : NodeId</para> 
        /// <para>@nodeClassMask : uint = (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method</para> 
        /// <para>@serverBrowse : bool = false, true if server objects should not be ignored</para> 
        /// By default, nodes of type Variable, Object and Method is returned. If no input node is provided, the method starts from the object node.
        /// </summary>
        public ReferenceDescriptionCollection BrowseNode(NodeId objectToBrowse = null, uint nodeClassMask = (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method, bool serverBrowse = false)
        { 
            // if no input node is provided, start from the object node
            if (objectToBrowse == null) objectToBrowse = ObjectIds.ObjectsFolder;

            // if the client is not operating successfully, return an empty collection
            if (!SanityCheck())
            {
                // Add to log
                Log.Log(LogLevel.INFO, "Cannot browse nodes on client with endpoint: " + URL + " due to bad connection.");
                return new ReferenceDescriptionCollection();
            }

            // create variables for session browse output
            ReferenceDescriptionCollection refs;
            Byte[] cp;

            try
            {
                // forward browse the server, that is, iterate the node tree downwards and output nodes of allowed types to "refs"
                ClientSession.Browse(null, null, objectToBrowse, 0u, BrowseDirection.Forward, ReferenceTypeIds.HierarchicalReferences, true, nodeClassMask, out cp, out refs);
            }
            catch (ServiceResultException)
            {
                Log.Log(LogLevel.WARN, "Client with endpoint: " + URL + " does not respond.");
                return new ReferenceDescriptionCollection();
            }

            // create variables for session browse output
            ReferenceDescriptionCollection n_refs = new ReferenceDescriptionCollection();

            // always browsing the server is usually not the best call
            if (!serverBrowse)
            {
                // remove anything related to server
                List<ReferenceDescription> server = refs.FindAll(a => a.DisplayName.ToString().Contains("Server"));
                foreach(ReferenceDescription rd in server)
                    refs.Remove(rd);
            } else
            {
                // remove anything not related to server
                List<ReferenceDescription> server = refs.FindAll(a => !a.DisplayName.ToString().Contains("Server"));
                foreach (ReferenceDescription rd in server)
                    refs.Remove(rd);
            } 

            return refs; 
        } 
          
        /// <summary>UpdatePublishingInterval sets the current publishing interval
        /// <para>@interval : int, milliseconds</para>
        /// This takes effect each time a subscription session is established, not on each individual subscription. For a new interval to take effect, the subscription session must be restarted.
        /// </summary>
        public void SetPublishingInterval(int interval)
        { 
            // if the internal is less than 0 it has defaulted to -1
            // thus, default again to DefailtVariables
            if (interval < 0) CurrentPublishingInterval = DefaultVariables.DefaultPublishingInterval;
            // else assign a custom interval
            else CurrentPublishingInterval = interval;
        }

        /// <summary>BrowseServerNode is a utility method that uses RecursiveBrowse to iterate the complete OPC node tree from the Server node.
        /// <example> Equivalent of:
        /// <code>
        /// ReferenceDescriptionCollection TopLevelNodes = BrowseNode(serverBrowse: true);
        /// </code>
        /// <code>
        /// ReferenceDescriptionCollection AllServerNodes = RecursiveBrowse(TopLevelNodes);
        /// </code>
        /// </example>
        /// </summary>
        public ReferenceDescriptionCollection BrowseServerNode()
        { 
            // calls browsenodes from the top level node
            // and performs recursive browse on the reference collection
            return RecursiveBrowse(BrowseNode(serverBrowse: true));
        }

        /// <summary>BrowseObjectsNode is a utility method that uses RecursiveBrowse to iterate the complete OPC node tree from the Objects node.
        /// <example> Equivalent of:
        /// <code>
        /// ReferenceDescriptionCollection TopLevelNodes = BrowseNode(serverBrowse: false);
        /// </code>
        /// <code>
        /// ReferenceDescriptionCollection AllServerNodes = RecursiveBrowse(TopLevelNodes);
        /// </code>
        /// </example>
        /// </summary>
        public ReferenceDescriptionCollection BrowseObjectsNode()
        {
            // calls browsenodes from the top level node
            // and performs recursive browse on the reference collection
            return RecursiveBrowse(BrowseNode(serverBrowse: false));
        } 

        /// <summary>RecursiveBrowse browses a complete ReferenceDescriptionCollection and all its children by DFS.
        /// <para>Params:</para> 
        /// <para>@nodeRefs : ReferenceDescriptionCollection</para> 
        /// Returns a new ReferenceDescriptionCollection that includes all nodes that exists within the nodes of the input collection, nodeRefs.
        /// </summary>
        public ReferenceDescriptionCollection RecursiveBrowse(ReferenceDescriptionCollection nodeRefs)
        {
            // Assignes the input collection to a temporary collection
            ReferenceDescriptionCollection t_nodeRefs = nodeRefs;
            
            // iterates all the nodes within the input collection
            // to extract the children of each node
            foreach (ReferenceDescription rd in nodeRefs.ToList())
            {
                // for each node call BrowseNode to obtain collection of children
                ReferenceDescriptionCollection rdc = BrowseNode((NodeId)rd.NodeId);

                // add the found nodes to the temporary collection
                // by calling recursive browse on each. Every call to RecursiveBrowse
                // will add the children to the temporary list
                t_nodeRefs.AddRange(RecursiveBrowse(rdc));
            }

            // return temporary collection
            return t_nodeRefs;
        }

        /// <summary>This method returns a list of strings, representing DisplayNames.
        /// <para>Params:</para> 
        /// <para>@nodeRefs : ReferenceDescriptionCollection </para> 
        /// Can for example be used with BrowseServer() or BrowseNode() which both returns a ReferenceDescriptionCollection.
        /// </summary>
        public List<string> GetDisplayNames(ReferenceDescriptionCollection nodeRefs)
        {
            // Initializes a new list 
            List<string> names = new List<string>();

            // iterates the reference collection and adds the display name to the list
            nodeRefs.ForEach(nr => names.Add(nr.DisplayName.ToString()));

            // returns the list of display name strings
            return names;
        }

        /// <summary>This method returns a list of strings, representing NodeIds.
        /// <para>Params:</para> 
        /// <para>@nodeRefs : ReferenceDescriptionCollection </para> 
        /// </summary> 
        public List<string> GetNodeIds(ReferenceDescriptionCollection nodeRefs)
        {
            // Initializes a new list 
            List<string> names = new List<string>();

            // iterates the reference collection and adds the node id to the list
            nodeRefs.ForEach(nr => names.Add(nr.NodeId.ToString()));

            // returns the list of node id strings
            return names;
        }

        /// <summary>Default MonitoredItemNotificationEvent method. Automatically called by the stack.
        /// <para>Params:</para> 
        /// <para>@item : MonitoredItem</para> 
        /// <para>@e : MonitoredItemNotificationEventArgs</para> 
        ///  To use a method such as this it must be initialized in a MonitoredItemNotificationEventHandler(...)
        /// </summary> 
        private void DefaultCallbackMethod(MonitoredItem item, MonitoredItemNotificationEventArgs e)
        {

            // This simple default, only prints the new values to screen
            // The logic can be substituted with any other

            // Values are deqeued such that earlier changes can 
            // be caught
            foreach (var value in item.DequeueValues())
            {
                // Add to log
                Log.Log(LogLevel.MSG, item.DisplayName + " { Value:" + value.Value + ", SourceTimestamp:" + value.SourceTimestamp + ", StatusCode:" + value.StatusCode +" }");
            }
        }
          
        /// <summary>ReadNode reads the value of a node from a server based on the input node. 
        /// <para>Params:</para> 
        /// <para>@nodeRef : ReferenceDescription</para> 
        ///  Returns null in case of bad connection or empty collection if unable.
        /// </summary> 
        public DataValueCollection ReadNode(ReferenceDescription nodeRef)
        {
            // call read nodes, but initialize a collection
            return ReadNodes(new ReferenceDescriptionCollection() { nodeRef });
        }
         
        /// <summary>ReadNodes reads a set of values from server-nodes based on the input nodes. 
        /// <para>Params:</para> 
        /// <para>@nodeRefs : ReferenceDescriptionCollection</para> 
        ///  Returns null in case of bad connection or empty collection if unable.
        /// </summary> 
        public DataValueCollection ReadNodes(ReferenceDescriptionCollection nodeRefs)
        {
            // initialize a DataValueCollection and assign to null
            DataValueCollection dataValues = null;

            // If the client session is not operation successfully, return null
            if (ClientSession == null || !Operating || !ClientSession.Connected) return dataValues;

            // Create a collection to store the noderefs as read value ID's
            ReadValueIdCollection collection = new ReadValueIdCollection();

            foreach (ReferenceDescription nodeRef in nodeRefs)
            {
                // else, create a read value id
                ReadValueId valueId = new ReadValueId();

                // assign the node id from the input node, and set attribute ID to Value
                valueId.NodeId = (NodeId)nodeRef.NodeId;
                valueId.AttributeId = Attributes.Value;

                // Add the node to the collection  
                collection.Add(valueId);
            }

            // create an output for diagnostics
            DiagnosticInfoCollection diagnosticInfos = null;

            try
            { 
                // Execute the read command
                ResponseHeader header = ClientSession.Read(null, 0, TimestampsToReturn.Source, collection, out dataValues, out diagnosticInfos);
            }
            catch (ServiceResultException)
            {
                // If connection is bad, log.
                Log.Log(LogLevel.ERROR, "Unable to read nodes from endpoint: " + URL);
            }

            // regardless, return data values which might be empty
            return dataValues; 
        }

        /// <summary>
        /// WriteToNodes writes a value to the set of nodes. 
        /// <para>Params:</para>
        /// <para>@nodeRefs : ReferenceDescriptionCollection</para>
        /// <para>@value : object, any input value</para>
        /// </summary>
        /// <returns>a boolean: false if unsuccessful, true if successful</returns>
        public bool WriteToNodes(ReferenceDescriptionCollection nodeRefs, object value)
        {
            // create the write value collection, based on the input nodes
            WriteValueCollection nodes = MakeWriteValue(nodeRefs, value);

            // if client session is not operating successfully, return false
            if (nodes == null) return false;

            // else, return result of write
            return Write(nodes);
        }

        /// <summary>
        /// WriteToNodes writes a value to a single noge. 
        /// <para>Params:</para>
        /// <para>@nodeRef : ReferenceDescription</para>
        /// <para>@value : object, any input value</para>
        /// </summary>
        /// <returns>a boolean: false if unsuccessful, true if successful</returns>
        public bool WriteToNode(ReferenceDescription nodeRef, object value)
        {
            // create the write value collection, based on the input nodes
            WriteValueCollection nodes = MakeWriteValue(new ReferenceDescriptionCollection() { nodeRef }, value);

            // if client session is not operating successfully, return false
            if (nodes == null) return false;

            // else, return result of write 
            return Write(nodes);
        }

        /// <summary>
        /// MakeWriteValue create write value objects, based on a reference collection.
        /// <para>Params:</para>
        /// <para>@nodeRefs : ReferenceDescriptionCollection</para>
        /// <para>@value : object, any input value</para>
        /// </summary>
        /// <returns>WriteValueCollection, empty if bad service result or failing client session.</returns>
        private WriteValueCollection MakeWriteValue(ReferenceDescriptionCollection nodeRefs, object value)
        {
            // initializing the write value collection
            WriteValueCollection collection = new WriteValueCollection();
             
            // if client is operating poorly, return empty collection
            if (ClientSession == null || !Operating || !ClientSession.Connected) return collection;
            
            // iterate the node references, and create a write value
            foreach (ReferenceDescription nodeRef in nodeRefs)
            {
                // initialize the write value object
                WriteValue node = new WriteValue();
                
                // assign parameters
                node.NodeId = (NodeId)nodeRef.NodeId;
                node.AttributeId = Attributes.Value;
                node.IndexRange = null;

                // formulate the data value, and the metadata
                node.Value = new DataValue()
                {
                    Value = value,
                    SourceTimestamp = DateTime.Now,
                    StatusCode = StatusCodes.Good,
                    ServerTimestamp = DateTime.MinValue
                };

                // add the write value to the collection
                collection.Add(node);
            } 

            // iterate each write value in the collection
            // to verify the index range
            foreach (WriteValue wNode in collection)
            {
                // create a numeric range, the index range
                NumericRange indexRange;

                // valudate/generate the indexrange
                ServiceResult result = NumericRange.Validate(wNode.IndexRange, out indexRange);

                // verify that the operation was successful, e.g that the indexrange is not empty
                if (ServiceResult.IsGood(result) && indexRange != NumericRange.Empty)
                {
                    // assign the value to a new object
                    object nValue = wNode.Value.Value;

                    // apply the range to the service result
                    result = indexRange.ApplyRange(ref nValue);

                    // validate the result
                    if (ServiceResult.IsGood(result))
                    {
                        // if succssful, apply the value to the node
                        wNode.Value.Value = nValue;
                    }
                }
            }

            // return the collection
            return collection;
        }

        /// <summary>
        /// Write writes a value to a range of write values.
        /// <para>Params:</para>
        /// <para>@collection : WriteValueCollection</para>
        /// </summary>
        /// <returns>a boolean: false if failiure, true is success.</returns>
        private bool Write(WriteValueCollection collection)
        {
            // if the client is not operate successfully, return false
            if (ClientSession == null || !Operating || !ClientSession.Connected) return false;                

            // initialize output variables for session channel
            StatusCodeCollection results;
            DiagnosticInfoCollection diagnosticInfos;

            try
            {
                // write the value and output the response
                ResponseHeader responseHeader = ClientSession.Write(null, collection, out results, out diagnosticInfos);
            } catch (ServiceResultException ex)
            {
                // if the connection was bad, log and return false
                Log.Log(LogLevel.ERROR, "Unable to write to nodes on server " + URL + " because of " + ex.Message);
                return false;
            } 

            // foreach response, validate if successfull
            foreach (StatusCode status in results)

                // if any was bad, return false
                if (ServiceResult.IsBad(status))
                    return false;

            // else, return true
            return true; 
        }

       
        /// <summary>Default KeepAliveEvent method. Automatically called by the stack.
        /// <para>Params:</para> 
        /// <para>@session : Session</para> 
        /// <para>@e : KeepAliveEventArgs</para> 
        ///  To use a method such as this it must be initialized in a KeepAliveEventHandler(...).
        /// </summary> 
        private void DefaultKeepAliveCallbackMethod(Session session, KeepAliveEventArgs e)
        { 
            // The service result validates the connection
            if (ServiceResult.IsBad(e.Status))
            {
                try 
                {
                    // Add to log
                    Log.Log(LogLevel.ERROR, "Connection on client with endpoint: " + URL + " is bad, trying to reconnect.");

                    // First, try to reconnect to the server.
                    // This does not work, if the server has been restarted.
                    session.Reconnect();
                }  
                // If the server does not support client memory
                // then Reconnect results in ServiceResultException
                catch (ServiceResultException) 
                {

                    // Make sure that the connection is clean
                    Disconnect();
                    if (!ResubscribeReferences())
                    { 
                        // if it is impossible to reesthablish a connection
                        // disconnect for good
                        Disconnect();

                        // Add to log
                        Log.Log(LogLevel.ERROR, "Unable to re-establish a connection to endpoint: " + URL + " client no longer in operation.");
                    }
                }
            }
        }

        /// <summary>ResubscribeReferences is used by Reconnect and the default KeepAliveEventHandler. The function resubscribes all nodes contained in References, if the nodes are intact on the server.
        /// </summary> 
        private bool ResubscribeReferences()
        {
            // Try to reconnect to the server
            // Then perform a sanity check to establish subscription
            if (Connect() && SanityCheck()) 
            {
                // If the connection and the subscription session were
                // successfull, iterate the server to verify that
                // previous references still exist: nodesIntact
                List<string> nodesIntact = GetDisplayNames(BrowseObjectsNode());

                // Iterate the stored references to verify that the nodes still
                // exist on the server before subscribing
                foreach (KeyValuePair<ReferenceDescription, MonitoredItemNotificationEventHandler> item in References.ToList())
                {
                    // if the node still exists server side, recreate the subscription
                    if (nodesIntact.Contains(item.Key.DisplayName.ToString()))
                    {
                        // call subscribe with known callback
                        Subscribe(item.Key, item.Value);

                        // Add to log
                        Log.Log(LogLevel.INFO, "Resubscribing to " + item.Key.DisplayName + " on client with endpoint: " + URL);
                    }
                    else 
                    {
                        // if the nodes no longer exists server side
                        // remove said node from References
                        References.Remove(item.Key);

                        // Add to log
                        Log.Log(LogLevel.WARN, item.Key.DisplayName + " no longer exists on endpoint: " + URL + " removing from References." );
                    }
                }

                // if the server item is set
                // resubscribe 
                if (MonitorServerStatusItem != null)
                    Monitor();

                // return true for successful resubscription
                return true;
            }
             
            // return false if the client is not operating successfully
            return false;
        }


        /** Helper Methods */

        /// <summary>The SanityCheck is used by all methods that builds on a subscriptions. Tt validates whether there is a successfull client subscribtion running. If not, it creates one.
        /// <para>Params:</para> 
        /// <para>@publishingInterval : int</para> 
        /// the publishing interval determines the interval bestween publish, and counts for all subscriptions.
        /// </summary> 
        private bool SanityCheck()   
        {
            // if the ClientSession has not yet been created, create one
            // However, if unable, return false.
            if (ClientSession == null) if (!Connect())
                {
                    // Add to log
                    Log.Log(LogLevel.INFO, "Sanity-check failed on client with endpoint: " + URL);

                    // return false
                    return false;
                }

            // If the subscription session has not yet been created
            // but the client is operating successfully, then create one.
            if (SubscriptionSession == null && Operating && ClientSession.Connected)
            {
                // Create by using the default Session subscription and set the publishing interval
                SubscriptionSession = new Subscription(ClientSession.DefaultSubscription) { PublishingInterval = CurrentPublishingInterval };

                // Add the subscription to the client session
                ClientSession.AddSubscription(SubscriptionSession);

                // create is called to complete the subscription
                // and allows the client to contact the server
                SubscriptionSession.Create();

                // Add to log
                Log.Log(LogLevel.INFO, "Sanity-check successfull on client with endpoint: " + URL);

                // return true for a successfull sanity check
                return true;
            }

            // If the subscription session is already created
            // and the client is operating successfully, return true for a successfull sanity check
            else if (SubscriptionSession != null && Operating && ClientSession.Connected)
            { 
                // Add to log
                Log.Log(LogLevel.INFO, "Sanity-check successfull on client with endpoint: " + URL);
                return true;
            } 
             
            // Add to log
            Log.Log(LogLevel.INFO, "Sanity-check failed on client with endpoint: " + URL);

            // if the client is not operating successfully,
            // a client session could not be establoshed, return false
            return false; 
        }

        /// <summary>ApplyChanges is called by any method the updates the subscription session
        /// </summary> 
        private void ApplyChanges()
        {
            try
            {
                // ApplyChanges is surrounded by a try/catch
                // in case the client is not operating successfully
                // or communication is bad
                SubscriptionSession.ApplyChanges();
            }

            // If the changes are not applies, write the exception to log.
            catch (Exception)
            {  // Add to log
                Log.Log(LogLevel.ERROR, "Unable to update subscription session on client with endpoint: " + URL);
            }
        }

        /// <summary>GetCallBack is a helper method, that is used by all methods that performs operations with the subscriptions in some way. The method returns the correct callback to be used.
        /// <para>Params:</para> 
        /// <para>@callback : MonitoredItemNotificationEventHandler</para> 
        /// This means that the callback to be used only have to be assigned once.
        /// </summary>
        private MonitoredItemNotificationEventHandler GetCallback(MonitoredItemNotificationEventHandler callback)
        {
            // if the call back is null, the default callback is assigned to callback
            if (callback == null) callback = DefaultNotificationCallback;
            // else, callback is assigned to the default callback
            else DefaultNotificationCallback = callback;

            // return the default callback
            return callback;
        }

        /// <summary>UpdateReferences is used to update the References.
        /// <para>Params:</para> 
        /// <para>@nodeRef : ReferenceDescription</para> 
        /// <para>@callback : MonitoredItemNotificationEventHandler</para> 
        /// The node reference associated to a particular callback is inserted into References.
        /// </summary>
        private void UpdateReferences(ReferenceDescription nodeRef, MonitoredItemNotificationEventHandler callback)
        {
            // if References does not already containt the current node,
            // insert the node into references
            if (SanityCheck())
            { 
                ReferenceDescription tempNodeRef = GetReference(nodeRef.DisplayName.ToString(), nodeRef.NodeId.ToString());
                  
                if (tempNodeRef != null)
                { 
                    // Stop the current subscription, since it is being added again
                    // this will also remove the node from References
                    StopSubscription(tempNodeRef);
                } 
                  
                // and add the new callback
                if (!References.Keys.ToList().Contains(nodeRef)) References.Add(nodeRef, callback);
            }  
        }   
         
        /// <summary>GetReference returns ReferenceDescription or null.
        /// <para>Params:</para> 
        /// <para>@displayName : string</para> 
        /// <para>@nodeId : string</para> 
        /// </summary>
        public ReferenceDescription GetReference(string displayName, string nodeId)
        {
            // for each key/value pair in References
            foreach(KeyValuePair<ReferenceDescription, MonitoredItemNotificationEventHandler> rf in References.ToList())
            {
                // if current reference has the same displayname and id
                if (rf.Key.DisplayName.ToString() == displayName && rf.Key.NodeId.ToString() == nodeId)

                    // return reference description
                    return rf.Key;
            }
            
            // not found
            return null;
        }
   
        /// <summary>CreateMonitoredItem is a used to create a monitored item from a reference desccription.
        /// <para>Params:</para> 
        /// <para>@nodeRef : ReferenceDescription</para> 
        /// <para>@callback : MonitoredItemNotificationEventHandler</para> 
        ///  The callback is added to the item. 
        /// </summary>
        public MonitoredItem CreateMonitoredItem(ReferenceDescription nodeRef, MonitoredItemNotificationEventHandler callback = null)
        {
            // this method must always return a useful item
            // thus, if the client is not operating successfully
            // throw an argument null exception on the "connection"
            if (!SanityCheck())
            {
                // Add to log
                Log.Log(LogLevel.FATAL, "Unable to create MonitoredItem on client with endpoint: " + URL + " due to bad connection.");
                return null;
            }

            // Create the new item, using the display name and the node id
            MonitoredItem item = new MonitoredItem(SubscriptionSession.DefaultItem) { DisplayName = nodeRef.DisplayName.Text, StartNodeId = (NodeId)nodeRef.NodeId };

            // if the callback is null, then no call back is added to the item
            if (callback != null) item.Notification += callback; 

            // return the monitored item
            return item;
        }

        /// <summary>ClearReferences is a top level helper method. It substitutes the need for calling, client.GetReferences().Clear().
        /// </summary>
        public void ClearReferences()
        {
            // Empties the References list
            References.Clear();

            // Add to log 
            Log.Log(LogLevel.INFO, "Cleared references on client with endpoint: " + URL);
        }

        /// <summary>GetReferences is a top level helper method. It returns the current References.
        /// </summary>
        public Dictionary<ReferenceDescription, MonitoredItemNotificationEventHandler> GetReferences()
        {
            // return dictionary
            return References;
        }
         
        /// <summary>GetMonitoredItems, returns monitored items empty if client not operating successfully.
        /// </summary>
        public List<MonitoredItem> GetMonitoredItems()
        {  
            if (SanityCheck()) return SubscriptionSession.MonitoredItems.ToList();
            return new List<MonitoredItem>(); 
        }
    }    
} 
  