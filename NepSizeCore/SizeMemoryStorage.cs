using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace NepSizeCore
{
    /// <summary>
    /// Event when the list of character is changed.
    /// </summary>
    public class ActiveCharactersChangedEvent : EventArgs
    {
        /// <summary>
        /// Character ID list.
        /// </summary>
        public IList<uint> ActiveCharacters { get; private set; }

        /// <summary>
        /// Creator for the character change event.
        /// </summary>
        /// <param name="activeCharacters"></param>
        public ActiveCharactersChangedEvent(IList<uint> activeCharacters)
        {
            this.ActiveCharacters = activeCharacters;
        }
    }

    /// <summary>
    /// Memory manager which reserves memory in the game's main memory and pins it.
    /// It can then be written by a client.
    /// </summary>
    public class SizeMemoryStorage
    {
        /// <summary>
        /// 16 KB of storage for scales
        /// </summary>
        private const int MEM_SCALELIST_SIZE = 16384;
        /// <summary>
        /// We need 8 byte per character.
        /// </summary>
        private const int MAX_SCALELIST_LENGTH = MEM_SCALELIST_SIZE / 8 - 1;

        /// <summary>
        /// 1 KB of storage for on-screeen characters.
        /// </summary>
        private const int MEM_CHARLIST_SIZE = 1024;
        /// <summary>
        /// We need 4 byte per character.
        /// </summary>
        private const int MEM_CHARLIST_LENGTH = MEM_CHARLIST_SIZE / 4 - 1;

        /// <summary>
        /// Pinnable memory for scales.
        /// </summary>
        private static byte[] _scaleMemory;

        /// <summary>
        /// Pinnable memory for the character list.
        /// </summary>
        private static byte[] _charList;

        /// <summary>
        /// For callbacks.
        /// </summary>
        private static SizeMemoryStorage _instance = null;

        /// <summary>
        /// Plugin to use the log from.
        /// </summary>
        private INepSizeGamePlugin _plugin;

        /// <summary>
        /// Instance generator.
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns></returns>
        public static SizeMemoryStorage Instance(INepSizeGamePlugin plugin)
        {    
            if (_instance == null)
            {
                _instance = new SizeMemoryStorage(plugin);
            }
            return _instance;
        }

        /// <summary>
        /// Handles for pinning scale memory.
        /// </summary>
        private GCHandle _scaleListHandle;
        /// <summary>
        /// Handle for pinning character list memory.
        /// </summary>
        private GCHandle _charListHandle;

        /// <summary>
        /// Address of the scale memory.
        /// </summary>
        private long _scaleListMemoryAddress;

        /// <summary>
        /// Address of the scale memory.
        /// </summary>
        public long ScaleListMemoryAddress { get { return _scaleListMemoryAddress; } }

        
        /// <summary>
        /// Address for the character list memory.
        /// </summary>
        private long _charListMemoryAddress;

        /// <summary>
        /// Address for the character list memory.
        /// </summary>
        public long CharListMemoryAddress { get { return _charListMemoryAddress; } }

        /// <summary>
        /// Memory Stream to read the scale list.
        /// </summary>
        private MemoryStream _scaleListMemoryStream;

        /// <summary>
        /// Binary reader to read the scale list, attached to _scaleListMemory.
        /// </summary>
        private BinaryReader _scaleListMemoryReader;
        /// <summary>
        /// Binary writer to write the scale list, attached to _scaleListMemory.
        /// </summary>
        private BinaryWriter _scaleListMemoryWriter;


        /// <summary>
        /// Memory Stream to read the character list.
        /// </summary>
        private MemoryStream _charListMemoryStream;

        /// <summary>
        /// Binary reader to read the character list, attached to _scaleListMemory.
        /// </summary>
        private BinaryReader _charListMemoryReader;
        /// <summary>
        /// Binary writer to write the character list, attached to _scaleListMemory.
        /// </summary>
        private BinaryWriter _charListMemoryWriter;

        /// <summary>
        /// Fire event when active characters differ from before.
        /// </summary>
        public event EventHandler<ActiveCharactersChangedEvent> ActiveCharactersChanged;

        /// <summary>
        /// Private constructor.
        /// </summary>
        /// <param name="plugin">First plugin instance passed onto the Instance() getter</param>
        private SizeMemoryStorage(INepSizeGamePlugin plugin)
        {
            this._plugin = plugin;

            // Init memory
            SizeMemoryStorage._scaleMemory = new byte[MEM_SCALELIST_SIZE];
            this._scaleListHandle = GCHandle.Alloc(_scaleMemory, GCHandleType.Pinned);
            for (int i = 0; i < MEM_SCALELIST_SIZE; i++)
            {
                SizeMemoryStorage._scaleMemory[i] = 0;
            }

            SizeMemoryStorage._charList = new byte[MEM_CHARLIST_SIZE];
            this._charListHandle = GCHandle.Alloc(_charList, GCHandleType.Pinned);
            for (int i = 0; i < MEM_CHARLIST_SIZE; i++)
            {
                SizeMemoryStorage._charList[i] = 0;
            }

            // Init memory readers
            _scaleListMemoryStream = new MemoryStream(SizeMemoryStorage._scaleMemory);

            this._scaleListMemoryReader = new BinaryReader(_scaleListMemoryStream);
            this._scaleListMemoryWriter = new BinaryWriter(_scaleListMemoryStream);

            this._scaleListMemoryAddress = _scaleListHandle.AddrOfPinnedObject().ToInt64();
            this._plugin.DebugLog("Received memory for scales: " + this._scaleListMemoryAddress.ToString("X"));

            _charListMemoryStream = new MemoryStream(SizeMemoryStorage._charList);

            this._charListMemoryReader = new BinaryReader(_charListMemoryStream);
            this._charListMemoryWriter = new BinaryWriter(_charListMemoryStream);

            this._charListMemoryAddress = _charListHandle.AddrOfPinnedObject().ToInt64();
            this._plugin.DebugLog("Received memory for character list: " + this._charListMemoryAddress.ToString("X"));

            Dictionary<uint, float> scales = ScalePersistence.ReadScales();
            if (scales != null)
            {
                this.UpdateSizes(scales, true);
            }
        }

        /// <summary>
        /// Create properties for the data which read the actual scale data.
        /// </summary>
        public Dictionary<uint, float> SizeValues
        {
            get
            {
                Dictionary<uint, float> values = new Dictionary<uint, float>();
                this._scaleListMemoryStream.Seek(0, SeekOrigin.Begin);
                uint cId;
                while (this._scaleListMemoryStream.Position < (MEM_SCALELIST_SIZE - 8) && (cId = this._scaleListMemoryReader.ReadUInt32()) != 0)
                {
                    float s = this._scaleListMemoryReader.ReadSingle();
                    if (s > 0 && s < float.MaxValue) {
                        values[cId] = s;
                    }
                }
                return values;
            }
        }

        /// <summary>
        /// List of active characters, cache.
        /// </summary>
        private List<uint> _activeCharacterCache = null;        

        /// <summary>
        /// List of active characters, virtual property.
        /// </summary>
        public List<uint> ActiveCharacters
        {
            get
            {
                if (_activeCharacterCache != null)
                {
                    return _activeCharacterCache;
                }

                List<uint> values = new List<uint>();
                this._charListMemoryStream.Seek(0, SeekOrigin.Begin);
                uint cId;
                while (this._charListMemoryStream.Position < (MEM_CHARLIST_SIZE - 4) && (cId = this._charListMemoryReader.ReadUInt32()) != 0)
                {
                    values.Add(cId);
                }

                values.Sort();
                _activeCharacterCache = values;

                return values;
            }
        }

        /// <summary>
        /// Update scales of characters, eventually overwriting it.
        /// </summary>
        /// <param name="sizes">Uint to Scale dictionary of sizes.</param>
        /// <param name="overwrite">Should old data be overwritten.</param>
        /// <exception cref="Exception"></exception>
        public void UpdateSizes(Dictionary<uint, float> sizes, bool overwrite)
        {
            // Check if overwriting is desired
            Dictionary<uint, float> entries;
            if (overwrite)
            {
                entries = new Dictionary<uint, float>();
            }
            else
            {
                entries = this.SizeValues;
            }

            // Copy input
            foreach (KeyValuePair<uint, float> size in sizes)
            {
                float f = size.Value;
                if (f > 0 && f < float.MaxValue)
                {
                    entries[size.Key] = f;
                }
            }            

            // Veryify
            if (entries.Count > MAX_SCALELIST_LENGTH)
            {
                throw new Exception("Maximum storage capacity exceeded");
            }

            // Write memory
            this._scaleListMemoryStream.Seek(0, SeekOrigin.Begin);
            foreach (KeyValuePair<uint, float> entry in entries)
            {
                this._scaleListMemoryWriter.Write(entry.Key);
                this._scaleListMemoryWriter.Write(entry.Value);
            }
            this._plugin.DebugLog("Written");

            this._scaleListMemoryWriter.Write(((uint)0));
        }

        /// <summary>
        /// Store the list of active characters into memory.
        /// </summary>
        /// <param name="characterIds">IDs of characters</param>
        /// <exception cref="Exception"></exception>
        public void UpdateCharacterList(List<uint> characterIds) {
            if (characterIds.Count > MEM_CHARLIST_LENGTH)
            {
                throw new Exception("Maximum storage capacity exceeded");
            }            
            characterIds.Sort();

            bool changed = false;
            if (_activeCharacterCache == null)
            {
                _activeCharacterCache = characterIds;
                changed = true;
            }
            if (!Enumerable.SequenceEqual(characterIds, _activeCharacterCache)) {
                _activeCharacterCache = characterIds;
                changed = true;
            }

            if (changed)
            {
                if (ActiveCharactersChanged != null)
                {
                    ActiveCharactersChanged(this, new ActiveCharactersChangedEvent(_activeCharacterCache.Distinct().ToList()));
                }
            }
            
            this._charListMemoryStream.Seek(0, SeekOrigin.Begin);
            foreach (uint characterId in characterIds)
            {
                this._charListMemoryWriter.Write(characterId);
            }
            this._charListMemoryWriter.Write((uint)0);
        }
    }
}