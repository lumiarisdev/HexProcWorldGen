using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EconSim
{
    public class WorldMapData
    {

        /*
         * Primary world data dictionaries.
         */
        public Dictionary<CubeCoordinates, WorldTile> WorldDict;
        public Dictionary<CubeCoordinates, WorldPlate> PlateDict;
        public Dictionary<CubeCoordinates, float> TempDict;
        public Dictionary<CubeCoordinates, float> WindDict;
        public Dictionary<CubeCoordinates, float> MoistureDict;
        public Dictionary<CubeCoordinates, float> RainfallDict;

        /*
         * Default constructor, initialize data stores
         */
        public WorldMapData() {
            WorldDict = new Dictionary<CubeCoordinates, WorldTile>();
            PlateDict = new Dictionary<CubeCoordinates, WorldPlate>();
            TempDict = new Dictionary<CubeCoordinates, float>();
            WindDict = new Dictionary<CubeCoordinates, float>();
            MoistureDict = new Dictionary<CubeCoordinates, float>();
            RainfallDict = new Dictionary<CubeCoordinates, float>();
        }

    }
}
