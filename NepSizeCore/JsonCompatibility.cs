using System.Text.Json;

namespace NepSizeCore
{
    /// <summary>
    /// SerializeToElement is not available in the NET 4.6 version of System.Text.Json.
    /// </summary>
    public static class JsonCompatibility
    {
        /// <summary>
        /// Serializes any object as a Json element.
        /// </summary>
        /// <param name="value">Object to serialise.</param>
        /// <returns>Json element.</returns>
        public static JsonElement SerializeToElement(object value)
        {
            string jsonString = JsonSerializer.Serialize(value);

            JsonDocument jsonDoc = JsonDocument.Parse(jsonString);
            JsonElement element = jsonDoc.RootElement.Clone();

            return element;
        }
    }
}