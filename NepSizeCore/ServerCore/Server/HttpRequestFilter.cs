/**
 * Additional filter class for firewall purposes.
 */

using System;
using System.Collections.Generic;
using System.Text;
using WebSocketSharp.Net;

namespace WebSocketSharp.Server
{
    public class HttpRequestFilter : HttpRequestEventArgs
    {
        internal HttpRequestFilter(HttpListenerContext context, string documentRootPath) : base(context, documentRootPath)
        {
        }

        private bool _cancel = false;

        public bool Cancel { get { return _cancel; } set { _cancel = value; } }
    }
}
