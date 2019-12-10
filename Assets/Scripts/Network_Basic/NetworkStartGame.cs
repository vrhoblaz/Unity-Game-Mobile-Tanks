using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class NetworkStartGame : NetworkBehaviour {

    public NetworkManager netMan;

    public void test ()
    {
        netMan.ServerChangeScene("GamePlay");
    }


}
