using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class NetworkManagerHudMine : NetworkBehaviour {

    public NetworkManager manager;
    public Text[] buttonTexts;

    private void Start()
    {
        if (!manager)
        {
            manager = GetComponent<NetworkManager>();
        }
    }

    public void DisableAndAnableHud ()
    {
        if (gameObject.GetComponent<NetworkManagerHUD>().showGUI)
            gameObject.GetComponent<NetworkManagerHUD>().showGUI = false;
        else
            gameObject.GetComponent<NetworkManagerHUD>().showGUI = true;
    }

    #region Local
    public void LocalHost ()
    {
        manager.StartHost();
    }

    public void LocalJoin ()
    {
        manager.StartClient();
    }

    public void Disconnect ()
    {
        manager.StopHost();
    }
    #endregion

    #region MatchMaker
    public void MatchMakerStart ()
    {
        if(manager.matchMaker == null)
        {
            manager.StartMatchMaker();
        }
    }

    public void MatchMakerDisable()
    {
        manager.StopMatchMaker(); 
    }

    public void MatchMakerHost()
    {
        manager.matchMaker.CreateMatch("Test", 2, true, "", "", "", 0, 0, manager.OnMatchCreate);
    }

    public void MatchMakerJoin()
    {
        // all to theri default
        buttonTexts[0].text = "Room 1";
        buttonTexts[1].text = "Room 2";
        buttonTexts[2].text = "Room 3";
        buttonTexts[3].text = "Room 4";

        manager.matchMaker.ListMatches(0, 20, "", false, 0, 0, manager.OnMatchList);

        // finds all rooms
        if (manager.matches != null)
        {
            for (int i = 0; i < manager.matches.Count; i++)
            {
                var match = manager.matches[i];
                buttonTexts[i].text = match.name;
            }
        }
    }

    public void MatchMakerJoinServer_1()
    {
        if (manager.matches != null)
            manager.matchMaker.JoinMatch(manager.matches[0].networkId, "", "", "", 0, 0, manager.OnMatchJoined);
    }

    public void MatchMakerJoinServer_2()
    {
        if (manager.matches != null && manager.matches.Count > 1)
            manager.matchMaker.JoinMatch(manager.matches[1].networkId, "", "", "", 0, 0, manager.OnMatchJoined);
    }

    public void MatchMakerJoinServer_3()
    {
        if (manager.matches != null && manager.matches.Count > 2)
            manager.matchMaker.JoinMatch(manager.matches[2].networkId, "", "", "", 0, 0, manager.OnMatchJoined);
    }

    public void MatchMakerJoinServer_4()
    {
        if (manager.matches != null && manager.matches.Count > 3)
            manager.matchMaker.JoinMatch(manager.matches[3].networkId, "", "", "", 0, 0, manager.OnMatchJoined);
    }
    #endregion
}
