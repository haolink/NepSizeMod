using System;
using System.Collections.Generic;
using System.Text;

namespace NepSizeCore
{
    /// <summary>
    /// Core configuration for the plugins. An embedding game plugin should absolutely initialise all these values BEFORE 
    /// </summary>
    public class CoreConfig
    {
        /// <summary>
        /// Game name - currently not really used as the Web UI doesn't need it. Might return if we need something like Named Pipes again.
        /// </summary>
        public static string GAMENAME = "DFLT";

        /// <summary>
        /// This is used in the &lt;title&gt; tag of the web UI.
        /// </summary>
        public static string WEBUI_TITLE = "Default game";

        /// <summary>
        /// Listen on all IPv4 by default - notice that it would limit to the current subnet by default.
        /// </summary>
        public static string SERVER_IP = "0.0.0.0";

        /// <summary>
        /// Set some default port for the web UI.
        /// </summary>
        public static int SERVER_PORT = 8888;

        /// <summary>
        /// By default we only accept connections from the current network - discourage the user from changing this to false.
        /// </summary>
        public static bool SERVER_LOCAL_SUBNET_ONLY = true;
    }
}
