using UnityEngine;

public class QuickTest : MonoBehaviour
{
    void Start()
    {
        // Créer un gros cube vert visible
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.position = new Vector3(5, 8, -3);
        cube.transform.localScale = Vector3.one * 3;
        cube.GetComponent<Renderer>().material.color = Color.blue;
        cube.name = "BIG_GREEN_TEST_CUBE";

        Debug.Log("TEST CUBE CREATED AT " + cube.transform.position);
    }
}