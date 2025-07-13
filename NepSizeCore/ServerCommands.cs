using System;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;

namespace NepSizeCore
{
    /// <summary>
    /// Scale object for JSON.
    /// </summary>
    [Serializable]
    public class ScaleEntry
    {
        public uint id { get; set; }
        public float scale { get; set; }
    }

    /// <summary>
    /// Commands which the pipe server can receive.
    /// </summary>
    public class ServerCommands
    {        
        /// <summary>
        /// Code name of the game.
        /// </summary>
        private readonly string _gameName;

        /// <summary>
        /// Main plugin to use functions from.
        /// </summary>
        private readonly INepSizeGamePlugin _gamePlugin;

        /// <summary>
        /// Public logging method.
        /// </summary>
        /// <param name="message">Log message</param>
        public void Log(string message)
        {
            this._gamePlugin.DebugLog(message);
        }

        /// <summary>
        /// Command set to initialise.
        /// </summary>
        /// <param name="gameName">Code name of the game.</param>
        /// <param name="gamePlugin">Main plugin</param>
        public ServerCommands(string gameName, INepSizeGamePlugin gamePlugin)
        {
            _gameName = gameName;
            _gamePlugin = gamePlugin;
        }

        /// <summary>
        /// Pipe server command to receive scales - unused currently.
        /// </summary>
        /// <param name="scales">List of Json Objects of scale.</param>
        /// <param name="overwrite">Should existing scales be overwritten?</param>
        /// <returns>What to respond to the pipe client.</returns>
        public SizeServerResponse SetScales(List<ScaleEntry> scales, bool overwrite = false)
        {
            Dictionary<uint, float> dict = new Dictionary<uint, float>();
            foreach (var entry in scales)
            {
                dict[entry.id] = entry.scale;
            }

            _gamePlugin.DebugLog($"Received scales - overwriting " + (overwrite ? "enabled" : "disabled"));
            _gamePlugin?.UpdateSizes(dict, overwrite);

            return SizeServerResponse.ReturnSuccess("SetScales OK");
        }

        /// <summary>
        /// Query the game settings so the pipe client knows which game it is and where the memory is to write.
        /// </summary>
        /// <returns>Data to send to the client.</returns>
        public SizeServerResponse GetGameSettings()
        {
            return SizeServerResponse.ReturnSuccess("OK", JsonCompatibility.SerializeToElement(new
            {
                scaleAddress = SizeMemoryStorage.Instance(this._gamePlugin).ScaleListMemoryAddress,
                charList = SizeMemoryStorage.Instance(this._gamePlugin).CharListMemoryAddress,
                game = _gameName
            }
            ));
        }

        /// <summary>
        /// List of currently active character IDs - currently unused.
        /// </summary>
        /// <returns>Character ID list.</returns>
        public SizeServerResponse GetActiveCharacterIds()
        {
            List<uint> charIds = SizeMemoryStorage.Instance(this._gamePlugin).ActiveCharacters;

            return SizeServerResponse.ReturnSuccess("OK", JsonCompatibility.SerializeToElement(new
                {
                    ids = charIds
                }
            ));
        }

        /// <summary>
        /// Reads current scales, returns them as a scale List.
        /// </summary>
        /// <returns>Scale list to reply with.</returns>
        public SizeServerResponse GetCurrentScales()
        {
            Dictionary<uint, float> keyValuePairs = SizeMemoryStorage.Instance(this._gamePlugin).SizeValues;

            List<ScaleEntry> scaleEntries = new List<ScaleEntry>();
            foreach (KeyValuePair<uint, float> entry in keyValuePairs)
            {
                scaleEntries.Add(new ScaleEntry() { id = entry.Key, scale = entry.Value });
            }
            
            return SizeServerResponse.ReturnSuccess("OK", JsonCompatibility.SerializeToElement(new
                {
                    scales = scaleEntries
                }
            ));
        }

        /// <summary>
        /// Update persistence.
        /// </summary>
        /// <param name="clear"></param>
        /// <returns></returns>
        public SizeServerResponse UpdatePersistence(bool clear = false)
        {
            if (clear)
            {
                ScalePersistence.PersistScales(null);
            }
            else
            {
                ScalePersistence.PersistScales(SizeMemoryStorage.Instance(this._gamePlugin).SizeValues);
            }
            return SizeServerResponse.ReturnSuccess("OK");           
        }
    }
}