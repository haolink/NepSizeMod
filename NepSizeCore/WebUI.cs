using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace NepSizeCore
{
    /// <summary>
    /// Allows the UI to log onto the Unity console.
    /// </summary>
    public class LogEvent : EventArgs
    {
        /// <summary>
        /// Log text.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message"></param>
        public LogEvent(string message) : base()
        {
            this.Message = message;            
        }
    }

    /// <summary>
    /// A message via websocket has been received.
    /// </summary>
    public class WebSocketMessageEvent : EventArgs
    {
        /// <summary>
        /// Log text.
        /// </summary>
        public string Message { get; }

        public WebUIJsonHandler Handler { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message"></param>
        public WebSocketMessageEvent(string message, WebUIJsonHandler handler) : base()
        {
            this.Message = message;
            this.Handler = handler;
        }
    }

    /// <summary>
    /// Send/Reply handler for the websocket.
    /// </summary>
    public class WebUIJsonHandler : WebSocketBehavior
    {
        protected override void OnMessage(MessageEventArgs e)
        {
            string inputJson = e.Data;

            if (_delegate != null)
            {
                _delegate(inputJson, this);
            }
        }

        private Action<string, WebUIJsonHandler> _delegate = null;

        public Action<string, WebUIJsonHandler> Delegate 
        { 
            get { return _delegate; }
            set { _delegate = value; }
        }

        public void SendReply(SizeServerResponse reply, string uuid)
        {
            reply.UUID = uuid;
            string outputData = JsonSerializer.Serialize(reply);

            this.Send(outputData);
        }
    }

    /// <summary>
    /// Manager for a web ui for a size plugin.
    /// </summary>
    public class WebUI
    {        
        /// <summary>
        /// Server component.
        /// </summary>
        HttpServer _server;

        /// <summary>
        /// Logger.
        /// </summary>
        public event EventHandler<LogEvent> Log;

        /// <summary>
        /// Event fired when the JS fires a message to the WS.
        /// </summary>
        public event EventHandler<WebSocketMessageEvent> MessageReceived;

        /// <summary>
        /// Root namespace.
        /// </summary>
        private string _rootNamespace;

        /// <summary>
        /// Character list of the current game.
        /// </summary>
        private CharacterList _characterList;

        /// <summary>
        /// List of special UI files.
        /// </summary>
        private Dictionary<string, Func<string>> _specialPaths;

        /// <summary>
        /// Resource name cache.
        /// </summary>
        private string[] _resourceCache;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="characterList"></param>
        /// <param name="ipString"></param>
        /// <param name="port"></param>
        public WebUI(CharacterList characterList, string ipString = null, int port = 7979)
        {
            if (ipString == null)
            {
                _server = new HttpServer(address: IPAddress.Any, port: port);
            }
            else
            {
                _server = new HttpServer(address: IPAddress.Parse(ipString), port: port);
            }

            this._rootNamespace = typeof(WebUI).Namespace.Split(new char[] { '.' })[0];
            this._characterList = characterList;

            this._specialPaths = new Dictionary<string, Func<string>>()
            {
                [this._rootNamespace + ".webresources.virtual.characters.js"] = new Func<string>(() => this.GenerateVirtualCharacterList())
            };

            List<string> validResources = new List<string>();
            foreach(string resourceName in Assembly.GetExecutingAssembly().GetManifestResourceNames())
            {
                if (resourceName.StartsWith(this._rootNamespace + "."))
                {
                    validResources.Add(resourceName);
                }
            }
            this._resourceCache = validResources.ToArray();

            _server.OnGet += ServerOnGet;
            _server.AddWebSocketService<WebUIJsonHandler>("/socket", (s) => s.Delegate = (s, e) =>
            {
                this.ReceivedMessageFromSocket(s, e);
            });
        }

        /// <summary>
        /// When a message via websocket has been received.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="handler"></param>
        internal void ReceivedMessageFromSocket(string message, WebUIJsonHandler handler)
        {
            if (this.MessageReceived != null)
            {
                this.MessageReceived(this, new WebSocketMessageEvent(message, handler));
            }
        }

        /// <summary>
        /// Start the server.
        /// </summary>
        public void Start()
        {
            _server.Start();
        }

        /// <summary>
        /// Log a message.
        /// </summary>
        /// <param name="message">Message text.</param>
        private void DebugLog(string message)
        {
            if (this.Log != null)
            {
                this.Log(this, new LogEvent(message));
            }
        }

        /// <summary>
        /// Currently supported Content types, might be more, probably sufficient for this use though.
        /// </summary>
        private static readonly Dictionary<string, string> _contentTypes = new()
        {
            [".html"] = "text/html; charset=utf-8",
            [".htm"] = "text/html; charset=utf-8",
            [".txt"] = "text/plain; charset=utf-8",
            [".css"] = "text/css; charset=utf-8",
            [".js"] = "application/javascript; charset=utf-8",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".gif"] = "image/gif",
            [".svg"] = "image/svg+xml",
            [".woff"] = "font/woff",
            [".woff2"] = "font/woff2",
            [".webp"] = "image/webp"
        };

        /// <summary>
        /// Generate fake headers for the web view.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private static string GetContentType(string uri)
        {
            string extension = Path.GetExtension(uri).ToLowerInvariant();
            return _contentTypes.TryGetValue(extension, out var type)
                ? type
                : "application/octet-stream";
        }

        /// <summary>
        /// Sanitise a URI.
        /// </summary>
        /// <param name="path">URI originally</param>
        /// <returns>Sanitised URI.</returns>
        private static string SanitisePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "/";
            }

            string[] parts = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            Stack<string> stack = new Stack<string>();

            foreach (var part in parts)
            {
                if (part == ".")
                {
                    // Ignore current directory references
                    continue;
                }
                else if (part == "..")
                {
                    // Pop only if not empty to prevent going above root
                    if (stack.Count > 0)
                        stack.Pop();
                }
                else
                {
                    stack.Push(part);
                }
            }

            // Rebuild the sanitized path
            var sanitized = "/" + string.Join("/", stack.Reverse());

            return sanitized;
        }

        /// <summary>
        /// Cached character JSON.
        /// </summary>
        private string _characterJson = null;

        private string GenerateVirtualCharacterList()
        {
            if (_characterJson == null)
            {
                JsonSerializerOptions options = new JsonSerializerOptions();
                options.WriteIndented = true;
                string json = JsonSerializer.Serialize(this._characterList, options);

                _characterJson = "const characterData = " + json + ";";
            }

            return _characterJson;
        }

        /// <summary>
        /// Check whether a resource exists and prepare the cache if needed.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private bool IsResourceValid(string path)
        {
            DebugLog("Checking for " + path);
            if (_specialPaths.ContainsKey(path))
            {
                return true;
            }

            if (_resourceCache.Contains(path))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Queries the content of a web server response.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private byte[] QueryResourceContent(string path)
        {
            if (_specialPaths.ContainsKey(path))
            {
                string data = _specialPaths[path]();
                return Encoding.UTF8.GetBytes(data);
            }

            if (_resourceCache.Contains(path))
            {
                Stream dataStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
                dataStream.Seek(0, SeekOrigin.Begin);
                byte[] result = dataStream.ReadBytes((int)dataStream.Length); // A Resource will NEVER be larger than 2 GB.
                return result;
            }

            return null;
        }

        /// <summary>
        /// Generate an internal protocol which forwards to the webresources.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private string MapUriToResourceName(string uri)
        {
            uri = SanitisePath(uri);

            if (uri.EndsWith("/"))
            {
                uri += "index.html";
            }

            uri = uri.Replace("/", ".");            

            

            string resourceUrl = $"{_rootNamespace}.webresources{uri}";

            if (IsResourceValid(resourceUrl))
            {
                return resourceUrl;
            }            
            else
            {
                resourceUrl += ".index.html";
                if (IsResourceValid(resourceUrl))
                {
                    return resourceUrl;
                }
            }
            return null;
        }

        /// <summary>
        /// Handle a GET request on a resource.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ServerOnGet(object sender, HttpRequestEventArgs e)
        {
            string resourceName = MapUriToResourceName(e.Request.Url.LocalPath);
            byte[] data = null;

            if (resourceName != null)
            {
                data = this.QueryResourceContent(resourceName);
            }

            if (data == null) 
            { 
                data = Encoding.UTF8.GetBytes("Not found");
                e.Response.ContentLength64 = data.Length;
                e.Response.ContentType = "text/plain; charset=utf-8";
                e.Response.OutputStream.Write(data, 0, data.Length);
                e.Response.StatusCode = 404;
                return;
            }

            string contentType = GetContentType(resourceName);

            e.Response.StatusCode = 200;
            e.Response.ContentLength64 = data.Length;
            e.Response.ContentType = contentType;
            e.Response.OutputStream.Write(data, 0, data.Length);
        }
    }
}
