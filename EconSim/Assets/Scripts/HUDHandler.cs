using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class HUDHandler : MonoBehaviour {

    public static HUDHandler Instance;

    private VisualElement seedDisplay;
    private VisualElement debugSelector;
    private VisualElement startPanel;
    private VisualElement inspector;

    private Button generateButton;
    private Slider progressBar;
    private TextField seedInput;

    //private Toggle terrain;
    //private Toggle humidity;
    //private Toggle temperature;

    private Label seed;
    private Label maxPrecip;

    private Label coords;
    private Label terrain;
    private Label elevation;
    private Label temp;
    private Label humidity;
    private Label windDir;
    private Label windMag;
    private Label precip;

    EconSim.WorldMap wMap;

    private void OnEnable() {

    }

    public event EventHandler<HUDGenerateEventArgs> HUDGenerate;
    public class HUDGenerateEventArgs : EventArgs {
        public int seed;
        public bool randomSeed;
    }

    public void GenerateButtonPressed() {

        // display progress bar
        progressBar.SetEnabled(true);

        // generate world using coroutine
        if(!seedInput.value.Equals("")) {
            var b = int.TryParse(seedInput.value, out int r);
            // wMap.generatorArgs.WorldSeed = b ? r : UnityEngine.Random.Range(0, int.MaxValue);
            // wMap.generatorArgs.RandomizeSeed = false;
            HUDGenerate?.Invoke(this, new HUDGenerateEventArgs {
                seed = b ? r : UnityEngine.Random.Range(0, int.MaxValue),
                randomSeed = false
            });
        } else {
            HUDGenerate?.Invoke(this, new HUDGenerateEventArgs {
                randomSeed = true
            });
        }

    }

    private void Awake() {
        if(Instance == null) {
            Instance = this;
        } else {
            Destroy(this);
        }
    }

    private void Start() {
        var rootVE = GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement;
        wMap = EconSim.WorldMap.Instance;
        mapLoaded = false;

        // containers
        seedDisplay = rootVE.Q("seed-display");
        debugSelector = rootVE.Q("debug-selector");
        startPanel = rootVE.Q("start-panel");
        inspector = rootVE.Q("inspector");

        generateButton = rootVE.Q<Button>("generate");
        progressBar = rootVE.Q<Slider>("progress");
        seedInput = rootVE.Q<VisualElement>("args-input").Q<TextField>("seed-input");
        generateButton.RegisterCallback<ClickEvent>(ev => GenerateButtonPressed());

        seed = seedDisplay.Q<Label>("seed");
        maxPrecip = seedDisplay.Q<Label>("max-precip");

        //terrain = debugSelector.Q<Toggle>("terrain-map");
        //humidity = debugSelector.Q<Toggle>("humidity-map");
        //temperature = debugSelector.Q<Toggle>("temperature-map");

        coords = inspector.Q<Label>("coords");
        terrain = inspector.Q<Label>("terrain");
        elevation = inspector.Q<Label>("elevation");
        temp = inspector.Q<Label>("temp");
        humidity = inspector.Q<Label>("humidity");
        windDir = inspector.Q<Label>("wind-dir");
        windMag = inspector.Q<Label>("wind-mag");
        precip = inspector.Q<Label>("precip");

        progressBar.SetEnabled(false);

        // listen to generatecomplete event
        wMap.Gen.GenerateComplete += GenerateCompleteListener;

    }

    private bool mapLoaded;

    private void Update() {
        if (progressBar.enabledInHierarchy) {
            progressBar.value = wMap.Gen.progress;
        }

        if(mapLoaded) {
            // remove listener since map is loaded
            wMap.Gen.GenerateComplete -= GenerateCompleteListener;
            // raycasting for inspector
            Ray inputRay = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if(Physics.Raycast(inputRay, out hit)) {

                var pos = transform.InverseTransformPoint(hit.point);
                var tile = EconSim.CubeCoordinates.FromPosition(pos);
                coords.text = "Coordinates: " + tile.ToString();
                terrain.text = "Terrain: " + wMap.worldMapData.WorldDict[tile].Terrain.ToString();
                elevation.text = "Elevation: " + wMap.worldMapData.WorldDict[tile].Elevation.ToString();
                temp.text = "Temperature: " + wMap.worldMapData.WorldDict[tile].Temperature.ToString();
                humidity.text = "Absolute Humidity: " + wMap.worldMapData.WorldDict[tile].Humidity.ToString();
                windDir.text = "Wind Direction: " + wMap.worldMapData.WorldDict[tile].Wind.Item1.ToString();
                windMag.text = "Wind Speed: " + wMap.worldMapData.WorldDict[tile].Wind.Item2.ToString();
                precip.text = "Precipitation: " + wMap.worldMapData.WorldDict[tile].Precipitation.ToString();

            }
        }

    }

    public void GenerateCompleteListener(object sender, EventArgs args) {
        startPanel.style.display = DisplayStyle.None;
        seedDisplay.style.display = DisplayStyle.Flex;
        inspector.style.display = DisplayStyle.Flex;
        seed.text = "Seed: " + wMap.generatorArgs.WorldSeed.ToString();
        maxPrecip.text = EconSim.WorldTile.MaxPrecipitation.ToString();
        mapLoaded = true;
    }

}
