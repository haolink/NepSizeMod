using System;
using System.Collections.Generic;
using System.Text;

using NepSizeCore;

namespace NepSizeSVSMono
{
    [Serializable]
    public class AddtlSettings
    {
        [SettingsDescription("Adjust running speed for players")]
        public bool AdjustSpeedPlayer {  get; set; } = true;
        
        [SettingsDescription("Adjust running speed for NPCs")]
        public bool AdjustSpeedNPC { get; set; } = true;

        [SettingsDescription("Adjust the player camera")]
        public bool AdjustCamera { get; set; } = true;

        [SettingsDescription("Lower or raise the camera height by")]
        public float CameraOffset { get; set; } = 0.0f;

        [SettingsDescription("Disable Camera Collission")]
        public bool DisableCameraCollission { get; set; } = true;

        [SettingsDescription("Enable unrestricted camera")]
        public bool UnrestrictedCamera { get; set; } = true;

        [SettingsDescription("Disable game UI")]
        public bool DisableUI { get; set; } = false;
    }
}
