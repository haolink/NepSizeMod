using System;
using System.Collections.Generic;
using Deli.Newtonsoft.Json;

namespace NepSizeCore
{
    /// <summary>
    /// Description of a character set.
    /// </summary>
    [Serializable]
    public class CharacterData
    {
        [JsonProperty("id")]
        public int Id { get; private set; }
        [JsonProperty("name")]
        public string Name { get; private set; }
        [JsonProperty("text")]
        public string Text { get; private set; }

        public CharacterData(int id, string name, string text)
        {
            Id = id;
            Name = name;
            Text = text;
        }
    }

    /// <summary>
    /// A list of characters for a game.
    /// </summary>
    [Serializable]
    public class CharacterList : Dictionary<string, List<CharacterData>> { }
    

    /// <summary>
    /// Set of methods each default size plugin must provide. While several implementations of the final plugins
    /// are heavily identical as each game is compiled against a different version of Unity, only the main plugins
    /// have the actual Unity plugin.
    /// </summary>
    public interface INepSizeGamePlugin
    {
        /// <summary>
        /// Method to set the sizes of characters at.
        /// </summary>
        /// <param name="sizes">Character ID to scale Dictionary</param>
        /// <param name="overwrite">Replace old sizes</param>
        void UpdateSizes(Dictionary<uint, float> sizes, bool overwrite);

        /// <summary>
        /// Queries the list of characters in a game.
        /// </summary>
        /// <returns></returns>
        CharacterList GetCharacterList();
        
        /// <summary>
        /// Determine the currently active characters on-screen.
        /// </summary>
        /// <returns></returns>
        List<uint> GetActiveCharacterIds();

        /// <summary>
        /// Determin the scales of currently active characters.
        /// </summary>
        /// <returns>Character ID to scale Dictionary</returns>
        Dictionary<uint, float> GetCharacterSizes();

        /// <summary>
        /// Log onto the Unity console.
        /// </summary>
        /// <param name="message">Message text</param>
        void DebugLog(string message);
    }
}