using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace EconSim
{
    [Serializable]
    public class WorldArgs {
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
        public float PlateMotionScaleFactor;
        public float UpliftDecay;

    }

}