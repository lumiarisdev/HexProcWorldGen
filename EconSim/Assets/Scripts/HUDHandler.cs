using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class HUDHandler : MonoBehaviour {

    private VisualElement seedDisplay;
    private VisualElement debugSelector;
    private VisualElement startPanel;

    private Button generateButton;
    private Slider progressBar;

    private Toggle terrain;
    private Toggle humidity;
    private Toggle temperature;
    private Label seed;

    EconSim.WorldMap wMap;

    private void OnEnable() {

    }

    public void GenerateButtonPressed() {

        // display progress bar
        progressBar.SetEnabled(true);

        // generate world using coroutine
        StartCoroutine(wMap.Gen.GenerateWorld());

    }

    private void Start() {
        var rootVE = GetComponent<UIDocument>().rootVisualElement;
        wMap = EconSim.WorldMap.Instance;

        // containers
        seedDisplay = rootVE.Q("seed-display");
        debugSelector = rootVE.Q("debug-selector");
        startPanel = rootVE.Q("start-panel");

        generateButton = rootVE.Q<Button>("generate");
        progressBar = rootVE.Q<Slider>("progress");
        generateButton.RegisterCallback<ClickEvent>(ev => GenerateButtonPressed());

        seed = rootVE.Q<Label>("seed");
        terrain = rootVE.Q<Toggle>("terrain-map");
        humidity = rootVE.Q<Toggle>("humidity-map");
        temperature = rootVE.Q<Toggle>("temperature-map");

        progressBar.SetEnabled(false);
        seedDisplay.SetEnabled(false);
        debugSelector.SetEnabled(false);

    }

    private void Update() {
        if (progressBar.value >= 1f) {
            progressBar.value = 0;
            progressBar.SetEnabled(false);
            startPanel.SetEnabled(false);
            startPanel.Remove(progressBar);
        }
        if (progressBar.enabledInHierarchy) {
            progressBar.value = wMap.Gen.progress;
        }
    }

}
