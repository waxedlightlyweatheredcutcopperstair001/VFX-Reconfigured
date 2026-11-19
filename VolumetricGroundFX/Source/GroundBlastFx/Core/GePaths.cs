using System;
using System.IO;

namespace GroundBlastFx.Core
{
    /// <summary>Chemins du mod, déduits de l'emplacement de la DLL (GameData/Volumetric Ground FX/Plugins/GroundBlastFx.dll).</summary>
    public static class GePaths
    {
        private static string _modRoot;

        /// <summary>GameData/Volumetric Ground FX (chemin absolu).</summary>
        // VFX Reconfigured: the merged mod's folder (GameData/VolumetricFXReconfigured); ground settings in PluginData/Ground.
        public static string ModRoot => _modRoot ?? (_modRoot = VFXReconfigured.ModPaths.ModRoot);

        public static string PluginData => VFXReconfigured.ModPaths.GroundData;

        public static string LogFile => Path.Combine(PluginData, "GroundBlastFx.log");

        public static string SettingsFile => Path.Combine(PluginData, "Settings.cfg");

        public static string DefaultSettingsFile => Path.Combine(PluginData, "DefaultSettings.cfg");

        public static string UserLaunchSitesFile => Path.Combine(PluginData, "LaunchSites_user.cfg");

        public static string DevAutomationFile => Path.Combine(PluginData, "DevAutomation.cfg");

        public static string CapturesDir => Path.Combine(PluginData, "Captures");
    }
}
