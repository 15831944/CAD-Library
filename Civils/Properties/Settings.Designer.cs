﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace JPP.Civils.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "15.6.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("0.2")]
        public float P_Hatch_Offset {
            get {
                return ((float)(this["P_Hatch_Offset"]));
            }
            set {
                this["P_Hatch_Offset"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("JPP_Plot_Drained_Areas")]
        public string Plot_Drained_Area_Layer {
            get {
                return ((string)(this["Plot_Drained_Area_Layer"]));
            }
            set {
                this["Plot_Drained_Area_Layer"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("256")]
        public int Plot_Drained_Area_Color {
            get {
                return ((int)(this["Plot_Drained_Area_Color"]));
            }
            set {
                this["Plot_Drained_Area_Color"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("ANSI31")]
        public string Plot_Drained_Area_Pattern {
            get {
                return ((string)(this["Plot_Drained_Area_Pattern"]));
            }
            set {
                this["Plot_Drained_Area_Pattern"] = value;
            }
        }
    }
}
