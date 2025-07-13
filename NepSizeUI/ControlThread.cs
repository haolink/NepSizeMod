using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Eventing.Reader;
using System.Text;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace NepSizeUI
{
    /// <summary>
    /// Helper event handler.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public delegate void GenericEventHandler<T>(object sender, T e) where T : EventArgs;

    /// <summary>
    /// Event when the game is connected.
    /// </summary>
    public class ConnectEventArgs : EventArgs
    {
        /// <summary>
        /// PID of the game.
        /// </summary>
        public int Pid { get; private set; }

        /// <summary>
        /// Game code.
        /// </summary>
        public string Game { get; private set; }

        /// <summary>
        /// What are the initial character scales.
        /// </summary>
        public ImmutableDictionary<uint, float> Scales { get; private set; }

        /// <summary>
        /// Constructor for this event.
        /// </summary>
        /// <param name="pid"></param>
        /// <param name="game"></param>
        /// <param name="scales"></param>
        public ConnectEventArgs(int pid, string game, IDictionary<uint, float> scales)
        {
            this.Pid = pid;
            this.Game = game;
            this.Scales = scales.ToImmutableDictionary<uint, float>();
        }
    }

    /// <summary>
    /// Event when the list of character is changed.
    /// </summary>
    public class ActiveCharactersChanged : EventArgs
    {
        /// <summary>
        /// Character ID list.
        /// </summary>
        public ImmutableList<uint> ActiveCharacters { get; private set; }

        /// <summary>
        /// Creator for the character change event.
        /// </summary>
        /// <param name="activeCharacters"></param>
        public ActiveCharactersChanged(ImmutableList<uint> activeCharacters)
        {
            this.ActiveCharacters = activeCharacters;
        }
    }

    /// <summary>
    /// Thread to handle exchange.
    /// </summary>
    public class ControlThread : IDisposable
    {
        /// <summary>
        /// Main thread.
        /// </summary>
        private Thread _thread;

        /// <summary>
        /// If the app is closing - prepare closure here.
        /// </summary>
        private bool _closing;

        /// <summary>
        /// Context for synchronisation.
        /// </summary>
        private SynchronizationContext? _synchronizationContext;

        /// <summary>
        /// Game has been connected.
        /// </summary>
        public event GenericEventHandler<ConnectEventArgs>? Connected;
        /// <summary>
        /// Game has been disconnected.
        /// </summary>
        public event GenericEventHandler<EventArgs>? Disconnected;
        /// <summary>
        /// List of characters has changed.
        /// </summary>
        public event GenericEventHandler<ActiveCharactersChanged>? ActiveCharactersChanged;

        /// <summary>
        /// Memory editor.
        /// </summary>
        private MemEditor64 _memEditor;

        /// <summary>
        /// Are we connected to the game memory?
        /// </summary>
        private bool _isMemoryConnected;

        /// <summary>
        /// Do we have access to everything?
        /// </summary>
        private volatile bool _isFullyConnected;

        /// <summary>
        /// Do we have access to everything?
        /// </summary>
        public bool IsConnected { get { return _isFullyConnected; } }

        /// <summary>
        /// Address for the character lists.
        /// </summary>
        private long _characterListAddress; //Field only for the thread, thus not volatile

        /// <summary>
        /// Address for the character scales.
        /// </summary>
        private long _characterScaleAddress; //Field only for the thread, thus not volatile

        /// <summary>
        /// Current character scales.
        /// </summary>
        private ImmutableDictionary<uint, float>? _characterScales;

        /// <summary>
        /// When the scales update - set this to non -zero.
        /// </summary>
        private uint _scalesUpdated = 0;

        /// <summary>
        /// Game title.
        /// </summary>
        private volatile string _game = null;

        /// <summary>
        /// Title of the game.
        /// </summary>
        public string Game { get { return _game; } }

        /// <summary>
        /// Updatable character scales.
        /// </summary>
        public ImmutableDictionary<uint, float>? CharacterScales { 
            get 
            {
                return Interlocked.CompareExchange<ImmutableDictionary<uint, float>>(ref _characterScales, null, null);
            } 
            set 
            {
                Interlocked.Exchange<ImmutableDictionary<uint, float>>(ref _characterScales, value ); 
                Interlocked.Exchange(ref _scalesUpdated, (uint)1); 
            }
        }

        /// <summary>
        /// List of active characters.
        /// </summary>
        private ImmutableList<uint>? _activeCharacters;

        /// <summary>
        /// List of active characters (readonly).
        /// </summary>
        public ImmutableList<uint>? ActiveCharacters
        {
            get
            {
                return Interlocked.CompareExchange(ref _activeCharacters, null, null);
            }
        }

        /// <summary>
        /// Creator.
        /// </summary>
        public ControlThread()
        {
            _isMemoryConnected = false;
            _isFullyConnected = false;
            _characterScales = null;
            _scalesUpdated = 0;
            _activeCharacters = null;

            _closing = false;
            _synchronizationContext = SynchronizationContext.Current;
            _thread = new Thread(ThreadLoop);
            _memEditor = new MemEditor64(["Neptunia Game Maker REvolution.exe", "neptunia-sisters-vs-sisters.exe", "NeptuniaRidersVSDogoos.exe"]);
        }

        /// <summary>
        /// Start the main thread.
        /// </summary>
        public void Start()
        {
            _thread.Start();
        }

        /// <summary>
        /// Allows executing an event on the main thread of the calling context.
        /// </summary>
        /// <typeparam name="T">Type of the event handler</typeparam>
        /// <param name="handler">Event handler</param>
        /// <param name="payload">Data to transmit.</param>
        private void TriggerEvent<T>(GenericEventHandler<T> handler, T payload) where T : EventArgs
        {
            if (handler != null)
            {
                //ConnectEventArgs e = new ConnectEventArgs(pid);
                if (_synchronizationContext == null || _synchronizationContext == SynchronizationContext.Current)
                {
                    handler.Invoke(this, payload);
                }
                else
                {
                    _synchronizationContext.Post(_ => handler.Invoke(this, payload), null);
                }
            }
        }

        /// <summary>
        /// Main thread loop.
        /// </summary>
        private void ThreadLoop()
        {
            while (!_closing) //When we close - abort.
            {
                try
                {
                    // Try to connect to the memory
                    bool memoryConnected = _memEditor.IsConnected();
                    if (!memoryConnected)
                    {
                        memoryConnected = _memEditor.Connect();

                        if (!memoryConnected && _isMemoryConnected) //Lost connection
                        {
                            this.TriggerEvent<EventArgs>(this.Disconnected!, EventArgs.Empty);
                        }
                    }
                    _isMemoryConnected = memoryConnected;
                    if (!_isMemoryConnected)
                    {
                        _isFullyConnected = false; //We also lost connection in general.
                    }

                    if (_isMemoryConnected && !_isFullyConnected)
                    {
                        // Welp - is the mod pipe ready:
                        if (this.QueryMemoryAddresses())
                        {
                            this._isFullyConnected = true;
                            IDictionary<uint, float> scales = this.ReadInitialScales();
                            this.TriggerEvent<ConnectEventArgs>(this.Connected!, new ConnectEventArgs(this._memEditor.ProcessId, this._game, scales));
                        }
                    }

                    if (_isFullyConnected)
                    {
                        HandleMemory();
                    }
                }
                catch (Exception ex)
                {
                    // Catch all
                }
                

                Thread.Sleep(50);
            }

            // Disconnect.
            if (_isMemoryConnected)
            {
                _isFullyConnected = false;
                _isMemoryConnected = false;
                _characterScales = null;
                _scalesUpdated = 0;
                _activeCharacters = null;
                this._memEditor.Disconnect();
            }
        }

        private const int MSG_TYPE_SUCCESS = 0;
        private const int MSG_TYPE_ERROR = 1;
        private const int MSG_TYPE_EXCEPTION = 2;

        /// <summary>
        /// Replies of the pipe server.
        /// </summary>
        [Serializable]
        internal class ApplicationReply
        {
            public required int Type { get; set; }
            public required string Message { get; set; }
            public required JsonElement Data { get; set; }
        }

        /// <summary>
        /// Address and title scheme.
        /// </summary>
        [Serializable]
        internal class MemoryAddressReply
        {
            [JsonPropertyName("scaleAddress")]
            public required long ScaleAddress { get; set; }
            [JsonPropertyName("charList")]
            public required long CharList { get; set; }
            [JsonPropertyName("game")]
            public required string Game { get; set; }
        }

        /// <summary>
        /// Helper to send commands to a pipe server.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        private ApplicationReply? SendPipeServerMessageInternal(string command, object data = null)
        {
            Func<NamedPipeClientStream, int, byte[], bool> pipeRead = (NamedPipeClientStream pipe, int length, byte[] memory) =>
            {
                int totalRead = 0;

                while (totalRead < length)
                {
                    int read = pipe.Read(memory, totalRead, length - totalRead);
                    if (read == 0)
                    {
                        return false;
                    }
                    totalRead += read;
                }

                if (totalRead == length)
                {
                    return true;
                }

                return false;
            };

            using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", "NepSizeCommandLet", PipeDirection.InOut))
            {
                try
                {
                    pipeClient.Connect(100); // Timeout to make sure no thread lock will occur
                }
                catch (TimeoutException tex) // Mod DLL inside the game probably hasn't launched yet - no biggie - we cancel.
                {
                    return null;
                }

                // JSON-Payload
                if (data == null)
                {
                    data = new { };
                }
                string json = JsonSerializer.Serialize(new
                {
                    command = command,
                    data = data
                });

                // Convert to bytes
                byte[] payload = Encoding.UTF8.GetBytes(json);

                // Get message length
                byte[] lengthPrefix = BitConverter.GetBytes(payload.Length);

                pipeClient.Write(lengthPrefix, 0, 4);
                pipeClient.Write(payload, 0, payload.Length);
                pipeClient.Flush();

                // Reading server response
                byte[] respLengthBytes = new byte[4];
                if (!pipeRead(pipeClient, 4, respLengthBytes))
                {
                    return null;
                }
                int respLen = BitConverter.ToInt32(respLengthBytes, 0);

                byte[] respBuffer = new byte[respLen];
                if (!pipeRead(pipeClient, respLen, respBuffer))
                {
                    return null;
                }
                string response = Encoding.UTF8.GetString(respBuffer);

                try
                {
                    ApplicationReply? reply = JsonSerializer.Deserialize<ApplicationReply>(response, JsonSerializerOptions.Default);                    

                    return reply;
                }
                catch (Exception ex)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Query the memory addresses of character lists and scales.
        /// </summary>
        /// <returns>True if successful.</returns>
        private bool QueryMemoryAddresses()
        {
            ApplicationReply? reply = this.SendPipeServerMessageInternal("GetGameSettings");

            if (reply != null && reply.Type == MSG_TYPE_SUCCESS && reply.Data.ValueKind == JsonValueKind.Object)
            {
                MemoryAddressReply? mar = reply.Data.Deserialize<MemoryAddressReply>();
                if (mar != null)
                {
                    this._characterListAddress = mar.CharList;
                    this._characterScaleAddress = mar.ScaleAddress;
                    this._game = mar.Game;

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Should the current scale list be persisted (survives a game restart).
        /// </summary>
        /// <returns>Succesful reply.</returns>
        public bool PersistScales()
        {
            if (!this._isFullyConnected)
            {
                return false;
            }

            ApplicationReply? reply = this.SendPipeServerMessageInternal("UpdatePersistence", new { clear = false });

            return (reply != null && reply.Type == MSG_TYPE_SUCCESS);
        }

        /// <summary>
        /// Should the currently persisted scale list be cleared.
        /// </summary>
        /// <returns>Succesful reply.</returns>
        public bool ClearPersistedScales()
        {
            if (!this._isFullyConnected)
            {
                return false;
            }

            ApplicationReply? reply = this.SendPipeServerMessageInternal("UpdatePersistence", new { clear = true });

            return (reply != null && reply.Type == MSG_TYPE_SUCCESS);
        }

        /// <summary>
        /// Maximum amount of scalable characters.
        /// </summary>
        const int MAX_CHAR_WRITE = 511;

        /// <summary>
        /// Read the scales of characters.
        /// </summary>
        /// <returns>Dictionary with character id -> scale assignment.</returns>
        private IDictionary<uint, float> ReadInitialScales()
        {
            byte[] mem = _memEditor.ReadMemory(this._characterScaleAddress, 4088);
            Dictionary<uint, float> scales = new Dictionary<uint, float>();

            MemoryStream ms = new MemoryStream(mem);
            BinaryReader br = new BinaryReader(ms);
            ms.Seek(0, SeekOrigin.Begin);
            int i = 0;
            uint cid = 0;
            float f = 0.0f;
            while ((cid = br.ReadUInt32()) != 0 && cid <= MAX_CHAR_WRITE)
            {
                f = br.ReadSingle();
                if (f < 0.0f || f == float.NaN || f >= float.MaxValue || f >= float.PositiveInfinity)
                {
                    f = 1.0f;
                }
                scales[cid] = f;
                i++;
            }
            this.CharacterScales = scales.ToImmutableDictionary();
            return scales;
        }

        /// <summary>
        /// Method to write memory.
        /// </summary>
        private void HandleMemory()
        {
            // Have the scales been updated?
            if (Interlocked.Exchange(ref _scalesUpdated, 0) == 1)
            {
                ImmutableDictionary<uint, float> characterScales = this.CharacterScales ?? ImmutableDictionary<uint, float>.Empty;

                int dl = Math.Min(MAX_CHAR_WRITE, characterScales.Count) * 8 + 4;
                MemoryStream ms = new MemoryStream(dl);
                int i = 0;
                foreach (KeyValuePair<uint, float> kvp in characterScales)
                {
                    ms.Write(BitConverter.GetBytes(kvp.Key), 0, 4);
                    ms.Write(BitConverter.GetBytes(kvp.Value), 0, 4);
                    i++;
                    if (i == MAX_CHAR_WRITE)
                    {
                        break;
                    }
                }
                ms.Write(BitConverter.GetBytes(((uint)0)), 0, 4);
                byte[] upload = ms.ToArray();
                ms.Close();

                _memEditor.WriteMemory(this._characterScaleAddress, upload);
            }

            // Read the characters.
            byte[] characterListMemory = _memEditor.ReadMemory(this._characterListAddress, 1024);
            List<uint> characters = new List<uint>();

            using (MemoryStream ms = new MemoryStream(characterListMemory)) 
            using (BinaryReader cr = new BinaryReader(ms))
            {
                cr.BaseStream.Position = 0;
                uint cid;
                while ((cid = cr.ReadUInt32()) != 0)
                {
                    characters.Add(cid);
                }
                characters = characters.Distinct().ToList();
                characters.Sort();

                if (characters.Count == 0 && _activeCharacters == null)
                {
                    _activeCharacters = characters.ToImmutableList();
                }
                else if (_activeCharacters == null)
                {
                    _activeCharacters = ImmutableList<uint>.Empty;
                }

                // Compare with the old list.
                if (!Enumerable.SequenceEqual(characters, _activeCharacters))
                {
                    _activeCharacters = characters.ToImmutableList();
                    // If not equal - trigger a character change event.
                    TriggerEvent<ActiveCharactersChanged>(this.ActiveCharactersChanged!, new NepSizeUI.ActiveCharactersChanged(
                        _activeCharacters
                    ));
                }
            }                
        }

        /// <summary>
        /// Close the thread.
        /// </summary>
        public void Close()
        {
            if (_closing)
            {
                return;
            }
            _closing = true;
            _thread.Join();
        }

        /// <summary>
        /// Dispose this.
        /// </summary>
        public void Dispose()
        {
            Close();
            GC.SuppressFinalize(this);
        }        
    }
}
