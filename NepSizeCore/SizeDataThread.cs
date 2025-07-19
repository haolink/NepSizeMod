using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Text;
using System.Reflection;

namespace NepSizeCore
{
    /// <summary>
    /// Main pipe server.
    /// Receives messages via Named pipes, hands them to its command list which is dynamically gathered via 
    /// refleciton.
    /// </summary>
    public class SizeDataThread
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

        internal class PipeOutputWriter : IOutputWriter
        {
            /// <summary>
            /// Pipe to write
            /// </summary>
            private NamedPipeServerStream _pipe;

            /// <summary>
            /// Message UUID.
            /// </summary>
            private string _uuid = null;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="pipe"></param>
            public PipeOutputWriter(NamedPipeServerStream pipe)
            {
                _pipe = pipe;
            }

            /// <summary>
            /// Send a reply.
            /// </summary>
            /// <param name="outputData"></param>
            public void SendReply(SizeServerResponse outputJson)
            {
                string outputData = JsonSerializer.Serialize(outputJson);

                byte[] utfResponse = UTF8Encoding.UTF8.GetBytes(outputData);
                byte[] replyLength = BitConverter.GetBytes(utfResponse.Length);

                this._pipe.Write(replyLength, 0, 4);
                this._pipe.Write(utfResponse, 0, utfResponse.Length);
                this._pipe.Flush();
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
            /// Close the pipe.
            /// </summary>
            public void CloseSystem()
            {
                if (this._pipe != null)
                {
                    this._pipe.Dispose();
                }
                this._pipe = null;
            }
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
                    Thread t = new Thread(() =>
                    {                                                
                        Output.SendReply(returnValue);

                        this.Close();
                    });
                    t.Start();
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
        /// Main thread.
        /// </summary>
        private Thread _pipeThread;

        /// <summary>
        /// Will be set in case the thread should be cancelled.
        /// </summary>
        private CancellationTokenSource _pipeCancellation;

        /// <summary>
        /// Currently incoming pipe.
        /// </summary>
        private NamedPipeServerStream _currentPipe;

        /// <summary>
        /// Thread queue.
        /// </summary>
        private ConcurrentQueue<ConnectionData> _mainThreadActiveConnections;

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
        /// Initialises the main pipe server thread.
        /// </summary>
        /// <param name="serverCommands"></param>
        public SizeDataThread(INepSizeGamePlugin mainPlugin, ServerCommands serverCommands)
        {
            _serverCommands = serverCommands;
            _mainPlugin = mainPlugin;

            RegisterAllCommands();

            _pipeCancellation = new CancellationTokenSource();
            _mainThreadActiveConnections = new ConcurrentQueue<ConnectionData>();

            _pipeThread = new Thread(() => {
                InitialiseWebUI();
                RunServer(_pipeCancellation.Token);
            })
            {
                IsBackground = true,
                Priority = System.Threading.ThreadPriority.Lowest
            };
            _pipeThread.Start();            

            _serverCommands.Log("Thread started");
        }
        
        /// <summary>
        /// Check commands in the ServerCommands object via Reflection.
        /// </summary>
        private void RegisterAllCommands()
        {
            foreach (var method in _serverCommands.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.ReturnType != typeof(SizeServerResponse)) {
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
                /*if (action.Pipe.IsConnected)
                {
                    action.Pipe.Dispose();
                }*/
                action.Close();
            }
            while (_mainThreadActiveConnections.TryDequeue(out _)) { }
            _pipeCancellation.Cancel();

            _currentPipe?.Dispose();

            _pipeThread.Join();
        }

        /// <summary>
        /// Web UI shall be initialised.
        /// </summary>
        private void InitialiseWebUI()
        {
            _webUI = new WebUI(this._mainPlugin.GetCharacterList());
            _webUI.Log += WebUIDebugLog;
            _webUI.MessageReceived += WebUIMessageReceived;
            _webUI.Start();
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
        /// Start the pipe server.
        /// </summary>
        /// <param name="token">Which token handles the connection - close the server if this token is cancelled.</param>
        private void RunServer(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                _currentPipe = new NamedPipeServerStream("NepSizeCommandLet", PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                ManualResetEvent waitHandle = new ManualResetEvent(false);

                _currentPipe.BeginWaitForConnection((IAsyncResult ar) =>
                {
                    //DebugLogThreadSafe("Incoming!");
                    try
                    {
                        _currentPipe.EndWaitForConnection(ar);
                        ThreadPool.QueueUserWorkItem(UnpackMessage, _currentPipe);
                    }
#pragma warning disable 0168
                    catch (Exception ex)
#pragma warning restore 0168
                    {

                    }
                    finally
                    {
                        waitHandle.Set();
                    }
                }, _currentPipe);

                waitHandle.WaitOne();

                _currentPipe = null;
            }
        }

        /// <summary>
        /// Data is incoming via the pipe.
        /// </summary>
        /// <param name="state">Pipe stream</param>
        private void UnpackMessage(object state)
        {
            NamedPipeServerStream pipe = (NamedPipeServerStream)state;

            try
            {
                // Read length
                byte[] lengthBuffer = new byte[4];
                pipe.Read(lengthBuffer, 0, 4);
                int msgLength = BitConverter.ToInt32(lengthBuffer, 0);

                // Read content
                byte[] payloadBuffer = new byte[msgLength];
                int totalRead = 0;
                while (totalRead < msgLength)
                {
                    int read = pipe.Read(payloadBuffer, totalRead, msgLength - totalRead);
                    if (read == 0)
                    {
                        DebugLogThreadSafe("Client closed the pipe prematurely.");
                        pipe.Dispose();
                        return;
                    }
                    totalRead += read;
                }

                string message = Encoding.UTF8.GetString(payloadBuffer);
                HandleMessage(new PipeOutputWriter(pipe), message);
            }
            catch (IOException ex)
            {
                DebugLogThreadSafe("Error reading/writing: " + ex.Message);
            }        
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
                GenericCommand cmd = null;
                try
                {
                    JsonSerializerOptions jso = new JsonSerializerOptions();
                    jso.DefaultBufferSize = 1024;
                    string tst = JsonSerializer.Serialize(new { test = "hello" });
                    cmd = JsonSerializer.Deserialize<GenericCommand>(message);
                }
                catch (Exception ex)
                {
                    DebugLogThreadSafe("Exception: " + ex.Message + " " + ex.GetType().FullName);
                }

                if (cmd == null || string.IsNullOrEmpty(cmd.command))
                {
                    this.CreateExceptionReply($"Unknown data structure!", output);
                    return false;
                }

                if (!string.IsNullOrEmpty(cmd.UUID))
                {
                    output.SetUUID(cmd.UUID);
                }

                return this.TryInvoke(cmd.command, cmd.data, output);
            }
            catch (Exception ex)
            {
                this.CreateExceptionReply($"JSON parsing error {ex.Message}", output);
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
        private bool TryInvoke(string commandName, JsonElement? data, IOutputWriter output)
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

            if (data == null || data.Value.ValueKind != JsonValueKind.Object)
            {
                this.CreateExceptionReply($"Missing or invalid 'data' field for command '{commandName}'.", output);
                return false;
            }

            // Assign data JSON to method parameters.
            foreach (var param in parameters)
            {
                if (!data.Value.TryGetProperty(param.Name, out var value))
                {
                    // Use default value if possible.
                    if (!param.HasDefaultValue)
                    {
                        this.CreateExceptionReply($"Missing parameter: {param.Name}", output);
                        return false;
                    }
                    else
                    {
                        args[param.Position] = param.DefaultValue;
                    }
                }
                else
                {
                    try
                    {
                        object parsed = JsonSerializer.Deserialize(value.GetRawText(), param.ParameterType);
                        args[param.Position] = parsed;
                    }
                    catch (Exception ex)
                    {
                        this.CreateExceptionReply($"Parameter parse error for '{param.Name}': {ex.Message}", output);
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
                this.CreateExceptionReply($"Exception in '{commandName}': {ex.Message}", output);
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
            public JsonElement? data { get; set; }
        }    

        #pragma warning restore 0649
        #endregion
    }
}