using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class UIHandler : MonoBehaviour
{
    private Button startButton;
    private Slider progressBar;

    private void OnEnable() {

        var rootVE = GetComponent<UIDocument>().rootVisualElement;

        startButton = rootVE.Q<Button>("start-button");
        progressBar = rootVE.Q<Slider>("progress-bar");

        startButton.RegisterCallback<ClickEvent>(ev => StartSim());

    }

    private void StartSim() {

        StartCoroutine(LoadAsync(1));

    }

    IEnumerator LoadAsync(int scene) {
        AsyncOperation operation = SceneManager.LoadSceneAsync(scene);

        while(!operation.isDone) {
            float progress = Mathf.Clamp01(operation.progress / .9f);
            progressBar.value = progress;
            yield return null;
        }
    }

}
