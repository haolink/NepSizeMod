using System;
using System.Text.Json;

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

        /// <summary>
        /// What type is the response.
        /// </summary>
        public int Type { get; private set; }
        
        /// <summary>
        /// Text of the message.
        /// </summary>
        public string Message { get; private set; }
        
        /// <summary>
        /// Transmitted data in the message.
        /// </summary>
        public JsonElement? Data { get; set; }

        /// <summary>
        /// Main constructur of a reply.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="message"></param>
        /// <param name="data"></param>
        private SizeServerResponse(int type, string message, JsonElement? data)
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
        public static SizeServerResponse ReturnSuccess(string message, JsonElement? data = null)
        {
            return new SizeServerResponse(MSG_TYPE_SUCCESS, message, data);
        }

        /// <summary>
        /// Static constructor for an error message.
        /// </summary>
        /// <param name="message">Message text.</param>
        /// <param name="data">JSON Data</param>
        /// <returns>Response object</returns>
        public static SizeServerResponse ReturnError(string message, JsonElement? data = null)
        {
            return new SizeServerResponse(MSG_TYPE_ERROR, message, data);
        }

        /// <summary>
        /// Static constructor for an exception message.
        /// </summary>
        /// <param name="message">Message text.</param>
        /// <param name="data">JSON Data</param>
        /// <returns>Response object</returns>
        public static SizeServerResponse ReturnException(string message, JsonElement? data = null)
        {
            return new SizeServerResponse(MSG_TYPE_EXCEPTION, message, data);
        }
    }
}