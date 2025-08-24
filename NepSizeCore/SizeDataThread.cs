using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using Deli.Newtonsoft.Json;
using Deli.Newtonsoft.Json.Linq;
using System.Drawing;

namespace NepSizeCore
{
    /// <summary>
    /// Main pipe server.
    /// Receives messages via Named pipes, hands them to its command list which is dynamically gathered via 
    /// refleciton.
    /// </summary>
    public sealed class SizeDataThread
    {
        internal interface IOutputWriter 
        {
            /// <summary>
            /// Sets an output UUID.
            /// </summary>
            /// <param name="uuid"></param>
            public void SetUUID(string uuid);

            /// <summary>
            /// Writes a JSON string into the output stream system.
            /// </summary>
            /// <param name="outputData">Json info to write.</param>
            public void SendReply(SizeServerResponse outputData);

            /// <summary>
            /// Close the connection if required.
            /// </summary>
            public void CloseSystem();
        }

        internal class WebsocketOutputWrite : IOutputWriter
        {
            /// <summary>
            /// Websocket to write into.
            /// </summary>
            private WebUIJsonHandler _wsHandler;

            /// <summary>
            /// Message UUID.
            /// </summary>
            private string _uuid = null;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="pipe"></param>
            public WebsocketOutputWrite(WebUIJsonHandler wsHandler)
            {
                _wsHandler = wsHandler;
            }

            /// <summary>
            /// Sets a message UUID.
            /// </summary>
            /// <param name="UUID">UUID.</param>
            public void SetUUID(string UUID)
            {
                _uuid = UUID;
            }

            /// <summary>
            /// Send a reply.
            /// </summary>
            /// <param name="outputData"></param>
            public void SendReply(SizeServerResponse outputData)
            {
                _wsHandler.SendReply(outputData, _uuid);
            }

            /// <summary>
            /// Close the websocket (not needed WebsocketSharp does this).
            /// </summary>
            public void CloseSystem()
            {
                // Empty
            }
        }

        /// <summary>
        /// Internal information class of a client connection.
        /// </summary>
        internal class ConnectionData
        {
            /// <summary>
            /// What command is the client running.
            /// </summary>
            public Func<SizeServerResponse> Action { get; private set; }
            /// <summary>
            /// What is the client output.
            /// </summary>
            public IOutputWriter Output { get; private set; }

            /// <summary>
            /// Is the connection closed?
            /// </summary>
            private bool _closed;

            /// <summary>
            /// Initialise connection object.
            /// </summary>
            /// <param name="action">Method to call in the Unity main thread.</param>
            /// <param name="pipe">Pipe to write the answer into.</param>
            public ConnectionData(Func<SizeServerResponse> action, IOutputWriter output)
            {
                _closed = false;
                Action = action;
                Output = output;
            }

            /// <summary>
            /// Must be run on the Unity main thread. Invokes the actual action.
            /// </summary>
            public void RunAction()
            {
                SizeServerResponse returnValue = Action.Invoke();

                // No reply, just close.
                if (returnValue == null)
                {
                    Close();
                }
                else
                {
                    // Parse the reply, send it in a new non-blocking thread.
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        try
                        {
                            Output.SendReply(returnValue);
                        }
                        catch (Exception ex)
                        {
                            // Send output to debug log.                            
                            SizeDataThread.Instance.DebugLogThreadSafe($"Error sending reply: {ex.Message}");
                        }
                        finally
                        {
                            try
                            {
                                // Close the connection.
                                Close();
                            }
                            catch (Exception ex)
                            {
                                SizeDataThread.Instance.DebugLogThreadSafe($"Error closing connection: {ex.Message}");
                            }
                        }
                    });                    
                }
            }

            /// <summary>
            /// Close the connection.
            /// </summary>
            public void Close()
            {
                if (!_closed)
                {
                    if (Output != null)
                    {
                        Output.CloseSystem();
                    }                    
                    _closed = true;
                }
            }
        }

        /// <summary>
        /// Self reference.
        /// </summary>
        public static SizeDataThread Instance { get; private set; }

        /// <summary>
        /// Web socket thread.
        /// </summary>
        private Thread _socketThread;

        /// <summary>
        /// Will be set in case the thread should be cancelled.
        /// </summary>
        private volatile bool _pipeCancellation = false;

        /// <summary>
        /// Thread queue.
        /// </summary>
        volatile private ThreadSafeQueue<ConnectionData> _mainThreadActiveConnections;

        /// <summary>
        /// Push notifications for Web Socket clients.
        /// </summary>
        volatile private ThreadSafeQueue<SizeServerResponse> _pushNotifications;

        /// <summary>
        /// Supported server commands.
        /// </summary>
        private ServerCommands _serverCommands;

        /// <summary>
        /// Map cache of commands.
        /// </summary>
        private Dictionary<string, MethodInfo> _commandMap = new();

        /// <summary>
        /// Web UI management class.
        /// </summary>
        private WebUI _webUI;

        /// <summary>
        /// Main game plugin.
        /// </summary>
        private INepSizeGamePlugin _mainPlugin;

        /// <summary>
        /// Size memory Storage.
        /// </summary>
        private SizeMemoryStorage _sizeMemoryStorage;

        /// <summary>
        /// Settings storage. Generic object.
        /// </summary>
        private Object _settingsObject;

        /// <summary>
        /// Initialises the main pipe server thread.
        /// </summary>
        /// <param name="serverCommands"></param>
        /// 
        public SizeDataThread(INepSizeGamePlugin mainPlugin, SizeMemoryStorage sizeMemoryStorage, Object settingsObject = null)
        {
            if (Instance != null)
            {
                throw new InvalidOperationException("SizeDataThread already initialised!");
            }
            Instance = this;

            _serverCommands = new ServerCommands(CoreConfig.GAMENAME, mainPlugin, this);
            _mainPlugin = mainPlugin;
            _sizeMemoryStorage = sizeMemoryStorage;

            this._settingsObject = settingsObject;

            _sizeMemoryStorage.ActiveCharactersChanged += MemoryReportsNewCharacterList;

            RegisterAllCommands();

            _pipeCancellation = false;
            _mainThreadActiveConnections = new ThreadSafeQueue<ConnectionData>();

            _pushNotifications = new ThreadSafeQueue<SizeServerResponse>();

            _socketThread = new Thread(() =>
            {
                InitialiseWebUI();
            })
            {
                IsBackground = true,
                Priority = System.Threading.ThreadPriority.Lowest
            };
            _socketThread.Start();

            _serverCommands.Log("Thread started");
        }

        /// <summary>
        /// When new characters are detected.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MemoryReportsNewCharacterList(object sender, ActiveCharactersChangedEvent e)
        {
            this.PushNewCharacters(e.ActiveCharacters);
        }

        /// <summary>
        /// Sends a push notification to all Web Socket clients with the new Character list.
        /// </summary>
        /// <param name="activeCharacters"></param>
        private void PushNewCharacters(IList<uint> activeCharacters)
        {
            JToken je = JToken.FromObject(activeCharacters);

            SizeServerResponse push = SizeServerResponse.CreatePushNotification("ActiveCharacterChange", "Active characters have changed", je);

            _pushNotifications.Enqueue(push);
        }
        
        /// <summary>
        /// Check commands in the ServerCommands object via Reflection.
        /// </summary>
        private void RegisterAllCommands()
        {
            foreach (var method in _serverCommands.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                // Only register methods that return SizeServerResponse.
                if (method.ReturnType != typeof(SizeServerResponse)) {
                    continue;
                }

                // Skip getters/setters and other compiler-generated methods.
                if (method.IsSpecialName)
                {
                    continue;
                }

                if (_commandMap.ContainsKey(method.Name))
                {
                    _serverCommands.Log($"Only added the first instance of overloaded method {method.Name} - method overloading is not allowed.");
                    continue;
                }
                
                _commandMap[method.Name] = method;
            }
        }

        /// <summary>
        /// Close the thread in case the game closes.
        /// </summary>
        public void CloseThread()
        {
            while (this._mainThreadActiveConnections.TryDequeue(out ConnectionData action))
            {
                action.Close();
            }
            _pipeCancellation = true;
            while (_mainThreadActiveConnections.TryDequeue(out _)) { }

            SizeMemoryStorage.DisposeInstance();

            // Wait for background thread to terminate gracefully, but don't block game shutdown indefinitely
            if (!_socketThread.Join(1000)) // Wait for up to 1 second
            {
                DebugLogThreadSafe("Background thread didn't terminate gracefully within timeout");
            }
        }

        /// <summary>
        /// Web UI shall be initialised.
        /// </summary>
        private void InitialiseWebUI()
        {
            DebugLogThreadSafe($"Starting web server - on port {CoreConfig.SERVER_PORT}, IP {CoreConfig.SERVER_IP}");

            try
            {
                _webUI = new WebUI(this._mainPlugin.GetCharacterList(), ipString: CoreConfig.SERVER_IP, port: CoreConfig.SERVER_PORT, filterLocalSubnetOnly: CoreConfig.SERVER_LOCAL_SUBNET_ONLY,
                settingsObject: this._settingsObject);
                _webUI.Log += WebUIDebugLog;
                _webUI.MessageReceived += WebUIMessageReceived;

                _webUI.Start();
            }
            catch (Exception ex)
            {
                DebugLogThreadSafe(ex.Message);
            }


            while (!_pipeCancellation)
            {
                while (_pushNotifications.TryDequeue(out SizeServerResponse notification))
                {
                    _webUI.SendPushNotification(notification);
                }
                Thread.Sleep(10); // Reduce CPU usage while remaining responsive
            }
        }

        /// <summary>
        /// Updates a settings object using a JsonElement object.
        /// </summary>
        /// <param name="settings"></param>
        public void UpdateSettingsObject(JToken settings)
        {
            if (_settingsObject == null)
            {
                return;
            }

            foreach(JProperty property in settings.Children<JProperty>())
            {
                string name = property.Name;
                JToken value = property.Value;

                PropertyInfo propertyInfo = _settingsObject.GetType().GetProperty(name);
                if (propertyInfo == null) { continue; }

                if (propertyInfo.PropertyType == typeof(float))
                {
                    if (value.Type == JTokenType.Float || value.Type == JTokenType.Integer)
                    {
                        propertyInfo.SetValue(_settingsObject, value.ToObject<float>(), null);
                    }
                }
                else if(propertyInfo.PropertyType == typeof(bool))
                {
                    if (value.Type == JTokenType.Boolean)
                    {
                        propertyInfo.SetValue(_settingsObject, value.ToObject<bool>(), null);
                    }
                }
            }
        }

        /// <summary>
        /// Allow the Web UI to log to the output.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WebUIDebugLog(object sender, LogEvent e)
        {
            DebugLogThreadSafe(e.Message);
        }        

        /// <summary>
        /// Send an error on the pipe reply.
        /// </summary>
        /// <param name="msg">Message text</param>
        /// <param name="output">Output stream.</param>
        private void CreateErrorReply(string msg, IOutputWriter output)
        {
            this._mainThreadActiveConnections.Enqueue(new ConnectionData(() =>
            {
                return SizeServerResponse.ReturnError(msg, null);
            }, output));
        }

        /// <summary>
        /// Send an exception on the pipe reply.
        /// </summary>
        /// <param name="msg">Message text</param>
        /// <param name="output">Output stream.</param>
        private void CreateExceptionReply(string msg, IOutputWriter output)
        {
            this._mainThreadActiveConnections.Enqueue(new ConnectionData(() =>
            {
                return SizeServerResponse.ReturnError(msg, null);
            }, output));
        }

        /// <summary>
        /// We got a message via the websocket.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WebUIMessageReceived(object sender, WebSocketMessageEvent e)
        {
            HandleMessage(new WebsocketOutputWrite(e.Handler), e.Message);
        }

        /// <summary>
        /// Message unpacked successfully to a string. Deserialise as JSON.
        /// </summary>
        /// <param name="output">Reply stream.</param>
        /// <param name="message">Message string.</param>
        /// <returns></returns>
        private bool HandleMessage(IOutputWriter output, string message)
        {
            DebugLogThreadSafe("Received: " + message);

            try
            {
                GenericCommand cmd = JsonConvert.DeserializeObject<GenericCommand>(message);

                if (cmd == null || string.IsNullOrEmpty(cmd.command))
                {
                    this.CreateExceptionReply("Invalid command structure", output);
                    return false;
                }

                // Validate command name format (alphanumeric only)
                if (!System.Text.RegularExpressions.Regex.IsMatch(cmd.command, @"^[a-zA-Z0-9]+$"))
                {
                    this.CreateExceptionReply("Invalid command format", output);
                    return false;
                }

                if (!string.IsNullOrEmpty(cmd.UUID))
                {
                    output.SetUUID(cmd.UUID);
                }

                return this.TryInvoke(cmd.command, cmd.data, output);
            }
            catch (JsonException)
            {
                this.CreateExceptionReply("Invalid JSON format", output);
                return false;
            }
            catch (Exception ex)
            {
                DebugLogThreadSafe($"Unexpected error in HandleMessage: {ex.Message}");
                this.CreateExceptionReply("Internal error", output);
                return false;
            }
        }        

        /// <summary>
        /// Json unpacked - check the desired command - parse the parameters. And create an Action invocation object.
        /// </summary>
        /// <param name="commandName">COmmand to run.</param>
        /// <param name="data">Parameters</param>
        /// <param name="output">Output stream</param>
        /// <returns>Can it correctly be executed?</returns>
        private bool TryInvoke(string commandName, JToken? data, IOutputWriter output)
        {
#pragma warning disable 8632
            // Get action
            if (!_commandMap.TryGetValue(commandName, out MethodInfo method))
            {
                this.CreateExceptionReply($"Unknown command: {commandName}", output);
                return false;
            }

            // Determine parameters and assign them
            var parameters = method.GetParameters();
            var args = new object?[parameters.Length];

            if (parameters.Length == 0)
            {
                //data parameter not necessary
                this._mainThreadActiveConnections.Enqueue(new ConnectionData(() =>
                {
                    return (SizeServerResponse?)method.Invoke(_serverCommands, null);
                }, output));
                return true;
            }

            if (data == null || data.Type != JTokenType.Object)
            {
                this.CreateExceptionReply("Invalid data format", output);
                return false;
            }

            JObject dataObj = data as JObject;

            // Assign data JSON to method parameters.
            foreach (var param in parameters)
            {
                if (!dataObj.TryGetValue(param.Name, out JToken value))
                {
                    // Use default value if possible (.NET 3.5 compatible check)
                    if (param.DefaultValue != DBNull.Value)
                    {
                        args[param.Position] = param.DefaultValue;
                    }
                    else
                    {
                        this.CreateExceptionReply("Missing required parameter", output);
                        return false;
                    }
                }
                else
                {
                    try
                    {
                        object parsed = value.ToObject(param.ParameterType);
                        args[param.Position] = parsed;
                    }
                    catch (Exception ex)
                    {
                        // Log detailed error for debugging but send generic message to client
                        DebugLogThreadSafe($"Parameter conversion failed for {param.Name}: {ex.Message}");
                        this.CreateExceptionReply("Invalid parameter value", output);
                        return false;
                    }
                }            
            }

            // Create an action.
            try
            {
                this._mainThreadActiveConnections.Enqueue(new ConnectionData(() =>
                {
                    return (SizeServerResponse?)method.Invoke(_serverCommands, args);
                }, output));
                return true;
            }
            catch (Exception ex)
            {
                // Log detailed error for debugging but send generic message to client
                DebugLogThreadSafe($"Failed to enqueue command '{commandName}': {ex.Message}");
                this.CreateExceptionReply("Internal error", output);
                return false;
            }
            #pragma warning restore 8632
        }

        /// <summary>
        /// To be called from the Unity thread - invokes actions on the queue.
        /// </summary>
        public void HandleConnectionQueue()
        {
            while (this._mainThreadActiveConnections.TryDequeue(out ConnectionData connection))
            {
                if (connection != null)
                {
                    connection.RunAction();
                }
            }
        }

        /// <summary>
        /// Log a message in a thread safe manor.
        /// </summary>
        /// <param name="msg"></param>
        private void DebugLogThreadSafe(string msg)
        {
            _mainThreadActiveConnections.Enqueue(new ConnectionData(() =>
            {
                this._serverCommands.Log(msg);
                return null;
            }, null));
        }

        /// <summary>
        /// Emergency destructor.
        /// </summary>
        ~SizeDataThread()
        {
            if (!_pipeCancellation)
            {
                this.CloseThread();                
            }
        }

        #region Internal classes
#pragma warning disable 0649
        /// <summary>
        /// Internal generic command data.
        /// </summary>
        [Serializable]
        internal class GenericCommand
        {
            public string UUID { get; set; }
            public string command { get; set; }
            public JToken? data { get; set; }
        }    

        #pragma warning restore 0649
        #endregion
    }
}