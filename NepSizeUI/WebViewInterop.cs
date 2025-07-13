using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace NepSizeUI
{
    /// <summary>
    /// Interop between the Edge based web UI.
    /// </summary>
    public class WebViewInterop
    {
        /// <summary>
        /// The WebView we connect to.
        /// </summary>
        private WebView2 _webView;

        /// <summary>
        /// Commands JavaScript can execute.
        /// </summary>
        private Dictionary<string, Delegate> _commands;

        /// <summary>
        /// Init event.
        /// </summary>
        public event EventHandler WebviewInitialized = null;

        /// <summary>
        /// Is the app initialised.
        /// </summary>
        private bool _started;

        /// <summary>
        /// Hook Interop.
        /// </summary>
        /// <param name="webview"></param>
        public WebViewInterop(WebView2 webview)
        {
            _webView = webview;
            _commands = new Dictionary<string, Delegate>();
            _started = false;
        }

        /// <summary>
        /// Currently supported Content types, might be more, probably sufficient for this use though.
        /// </summary>
        private static readonly Dictionary<string, string> _contentTypes = new()
        {
            [".html"] = "text/html",
            [".css"] = "text/css",
            [".js"] = "application/javascript",
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
        /// Prepare Webview for interopping requests and communication
        /// </summary>
        public async void InitializeWebView()
        {
            if (_started)
            {
                return;
            }

            _started = true;

            List<CoreWebView2CustomSchemeRegistration> schemes = new List<CoreWebView2CustomSchemeRegistration>();

            schemes.Add(new CoreWebView2CustomSchemeRegistration("intresource")
            {
                TreatAsSecure = true,
                HasAuthorityComponent = false,
            });

            CoreWebView2EnvironmentOptions options = new CoreWebView2EnvironmentOptions(
                additionalBrowserArguments: null,
                language: CultureInfo.CurrentCulture.Name,
                targetCompatibleBrowserVersion: null,
                allowSingleSignOnUsingOSPrimaryAccount: false,
                customSchemeRegistrations: schemes                
            );

            CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(
                userDataFolder: null,
                options: options                
            );
            
            await _webView.EnsureCoreWebView2Async(environment);
#if DEBUG
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = true;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
#else
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
#endif

            _webView.CoreWebView2.WebMessageReceived += MessageReceived;
            _webView.CoreWebView2.WebResourceRequested += CoreWebView_WebResourceRequested;
            _webView.CoreWebView2.AddWebResourceRequestedFilter(@"intresource:*", CoreWebView2WebResourceContext.All);
            _webView.CoreWebView2.ContextMenuRequested += ContextMenuRequested;   
            
            if (WebviewInitialized != null)
            {
                WebviewInitialized(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// No context menu please.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
        {
            e.Handled = true;
        }

        /// <summary>
        /// Cache for reflection parameters.
        /// </summary>
        private Dictionary<string, ParameterInfo[]> _paramCache = new Dictionary<string, ParameterInfo[]>();

        /// <summary>
        /// Register a JS callback.
        /// </summary>
        /// <param name="command">Command name.</param>
        /// <param name="callback">Method to call.</param>
        public void RegisterCommand(string command, Delegate callback)
        {
            this._paramCache.Remove(command);
            this._commands.Add(command, callback);
        }

        /// <summary>
        /// Send a command to JS.
        /// </summary>
        /// <param name="command">COmmand name.</param>
        /// <param name="payload">Data to submit.</param>
        public void SendCommand(string command, object? payload = null)
        {
            object? baseData = (payload == null) ?
                new { command = command } :
                new { command = command, payload = payload }
            ;
            JsonElement json = JsonSerializer.SerializeToElement(baseData);


            this._webView.CoreWebView2.PostWebMessageAsJson(
                json.ToString()
            );
        }

        [Serializable]
        internal class JsonInteropCommand
        {
            [JsonPropertyName("command")]
            public required string Command { get; set; }
            [JsonPropertyName("payload")]
            public JsonElement? Payload { get; set; }
        }        

        /// <summary>
        /// JSON message interface will be interpreted here, will be sent as an event to the outside.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string msg = e.WebMessageAsJson;

            try
            {
                JsonInteropCommand? c = JsonSerializer.Deserialize<JsonInteropCommand>(msg);
                if (c != null)
                {
                    InvokeCommand(c.Command, c.Payload);
                }
            } 
            catch(Exception ex)
            {

            }            
        }

        /// <summary>
        /// Invoke the Delegate from JS using reflection.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="callParameters"></param>
        /// <returns></returns>
        public bool InvokeCommand(string command, JsonElement? callParameters)
        {
            if (!this._commands.ContainsKey(command)) 
            {
                // Command not known
                return false;
            }

            Delegate action = this._commands[command];

            ParameterInfo[] methodParameters;
            if (_paramCache.ContainsKey(command))
            {
                methodParameters = _paramCache[command];
            }
            else
            {
                methodParameters = action.Method.GetParameters();
                _paramCache[command] = methodParameters;
            }             

            if (methodParameters.Length <= 0) 
            {
                action.Method.Invoke(action.Target, null);
                return true;
            }

            object?[] args = new object?[methodParameters.Length];

            foreach (ParameterInfo parameter in methodParameters)
            {
                if (!callParameters!.Value.TryGetProperty(parameter.Name!, out var value))
                {
                    if (!parameter.HasDefaultValue)
                    {
                        return false;
                    }
                    else
                    {
                        args[parameter.Position] = parameter.DefaultValue;
                    }
                } 
                else
                {
                    try
                    {
                        object? parsed = JsonSerializer.Deserialize(value.GetRawText(), parameter.ParameterType);
                        args[parameter.Position] = parsed;
                    }
                    catch (Exception ex)
                    {
                        return false;
                    }
                }
            }

            try
            {
                action.Method.Invoke(action.Target, args);
            }
            catch (TargetInvocationException ex)
            {
                // ex.InnerException enthält die eigentliche Exception der aufgerufenen Methode
                return false;
            }

            return true;
        }

        /// <summary>
        /// Generate an internal protocol which forwards to the webresources.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private string MapUriToResourceName(string uri)
        {
            string path = uri.Replace("intresource://res/", "")
                             .Replace("/", ".");
            return $"{Assembly.GetExecutingAssembly().GetName().Name}.webresources.{path}";
        }

        /// <summary>
        /// Generate fake headers for the web view.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private string GetContentType(string uri)
        {
            string extension = Path.GetExtension(uri).ToLowerInvariant();
            return _contentTypes.TryGetValue(extension, out var type)
                ? type
                : "application/octet-stream";
        }        

        /// <summary>
        /// When the webview requests data.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CoreWebView_WebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            if (sender == null)
            {
                return;
            }

            CoreWebView2 webView = (CoreWebView2)sender;
            var uri = e.Request.Uri;
            if (uri.StartsWith("intresource://"))
            {
                var resourceName = MapUriToResourceName(uri);
                var assembly = Assembly.GetExecutingAssembly();
                var stream = assembly.GetManifestResourceStream(resourceName);

                if (stream != null)
                {
                    string contentType = GetContentType(uri); // z. B. text/html, image/jpeg etc.

                    e.Response = webView.Environment.CreateWebResourceResponse(
                        Content: stream,
                        StatusCode: 200,
                        ReasonPhrase: "OK",
                        Headers: $"Content-Type: {contentType}"
                    );
                }
                else
                {
                    e.Response = webView.Environment.CreateWebResourceResponse(
                        Stream.Null,
                        404,
                        "Not Found",
                        "Content-Type: text/plain"
                    );
                }
            }
        }
    }
}
