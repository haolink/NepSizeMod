using System;
using Deli.Newtonsoft.Json.Linq;

namespace NepSizeCore
{
    /// <summary>
    /// Generalised response object
    /// </summary>
    [Serializable]
    public class SizeServerResponse
    {
        /// <summary>
        /// Response types
        /// </summary>
        const int MSG_TYPE_SUCCESS = 0;
        const int MSG_TYPE_ERROR = 1;
        const int MSG_TYPE_EXCEPTION = 2;
        const int MSG_TYPE_PUSH = 3;

        /// <summary>
        /// What type is the response.
        /// </summary>
        public int Type { get; private set; }
        
        /// <summary>
        /// Context of a push notification.
        /// </summary>
        public string Context { get; private set; }

        /// <summary>
        /// Text of the message.
        /// </summary>
        public string? Message { get; private set; }
        
        /// <summary>
        /// Transmitted data in the message.
        /// </summary>
        public JToken? Data { get; set; }

        /// <summary>
        /// UUID (optional)
        /// </summary>
        public string? UUID { get; set; }

        /// <summary>
        /// Main constructur of a reply.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="message"></param>
        /// <param name="data"></param>
        private SizeServerResponse(int type, string message, JToken? data)
        {
            this.Type = type;
            this.Message = message;
            this.Data = data;
        }

        /// <summary>
        /// Static constructor for a success message.
        /// </summary>
        /// <param name="message">Message text.</param>
        /// <param name="data">JSON Data</param>
        /// <returns>Response object</returns>
        public static SizeServerResponse ReturnSuccess(string message, JToken? data = null)
        {
            return new SizeServerResponse(MSG_TYPE_SUCCESS, message, data);
        }

        /// <summary>
        /// Static constructor for an error message.
        /// </summary>
        /// <param name="message">Message text.</param>
        /// <param name="data">JSON Data</param>
        /// <returns>Response object</returns>
        public static SizeServerResponse ReturnError(string message, JToken? data = null)
        {
            return new SizeServerResponse(MSG_TYPE_ERROR, message, data);
        }

        /// <summary>
        /// Static constructor for an exception message.
        /// </summary>
        /// <param name="message">Message text.</param>
        /// <param name="data">JSON Data</param>
        /// <returns>Response object</returns>
        public static SizeServerResponse ReturnException(string message, JToken? data = null)
        {
            return new SizeServerResponse(MSG_TYPE_EXCEPTION, message, data);
        }

        /// <summary>
        /// Erzeugt eine Push-Notification.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="message"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static SizeServerResponse CreatePushNotification(string context, string message, JToken? data = null)
        {
            SizeServerResponse ssr = new SizeServerResponse(MSG_TYPE_PUSH, message, data);
            ssr.Context = context;
            return ssr;
        }
    }
}