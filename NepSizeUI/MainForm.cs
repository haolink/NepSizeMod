using Microsoft.Web.WebView2.Core;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Reflection;

namespace NepSizeUI
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// Pipe client and memory handler.
        /// </summary>
        private ControlThread _controlThread;

        /// <summary>
        /// Webview interop component.
        /// </summary>
        private WebViewInterop _webViewInterop;

        public MainForm()
        {
            InitializeComponent();

            this.Icon = Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);

            // Initialise the webview
            _webViewInterop = new WebViewInterop(this.webView);
            _webViewInterop.RegisterCommand("updateScales", this.UpdateScales);
            _webViewInterop.RegisterCommand("setReady", this.SetReady);
            _webViewInterop.RegisterCommand("persistScales", this.PersistScales);
            _webViewInterop.RegisterCommand("clearPersistance", this.ClearPersistence);
            _webViewInterop.WebviewInitialized += WebviewReady;
            _webViewInterop.InitializeWebView();
        }

        /// <summary>
        /// Web view is read.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WebviewReady(object? sender, EventArgs e)
        {
            // Open the index.html.
            this.webView.Source = new Uri("intresource://res/index.html");
        }

        /// <summary>
        /// When JS runs UpdateScales.
        /// </summary>
        /// <param name="scales"></param>
        private void UpdateScales(Dictionary<uint, float> scales)
        {
            _controlThread.CharacterScales = scales.ToImmutableDictionary();
        }

        /// <summary>
        /// Method executes when DOMContentLoaded has fired.
        /// </summary>
        private void SetReady()
        {
#if DEBUG
            this._webViewInterop.SendCommand("EnableDebug");
#endif

            // WebView must be ready before initialising this, it might crossfire otherwise
            _controlThread = new ControlThread();
            _controlThread.Connected += GameConnected;
            _controlThread.Disconnected += GameDisconnected;
            _controlThread.ActiveCharactersChanged += CharactersChanged;

            _controlThread.Start();
        }

        /// <summary>
        /// We connected to a game - inform JS.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void GameConnected(object sender, ConnectEventArgs e)
        {
            this._webViewInterop.SendCommand("GameConnected", new
            {
                game = e.Game,
                currentScales = e.Scales
            });
        }

        /// <summary>
        /// Lost connection to the game. Inform JS.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void GameDisconnected(object sender, EventArgs e)
        {
            this._webViewInterop.SendCommand("GameDisconnected");
        }

        /// <summary>
        /// We have a new character list - forward to JS.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void CharactersChanged(object sender, ActiveCharactersChanged e)
        {
            this._webViewInterop.SendCommand("UpdatePlayerList", new
            {
                characterIds = e.ActiveCharacters
            });
        }

        /// <summary>
        /// On close - close all threads.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            _controlThread?.Close();
        }

        /// <summary>
        /// Persist scales.
        /// </summary>
        private void PersistScales()
        {
            bool success = this._controlThread.PersistScales();
            MessageBox.Show((success) ? "Successfully saved" : "An error occured", "Status", MessageBoxButtons.OK, (success) ? MessageBoxIcon.None : MessageBoxIcon.Error);
        }

        /// <summary>
        /// Clear persistence.
        /// </summary>
        private void ClearPersistence()
        {
            bool success = this._controlThread.ClearPersistedScales();
            MessageBox.Show((success) ? "Successfully cleared" : "An error occured", "Status", MessageBoxButtons.OK, (success) ? MessageBoxIcon.None : MessageBoxIcon.Error);
        }
    }
}
