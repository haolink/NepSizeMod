using System;
using NepSizeCore;

[Serializable]
public class AddtlSettings
{
    [SettingsDescription("Adjust running speed for players")]
    public bool AdjustSpeedPlayer { get; set; } = true;

    [SettingsDescription("Adjust running speed for NPCs")]
    public bool AdjustSpeedNPC { get; set; } = true;
}
