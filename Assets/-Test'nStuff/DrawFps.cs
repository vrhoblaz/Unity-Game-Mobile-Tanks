using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DrawFps : MonoBehaviour {

    Text fpsText;

    private void Start()
    {
        fpsText = GetComponent<Text>();
    }

    void Update()
    {
        fpsText.text = (1/Time.deltaTime).ToString("F0");
    }
}

