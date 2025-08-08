using System;
using System.Collections.Generic;
using System.Text;

namespace NepSizeCore
{
    public class SettingsDescriptionAttribute : Attribute
    {
        public string Description { get; private set; }
        public SettingsDescriptionAttribute(string description) 
        { 
            this.Description = description;
        }
    }
}
