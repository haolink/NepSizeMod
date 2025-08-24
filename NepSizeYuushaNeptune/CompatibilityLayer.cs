using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NepSizeYuushaNeptune
{
    /// <summary>
    /// For this game we are using Texture2D names as identifier - and not model IDs.
    /// Due to the 2D nature of this game it is an outlier. To not reimplement NepSizeCore though
    /// this layer maps names onto "virtual" character IDs.
    /// </summary>
    public static class CompatibilityLayer
    {
        public const int CHAR_NEPTUNE = 100;
        public const int CHAR_NOIRE = 200;
        public const int CHAR_BLANC = 300;
        public const int CHAR_VERT = 400;

        public const int CHAR_IF = 500;
        public const int CHAR_COMPA = 600;

        public const int CHAR_CHROME = 1100;
        public const int CHAR_ARTISAN = 1200;

        /// <summary>
        /// Internal dictionary.
        /// </summary>
        public static readonly Dictionary<string, uint> _uidToTex2DNames = new Dictionary<string, uint>()
        {
            { "neptune", CHAR_NEPTUNE }, { "neptune_battle", CHAR_NEPTUNE },
            { "noire", CHAR_NOIRE }, { "noire_battle", CHAR_NOIRE },
            { "blanc", CHAR_BLANC }, { "blanc_battle", CHAR_BLANC },
            { "vert", CHAR_VERT }, { "vert_battle", CHAR_VERT },
            { "compa", CHAR_COMPA }, { "compa_battle", CHAR_COMPA },
            { "if", CHAR_IF }, { "if_battle", CHAR_IF },
            { "chrome", CHAR_CHROME }, { "chrome_battle", CHAR_CHROME },
            { "artisan", CHAR_ARTISAN }, { "artisan_battle", CHAR_ARTISAN },
        };

        /// <summary>
        /// Assigns texture name to model ID.
        /// </summary>
        /// <param name="texName"></param>
        /// <returns></returns>
        public static uint? UidToTex2DNames(string texName)
        {
            if (String.IsNullOrEmpty(texName))
            {
                return null;
            }

            if (_uidToTex2DNames.TryGetValue(texName, out uint uid))
            {
                return uid;
            }
            return null;
        }
    }
}
