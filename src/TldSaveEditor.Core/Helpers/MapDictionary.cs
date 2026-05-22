using System.Collections.Generic;
using System.Linq;

namespace The_Long_Dark_Save_Editor_2.Helpers
{
    // Per-region map metadata. Coordinate transforms are intentionally done with plain doubles
    // (no System.Windows.Point) so this stays cross-platform; the client does the pixel<->game
    // conversion using the origo / pixelsPerCoordinate exposed here.
    public class MapInfo
    {
        public string inGameName;
        public double origoX;
        public double origoY;
        public int width;
        public int height;
        public float pixelsPerCoordinate;
        public string image;

        // game coords -> map-image pixel coords
        public (double X, double Y) ToLayer(double x, double y)
            => (x * pixelsPerCoordinate + origoX, y * -pixelsPerCoordinate + origoY);

        // map-image pixel coords -> game coords
        public (double X, double Y) ToRegion(double px, double py)
            => ((px - origoX) / pixelsPerCoordinate, (py - origoY) / -pixelsPerCoordinate);
    }

    public static class MapDictionary
    {
        private static MapInfo M(double ox, double oy, int w, int h, float ppc, string image)
            => new MapInfo { origoX = ox, origoY = oy, width = w, height = h, pixelsPerCoordinate = ppc, image = image };

        private static readonly Dictionary<string, MapInfo> dict = new Dictionary<string, MapInfo>
        {
            { "CoastalRegion",          M(1441,   1426,   2687, 2065, 0.98541666666f, "CoastalHighwaySF.png") },
            { "LakeRegion",             M(343,    2037,   2330, 2330, 0.9999f,        "MysteryLakeSF.png") },
            { "WhalingStationRegion",   M(94.5,   2018.3, 1434, 1477, 0.98583333333f, "DesolationPointSF.png") },
            { "RuralRegion",            M(-58,    2209,   2000, 2245, 0.68233333333f, "PleasantValleySF.png") },
            { "CrashMountainRegion",    M(62.5,   2006,   2124, 2349, 0.98476190476f, "TimberwolfMountainSF.png") },
            { "MarshRegion",            M(132,    2193,   1988, 2419, 0.937f,         "ForlomMuskeg.png") },
            { "RavineTransitionZone",   M(1275.5, 543.5,  1538, 958,  0.98538461538f, "RavineSF.png") },
            { "HighwayTransitionZone",  M(88,     905.2,  1182, 787,  0.986f,         "CrumblingHighwaySF.png") },
            { "TracksRegion",           M(308,    1746,   1763, 2007, 0.9385f,        "BrokenRailRoadSF.png") },
            { "RiverValleyRegion",      M(99.18,  1832,   1968, 2092, 0.9385f,        "HushedRiverValleySF.png") },
            { "MountainTownRegion",     M(38.15,  2380,   2156, 2606, 0.9385f,        "MountainTownSF.png") },
            { "CanneryRegion",          M(1399,   1401,   2500, 2602, 1.0f,           "CanneryRegion.png") },
            { "AshCanyonRegion",        M(1133.7, 1118,   2274, 2655, 1.0f,           "AshCanyonRegion.png") },
        };

        public static List<string> MapNames => dict.Keys.ToList();

        public static MapInfo GetMapInfo(string mapName) => dict[mapName];

        public static bool MapExists(string region) => region != null && dict.ContainsKey(region);

        public static string GetInGameName(string region)
            => Properties.Resources.ResourceManager.GetString(region) ?? region;
    }
}
