using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Linq.Expressions;
using Deli.Newtonsoft.Json;

namespace NepSizeCore
{    
    /// <summary>
    /// Scale data can be permanently persisted without the use of the UI.
    /// </summary>
    public static class ScalePersistence
    {
        /// <summary>
        /// Storage of scale file.
        /// </summary>
        /// <returns></returns>
        private static string DetermineFullPathOfJson()
        {
            string baseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string path = Path.Combine(baseDirectory ?? "", "scales.json");

            return path;
        }

        /// <summary>
        /// Read scales.
        /// </summary>
        /// <returns></returns>
        public static Dictionary<uint, float> ReadScales()
        {
            string inputFile = DetermineFullPathOfJson();
            if (!File.Exists(inputFile))
            {
                return null;
            }

            string fileData = File.ReadAllText(inputFile);

            List<ScaleEntry> scaleEntries = null;

            try
            {
                scaleEntries = JsonConvert.DeserializeObject<List<ScaleEntry>>(fileData);
            } 
            catch
            {
                scaleEntries = null;
            }

            if (scaleEntries == null || scaleEntries.Count == 0)
            {
                return null;
            }

            Dictionary<uint, float> scales = new Dictionary<uint, float>();
            foreach (ScaleEntry scaleEntry in scaleEntries)
            {
                scales[scaleEntry.id] = scaleEntry.scale;
            }
            return scales;
        }

        /// <summary>
        /// Write scales.
        /// </summary>
        /// <param name="scales"></param>
        public static void PersistScales(Dictionary<uint, float> scales = null)
        {
            if (scales != null && scales.Count <= 0)
            {
                scales = null;
            }

            string outputFile = DetermineFullPathOfJson();
            if (scales == null)
            {
                if (File.Exists(outputFile))
                {
                    File.Delete(outputFile);
                }
                return;
            }

            List<ScaleEntry> scaleEntries = new List<ScaleEntry>();
            foreach(KeyValuePair<uint, float> kvp in scales)
            {
                scaleEntries.Add(new ScaleEntry() { id = kvp.Key, scale = kvp.Value });
            }

            string scaleJson = null;
            try
            {
                scaleJson = JsonConvert.SerializeObject(scaleEntries);
            }
            catch (Exception ex)
            {
                scaleJson = null;
            }

            if (scaleJson != null)
            {
                File.WriteAllText(outputFile, scaleJson);
            }
        }
    }
}
