using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

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
        public Dictionary<CubeCoordinates, Tuple<int, float>> WindDict;
        public Dictionary<CubeCoordinates, float> MoistureDict;
        public Dictionary<CubeCoordinates, float> RainfallDict;

        /*
        * Data like date, time, and other temporal factors
        */
        public int ElapsedTime;
        public DateTime StartTime;
        public DateTime CurrentTime {
            get {
                return new DateTime();
            }
        }

        /*
         * Default constructor, initialize data stores
         */
        public WorldMapData() {
            WorldDict = new Dictionary<CubeCoordinates, WorldTile>();
            PlateDict = new Dictionary<CubeCoordinates, WorldPlate>();
            TempDict = new Dictionary<CubeCoordinates, float>();
            WindDict = new Dictionary<CubeCoordinates, Tuple<int, float>>();
            MoistureDict = new Dictionary<CubeCoordinates, float>();
            RainfallDict = new Dictionary<CubeCoordinates, float>();
        }

    }
}
