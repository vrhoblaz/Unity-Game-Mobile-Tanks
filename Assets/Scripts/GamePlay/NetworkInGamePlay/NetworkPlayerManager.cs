using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class NetworkPlayerManager : NetworkBehaviour
{
    /// Network info of this player
    /// 
    // id on network of this player object - so I can find it from anywhere (server, client, ...)
    NetworkInstanceId objectNetId;

    // index igralca
    public int playerIndex;


    public bool isReadyForGamePlay = false;

    public GameObject tankPrefab;

    #region Setting basic values and names
    // ko se uspešno povežemo na server
    public override void OnStartAuthority()
    {
        base.OnStartAuthority();
        // če imamo authority nad tem objektom
        if (hasAuthority && isClient)
        {
            // najprej nastavi ime / index / net id tega igralca
            CmdSetPlayerNameAndIndexServer();

            // pogej če je še kakšen igralec na serverju ...
            GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("NetworkPlayer");
            foreach (GameObject player in allPlayers)
            {
                // če ta igralec nisem jaz
                if (player != gameObject)
                {
                    // poišči vrednosti; podamo net id od objekta ki ga želimo spremeniti
                    CmdSetValuesOfOtherPlayers(player.GetComponent<NetworkIdentity>().netId);
                }
            }
        }
    }

    // nastavi ime, index, net id na serverju
    [Command]
    void CmdSetPlayerNameAndIndexServer()
    {
        // index je enak connection id igralca; naj bi bil 0 ali 1 če je vse prav; potrebno bo malo stestirat ...
        Debug.LogWarning("Will index allways be \"0\" and \"1\"? \nTesting needed!");  // potrebno malo stestirat; kaj če se igralec prijavi na server, odjavi in pride drug igralec?? kakšen id bo imel??
        playerIndex = connectionToClient.connectionId;
        // preprosto pogledamo kakašen net id ima na serverju in ga dodelimo
        objectNetId = netId;
        // ime ki ima na koncu index igralca, za lažje ločevanje
        gameObject.name = "PlayerGameObjectNetwork_" + playerIndex.ToString();

        // pošljemo komando vsem clientom da se vse spremeni še pri njih
        RpcSetPlayerNameAndIndex(gameObject.name, playerIndex, netId);
    }

    // nastavi ime, index, in net id v vseh skriptah tega objekta na vseh clientih
    [ClientRpc]
    void RpcSetPlayerNameAndIndex(string tankName, int index, NetworkInstanceId id)
    {
        // najprej poiščemo objekt preko net id
        GameObject localObject = ClientScene.FindLocalObject(id);

        // just double check, da smo res našli objekt
        if (localObject != null)
        {
            // poišči to skripto na objektu
            NetworkPlayerManager localObjectScript = localObject.GetComponent<NetworkPlayerManager>();
            // spremeni ime objekta
            localObject.name = tankName;
            // spremeni index v skripti
            localObjectScript.playerIndex = index;
            // spremeni net id value v skripti (potreben če se pridruži še kakšen igralec)
            localObjectScript.objectNetId = id;
        }
    }

    // išče vrednosti od clientov kateri nismo mi
    [Command]
    void CmdSetValuesOfOtherPlayers(NetworkInstanceId id)
    {
        // najprej najdemo objekt na serverju preko net id
        GameObject otherPlayer = NetworkServer.FindLocalObject(id);
        // poiščemo tole skripto
        NetworkPlayerManager otherPlayerScript = otherPlayer.GetComponent<NetworkPlayerManager>();

        // shranimo ime objekta
        string name = otherPlayer.name;
        // shranimo index objekta
        int index = otherPlayerScript.playerIndex;
        // pošljemo informacije vsem clientom
        RpcSetPlayerNameAndIndex(name, index, id);
    }
    #endregion


    #region On GamePlay start and Tank Spawn

    // ko smo na tej napravi zlovdali GamePlay scene, povej serverju da smo pripravjeni
    public void SetPlayerReadyForGamePlay()
    {
        // ker se to kliče na vseh skriptah, končamo če nismo local player; če se to prestavi na kako skripto ki ni na player objectu to ne bo več delalo
        if (!isLocalPlayer)
        {
            return;
        }

        // ukažemo serverju naj poveča število, ki jo server beleži za število pripravjenih igralcev
        CmdSetPlayerReadyForGamePlay();
    }

    // ukažemo serverju naj poveča število, ki jo server beleži za število pripravjenih igralcev
    [Command]
    void CmdSetPlayerReadyForGamePlay()
    {
        // sporočimo map managerju da smo ready
        GameObject.Find("MapManager").GetComponent<MapManager>().numOfPlayersReady++;
    }

    // začetek spawnanja tanka (samo tanka ki si ga bo lokalna naprava potem lastila);
    // povečini identično tistemu v MapManager za neomrežno igro
    public void SpawnMyTank(int[] _heightArray)
    {
        // ker se to kliče na vseh skriptah, končamo če nismo local player; če se to prestavi na kako skripto ki ni na player objectu to ne bo več delalo
        if (!isLocalPlayer)
        {
            return;
        }

        // tukaj nastavimo player index v PlayerManagerju
        GameObject.Find("GameManager").GetComponent<PlayerManager>().SetNetworkPlayerIndexValue(playerIndex);

        // izračunamo začetno pozicijo
        int xStartPosOfTank = (int)((2 * playerIndex + 1) / 4f * _heightArray.Length);
        int yStartPosOfTank = _heightArray[xStartPosOfTank];

        // gremo na server; podamo začetno pozicijo
        CmdSpawnTankOnServer(xStartPosOfTank, yStartPosOfTank);

        if (isServer)
        {
            StartCoroutine(WaitForTankSpawningThenStartTheGame());
        }
    }

    // dokončno spawnanje tanka - za Spawn() funkcijo rabimo biti na serverju!!
    [Command]
    void CmdSpawnTankOnServer(int _xStartPosOfTank, int _yStartPosOfTank)
    {
        // tank index je enak playerIndexu
        int tankIndex = playerIndex;

        // ustvari tank
        GameObject tankInstance = Instantiate(tankPrefab, new Vector3(_xStartPosOfTank, _yStartPosOfTank, 0), Quaternion.identity);

        string tankName = "Tank" + tankIndex;
        // preimenujemo tank
        tankInstance.name = tankName;

        // pošljemo tank index tanku
        tankInstance.GetComponent<TankMoveAndAim>().SetTankIndex(tankIndex);

        // dobimo info o mapi in pošljemo tankom in zgeneriramo edge collider 
        // collider je sicer že zgeneriran, ampak ker je v isti funkciji ga tukaj še 1x (more biti v tej funkciji za naslednja generiranja
        GameObject.Find("MapManager").GetComponent<MapManager>().SendMapInfoToTanks();

        // spawnamo na vseh napravah
        NetworkServer.SpawnWithClientAuthority(tankInstance, gameObject);

        // pošljemo informacije o tanku vsem clientom
        RpcSetTankInfoToClients(tankInstance, tankName, tankIndex);
    }

    // pošljemo informacije o tanku vsem clientom
    [ClientRpc]
    void RpcSetTankInfoToClients(GameObject tank, string name, int tankIndex)
    {
        // nastavimo ime
        tank.name = name;
        // nastavimo index
        tank.GetComponent<TankMoveAndAim>().SetTankIndex(tankIndex);

        // dobimo info o mapi in pošljemo tankom in zgeneriramo edge collider 
        // collider je sicer že zgeneriran, ampak ker je v isti funkciji ga tukaj še 1x (more biti v tej funkciji za naslednja generiranja
        GameObject.Find("MapManager").GetComponent<MapManager>().SendMapInfoToTanks();
    }

    IEnumerator WaitForTankSpawningThenStartTheGame()
    {
        GameObject[] tanks = GameObject.FindGameObjectsWithTag("Player");

        while (tanks.Length < 2)
        {
            tanks = GameObject.FindGameObjectsWithTag("Player");
            yield return new WaitForSeconds(0.5f);
        }

        StartCoroutine(GameObject.Find("GameManager").GetComponent<PlayerManager>().NewRoundStarted());

        yield return null;
    }
    #endregion


    #region Bypassing TankHealth script
    // tukaj bypassamo tank health (ker pač na tisti skripti ponavadi nimamo authorityja, mi ga pa želimo ...
    // lahko bi celo skripto premakil na kakšen GO z avtoriteto ampak mi je bilo takole bolj všeč

    public void DecreseTankHealtRemote(int tankIndex, int newHealth)
    {
        GameObject[] allTanks = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject tank in allTanks)
        {
            TankHealth tankHealthScript = tank.GetComponent<TankHealth>();
            if (tankHealthScript.tankIndex == tankIndex)
            {
                CmdDecreseTankHealthRemote(tank, newHealth);
            }
        }
    }

    [Command]
    void CmdDecreseTankHealthRemote(GameObject tank, int newHealth)
    {
        RpcDecreseTankHealthRemote(tank, newHealth);
    }

    [ClientRpc]
    void RpcDecreseTankHealthRemote(GameObject tank, int newHealth)
    {
        TankHealth tankHealthScript = tank.GetComponent<TankHealth>();
        tankHealthScript.tankCurrentHealth = newHealth;
        tankHealthScript.healthSlider.value = tankHealthScript.tankCurrentHealth;
    }

    public void OnTankDeathRemote(int tankIndex)
    {
        GameObject[] allTanks = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject tank in allTanks)
        {
            // rabim najdit točno določen tank zaradi napisa kdo je zmagal; 
            // lahko bi samo podal index in preskočil ta del ...
            TankHealth tankHealthScript = tank.GetComponent<TankHealth>();
            if (tankHealthScript.tankIndex == tankIndex)
            {
                RpcOnTankDeathRpc(tank);
            }
        }
    }

    [Command]
    void CmdOnTankDeathRemote(GameObject tank)
    {
        RpcOnTankDeathRpc(tank);
    }

    [ClientRpc]
    void RpcOnTankDeathRpc(GameObject tank)
    {
        tank.GetComponent<TankHealth>().OnTankDeath();
    }

    #endregion


    #region Player Turn Bypass
    
    public void StopWaitingForNextPlayerRemote()
    {
        if (!hasAuthority)
            return;
        
        CmdStopWaitingForNextPlayerRemote();
    }

    [Command]
    void CmdStopWaitingForNextPlayerRemote()
    {
        GameObject.Find("GameManager").GetComponent<PlayerManager>().StopWaitingForNextPlayer();
    }

    public void StartWaitingForNextPlayerRemote()
    {
        if (!hasAuthority)
            return;

        CmdStartWaitingForNextPlayerRemote();
    }

    [Command]
    void CmdStartWaitingForNextPlayerRemote()
    {
        GameObject.Find("GameManager").GetComponent<PlayerManager>().StartWaitingForNextPlayer();
    }
    #endregion


    #region Map check

    public void CheckMap(int[] correctHeightArray)
    {
        

        if (!hasAuthority)
            return;
        StartCoroutine(WaitForAllMissleToDestroy(correctHeightArray));
        
    }

    int missileDestroyedCount = 0;
    public void SetMissileDestroyedCount(int count)
    {
        CmdSetMissileDestroyedCount(count);
    }

    [Command]
    void CmdSetMissileDestroyedCount(int _count)
    {
        RpcSetMissileDestroyedCount(_count);
    }

    [ClientRpc]
    void RpcSetMissileDestroyedCount(int _count)
    {
        if (_count == 0)
        {
            missileDestroyedCount = 0;
            print("1");
        }
        else
        {
            print("2");
            missileDestroyedCount += _count;
        }
    }

    IEnumerator WaitForAllMissleToDestroy(int[] correctHeightArray)
    {
        print("qwer: " + missileDestroyedCount);
        while (missileDestroyedCount < 2)
        {
            GameObject[] allMissiles = GameObject.FindGameObjectsWithTag("Missile");
            print(allMissiles.Length + " missiles");
            print("asdqwe : " + missileDestroyedCount);
            yield return new WaitForSeconds(0.1f);
        }

        missileDestroyedCount = 0;
        CmdCheckMap(correctHeightArray);
        //yield return null;
    }

    [Command]
    void CmdCheckMap(int[] correctHeightArray)
    {

        RpcCheckMap(correctHeightArray);

    }

    [ClientRpc]
    void RpcCheckMap(int[] correctHeightArray)
    {
        GameObject.Find("MapManager").GetComponent<MapManager>().MapCompatibility(correctHeightArray);
    }


    #endregion

    





}
