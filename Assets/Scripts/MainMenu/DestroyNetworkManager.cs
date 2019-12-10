using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroyNetworkManager : MonoBehaviour {

	void Start () {
	    if (GameObject.Find("NetworkManager") != null)
        {
            Destroy(GameObject.Find("NetworkManager"));
        }

        NetworkInfo.isNetworkMatch = false;
	}
}
