using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EconSim {

    public class WorldMap : MonoBehaviour {

        public WorldArgs generatorArgs;
        public bool DebugMode;

        public Dictionary<CubeCoordinates, WorldTile> worldTiles;

        private void Awake() {

            WorldGenerator gen = new WorldGenerator(generatorArgs);
            gen.debug = DebugMode;

            worldTiles = gen.GenerateWorld();


        }

        // Start is called before the first frame update
        void Start() {

            

        }

        // Update is called once per frame
        void Update() {

        }

        /*
         * There are some testing gizmos in here.
         * They, ideally, should be removed.
         * Right now, a line is drawn to represnt each plate's motion vector
         */
        private void OnDrawGizmos() {
            if(DebugMode) {
                foreach (WorldTile tile in worldTiles.Values) {
                    if (tile.MotionVector != null) {
                        Vector3 originWorldPos = CubeCoordinates.CubeToOffset(tile.MotionVector.Item1);
                        originWorldPos.x *= (2f * HexMetrics.outerRadius * 0.75f);
                        originWorldPos.y = tile.Elevation * HexMetrics.elevationStep;
                        originWorldPos.z = (originWorldPos.z + originWorldPos.x * 0.5f - originWorldPos.x / 2) * (Mathf.Sqrt(3) * HexMetrics.outerRadius);
                        Vector3 driftWorldPos = CubeCoordinates.CubeToOffset(tile.MotionVector.Item2);
                        driftWorldPos.x *= (2f * HexMetrics.outerRadius * 0.75f);
                        driftWorldPos.y = tile.Elevation * HexMetrics.elevationStep;
                        driftWorldPos.z = (driftWorldPos.z + driftWorldPos.x * 0.5f - driftWorldPos.x / 2) * (Mathf.Sqrt(3) * HexMetrics.outerRadius);
                        //var vWorldPos = driftWorldPos - originWorldPos;
                        //vWorldPos *= 0.125f;
                        //driftWorldPos = originWorldPos + vWorldPos;
                        Gizmos.DrawLine(originWorldPos, driftWorldPos);
                    }
                }
            }
        }

    }

}