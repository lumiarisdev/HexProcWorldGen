using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{

    public static CameraController instance;

    public Transform cameraTransform;

    public float movementSpeed;
    public float movementTime;
    public float rotationAmount;
    public Vector3 zoomAmount;

    public Vector3 newPosition;
    public Quaternion newRotation;
    public Vector3 newZoom;

    void Start() {
        instance = this;
        var v = new Vector3(EconSim.WorldMap.Instance.generatorArgs.SizeX / 2, transform.position.y, EconSim.WorldMap.Instance.generatorArgs.SizeZ / 2);
        v.x = v.x * 2f * HexMetrics.outerRadius * 0.75f;
        v.z = v.z * HexMetrics.outerRadius * Mathf.Sqrt(3);
        newPosition = transform.position = v;
        newRotation = transform.rotation;
        cameraTransform = GetComponentInChildren<Camera>().transform;
        newZoom = cameraTransform.localPosition;
    }

    // Update is called once per frame
    void Update()
    {
        HandleMouseInput();
        HandleMovementInput();
    }

    void HandleMouseInput() {
        if(Input.mouseScrollDelta.y != 0) {
            newZoom += Input.mouseScrollDelta.y * zoomAmount;
        }
    }

    void HandleMovementInput() {
        if(Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) {
            newPosition += (transform.forward * movementSpeed);
        }
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) {
            newPosition += (transform.forward * -movementSpeed);
        }
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) {
            newPosition += (transform.right * movementSpeed);
        }
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) {
            newPosition += (transform.right * -movementSpeed);
        }

        if(Input.GetKey(KeyCode.Q)) {
            newRotation *= Quaternion.Euler(Vector3.up * rotationAmount);
        }
        if(Input.GetKey(KeyCode.E)) {
            newRotation *= Quaternion.Euler(Vector3.up * -rotationAmount);
        }

        if(Input.GetKey(KeyCode.R)) {
            newZoom += zoomAmount;
        }
        if(Input.GetKey(KeyCode.F)) {
            newZoom -= zoomAmount;
        }

        transform.position = Vector3.Lerp(transform.position, newPosition, Time.deltaTime * movementTime);
        transform.rotation = Quaternion.Lerp(transform.rotation, newRotation, Time.deltaTime * movementTime);
        cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, newZoom, Time.deltaTime * movementTime);

    }

}
