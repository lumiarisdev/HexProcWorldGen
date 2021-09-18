using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EconSim {

    public class WorldMap : MonoBehaviour {

        public static WorldMap Instance;

        public HexMap hexMap;

        public WorldGenerator Gen;
        public WorldArgs generatorArgs;
        //public bool DebugMode;

        public WorldMapData worldData;

        private void Awake() {

            if(Instance == null) {
                Instance = this;
            } else {
                Destroy(this);
            }

            Gen = new WorldGenerator(generatorArgs);

        }

        // Start is called before the first frame update
        void Start() {
            hexMap = HexMap.Instance;
        }

        // Update is called once per frame
        void Update() {
            
        }

    }

}
