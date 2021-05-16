using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace EconSim
{
    [Serializable]
    public class WorldArgs {

        public int WorldSeed;
        public string StringSeed;
        public bool UseStringSeed;
        public bool RandomizeSeed;

        public int SizeChunksX;
        public int SizeChunksZ;

        public int SizeX {
            get {
                return SizeChunksX * HexMetrics.chunkSizeX;
            }
        }
        public int SizeZ {
            get {
                return SizeChunksZ * HexMetrics.chunkSizeZ;
            }
        }

        [Range(0, 1)]
        public float OceanFrequency;

        public int NumPlates;
        [Range(0, 1)]
        public float PlateSpreadDecay;
        [Range(0, 1)]
        public float PlateMotionScaleFactor;
        [Range(0, 1)]
        public float UpliftDecay;

        [Range(0, 1)]
        public float TemperatureDecay;
        [Range(0, 1)]
        public float TemperatureDecayElevation;

    }

}