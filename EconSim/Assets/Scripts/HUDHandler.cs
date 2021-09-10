using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class HUDHandler : MonoBehaviour {

    private Toggle terrain;
    private Toggle humidity;
    private Toggle temperature;
    private Label seed;

    private void OnEnable() {

        var rootVE = GetComponent<UIDocument>().rootVisualElement;

        seed = rootVE.Q<Label>("seed");
        terrain = rootVE.Q<Toggle>("terrain-map");
        humidity = rootVE.Q<Toggle>("humidity-map");
        temperature = rootVE.Q<Toggle>("temperature-map");

    }

    private void Start() {
        seed.text = HexMap.Instance.worldMap.generatorArgs.WorldSeed.ToString();
    }

    private void Update() {
        if(terrain.value) {
            HexMap.Instance.debugMode = DebugMode.None;
        } else if(humidity.value) {
            HexMap.Instance.debugMode = DebugMode.Humidity;
        } else if(temperature.value) {
            HexMap.Instance.debugMode = DebugMode.Temperature;
        }
    }

}
