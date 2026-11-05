using RimWorld;
using Verse;

namespace Spatialist
{
    // Floor and roof over the whole map, walls around an open square in the middle, the exit at the centre.
    public class GenStep_PocketRoom : GenStep
    {
        public TerrainDef floor;
        public RoofDef roof;
        public ThingDef exitDef;
        public ThingDef wallDef;
        public int openSize = 15;

        public override int SeedPart => 581273641;

        public override void Generate(Map map, GenStepParams parms)
        {
            IntVec3 center = map.Center;
            foreach (IntVec3 cell in map.AllCells)
            {
                map.terrainGrid.SetTerrain(cell, floor);
                if (roof != null)
                {
                    map.roofGrid.SetRoof(cell, roof);
                }
                if (wallDef != null && !IsOpen(cell, center, openSize))
                {
                    GenSpawn.Spawn(wallDef, cell, map);
                }
            }
            GenSpawn.Spawn(ThingMaker.MakeThing(exitDef), center, map);
            MapGenerator.PlayerStartSpot = center;
        }

        public static bool IsOpen(IntVec3 cell, IntVec3 center, int openSize)
        {
            int half = openSize / 2;
            return System.Math.Abs(cell.x - center.x) <= half && System.Math.Abs(cell.z - center.z) <= half;
        }
    }
}
