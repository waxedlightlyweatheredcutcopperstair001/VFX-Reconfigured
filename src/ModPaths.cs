using System;
using System.IO;

namespace VFXReconfigured
{
    /// <summary>
    /// Where Volumetric FX Reconfigured lives: GameData/VolumetricFXReconfigured, worked out from the DLL's location
    /// (Plugins/VolumetricFXReconfigured.dll) so a copy one folder too deep still finds its files.
    /// The explosion and ground parts keep their own PluginData subfolders: their settings files would otherwise
    /// clash (settings.cfg and Settings.cfg are the same file on Windows).
    /// </summary>
    public static class ModPaths
    {
        public const string FolderName = "VolumetricFXReconfigured";
        public const string DisplayName = "Volumetric FX Reconfigured";

        static string modRoot;

        public static string ModRoot
        {
            get
            {
                if (modRoot != null) return modRoot;
                try
                {
                    string plugins = Path.GetDirectoryName(typeof(ModPaths).Assembly.Location);
                    modRoot = Path.GetDirectoryName(plugins);
                }
                catch (Exception) { modRoot = null; }
                if (string.IsNullOrEmpty(modRoot) || !Directory.Exists(modRoot))
                    modRoot = Path.Combine(Path.Combine(KSPUtil.ApplicationRootPath, "GameData"), FolderName);
                return modRoot;
            }
        }

        public static string PluginData => Path.Combine(ModRoot, "PluginData");
        public static string ExplosionsData => Path.Combine(PluginData, "Explosions");
        public static string GroundData => Path.Combine(PluginData, "Ground");
        public static string Assets => Path.Combine(ModRoot, "Assets");
    }
}
