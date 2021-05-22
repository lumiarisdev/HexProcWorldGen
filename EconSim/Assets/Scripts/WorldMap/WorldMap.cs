using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EconSim {

    public class WorldMap : MonoBehaviour {

        public HexMap hexMap;

        public WorldArgs generatorArgs;
        public bool DebugMode;

        public WorldMapData worldData;

        private void Awake() {

            WorldGenerator gen = new WorldGenerator(generatorArgs);

            worldData = gen.GenerateWorld();

            hexMap = GetComponent<HexMap>();


        }

        // Start is called before the first frame update
        void Start() {

            

        }

        // Update is called once per frame
        void Update() {

            if (Input.GetKey(KeyCode.G)) {
                Debug.Log("Generating new world...");
                worldData = WorldGenerator.GenerateWorld(generatorArgs);
                Debug.Log(Time.deltaTime);
                Debug.Log("Refreshing HexMap...");
                hexMap.Refresh();
                Debug.Log(Time.deltaTime);
            }

        }

    }

}
