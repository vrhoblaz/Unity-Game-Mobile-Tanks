using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

public class TankHealth : NetworkBehaviour {

    // index tanka; to info pošljemo PlayerManager scripti ko tank umre
    [HideInInspector]
    public int tankIndex;
    
    // začetna vrednost zdravlja
    int tankStartHealth = 100;
    // trenutna vrednost tanka
    [HideInInspector]
    public int tankCurrentHealth;

    // slider za zdravje tanka
    [HideInInspector]
    public Slider healthSlider;

    // netwrork skripta v kateri imamo authority ...
    public NetworkPlayerManager netPlayerManagerSript;


    // nastavimo trenutno vrednost zdravja na začetno vrednost
    private void Start()
    {
        tankCurrentHealth = tankStartHealth;
    }

    // zmanjšamo trenutno vrednost zdravja za podano vrednost
    public void DecreseTankHealt(int decreseHealtValue)
    {
            // nastavimo primerno vrednost zdravja
            tankCurrentHealth -= decreseHealtValue;

            // če je trenutno zdravje manj kot 0, ga nastavimo na 0; da se izognemo morebitnim bugom
            if (tankCurrentHealth < 0)
            {
                tankCurrentHealth = 0;
            }

            // popravimo slider za zdravje
            healthSlider.value = tankCurrentHealth;

        if (NetworkInfo.isNetworkMatch)
        {
            if (hasAuthority)
            {
                // potrebeno preko Cmd-ja ker rpc more biti klican iz serverja. Tukaj smo pa lahko tudi če smo client in si lastimo tank ...
                CmdDecreseTankHealth(tankCurrentHealth);
            }
            else
            {
                netPlayerManagerSript.DecreseTankHealtRemote(tankIndex, tankCurrentHealth);
            }
        }

        // če smo uničili tank (če je tank health 0 ali manj)
        if (tankCurrentHealth <= 0)
        {
            if (!NetworkInfo.isNetworkMatch)
            {
                OnTankDeath();
            }
            else if (hasAuthority)
            {
                CmdOnTankDeath();
            }
            else
            {
                netPlayerManagerSript.OnTankDeathRemote(tankIndex);
            }
        }
    }

    [Command]
    void CmdDecreseTankHealth(int newHealth)
    {
        RpcDecreseTankHealth(newHealth);
    }

    [ClientRpc]
    void RpcDecreseTankHealth(int newHealth)
    {
        tankCurrentHealth = newHealth;
        healthSlider.value = tankCurrentHealth;
    }

    [Command]
    void CmdOnTankDeath()
    {
        RpcOnTankDeath();
    }

    [ClientRpc]
    void RpcOnTankDeath()
    {
        OnTankDeath();
    }

    // funkcija kilcana ko je tank uničen
    public void OnTankDeath()
    {
        // izvede funkcijo GameFinished in poda informacijo kateri tank je bil uničen
        GameObject.Find("GameManager").GetComponent<PlayerManager>().GameFinished(tankIndex);
    }

    // nastavljanje tank indexa
    public void SetTankIndex (int _tankIndex)
    {
        // nastavimo primeren tank index
        tankIndex = _tankIndex;

        // poiščemo primeren health slider
        string sliderName = "Slider_Health_Tank_" + tankIndex.ToString();
        healthSlider = GameObject.Find(sliderName).GetComponent<Slider>();
        // nastavimo polno vrednost health sliderja
        healthSlider.value = tankStartHealth;

        if (NetworkInfo.isNetworkMatch)
        {
            GameObject[] allNetPlayers = GameObject.FindGameObjectsWithTag("NetworkPlayer");
            foreach (GameObject netPlayer in allNetPlayers)
            {
                NetworkPlayerManager netPlayerScript = netPlayer.GetComponent<NetworkPlayerManager>();
                if (netPlayerScript.playerIndex != tankIndex)
                {
                    netPlayerManagerSript = netPlayerScript;
                }
            }
        }
    }
}
