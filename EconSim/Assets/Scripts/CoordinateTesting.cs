using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EconSim;

public class CoordinateTesting : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {

        CubeCoordinates c = new CubeCoordinates(0, 0, 0);
        Vector3 v = c.ToOffset();

        Vector3 v2 = new Vector3(1, 0, 2);
        CubeCoordinates c2 = CubeCoordinates.OffsetToCube(v2);

        //Debug.Log("CC: " + v.ToString());
        //Debug.Log("V: " + c2.ToString());

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
