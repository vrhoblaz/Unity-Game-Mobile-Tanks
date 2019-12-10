using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkAssistant : MonoBehaviour {

    public GameObject[] gameObjectsWithNetworkIdentity;

    void Start() {
        if (!NetworkInfo.isNetworkMatch)
        {
            // aktivacija vseh objektov ki so deaktivirani zaradi "NetworkIdentity" komponente
            ActivateAllObjectsWithNetworkIdentity();
        }
    }

    // Aktivira vse objekte ki so deaktivirani zaradi "NetworkIdentity" komponente
    void ActivateAllObjectsWithNetworkIdentity()
    {
        foreach (GameObject go in gameObjectsWithNetworkIdentity)
        {
            if (!go.activeInHierarchy)
            {
                go.SetActive(true);
            }
        }
    }
	
}
