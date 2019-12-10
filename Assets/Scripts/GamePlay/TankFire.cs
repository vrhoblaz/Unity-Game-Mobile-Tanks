using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

public class TankFire : NetworkBehaviour {
    
    // transform of Fire Point; za kalkulacije
    Transform firePoint;
    // transform of cevRotatePoint; za kalkulacije
    Transform cevRotatePoint;
    // missile force multiplier; osnovna hitrost iztrelka
    float maxMissileForce = 13f;// / 60; // /60f je popravek, ki je potreben zaradi spremembe iz "AddForce" v "velocity"
    // moč iztrelka v procentih, ki jo kontroliramo v sami igri; 0-100%
    [Range(0, 100)]
    int firePowerValue = 50;

    // power slider
    Slider firePowerSlider;
    // power input field
    InputField firePowerInputField;


    /// <summary>
    /// za test
    /// </summary>
    public GameObject TestMissile;



    bool isFireing = false;

    private void Start()
    {
        // poišče potrebne komponente
        firePoint = transform.Find("CevRotatePoint/CevSprite/CevFirePoint");
        cevRotatePoint = transform.Find("CevRotatePoint");
        firePowerSlider = GameObject.Find("Slider_FirePower").GetComponent<Slider>();
        firePowerInputField = GameObject.Find("InputField_FirePower").GetComponent<InputField>();
    }
    
    // izstreli missile; pridobi informacijo o rotaciji cevi - da lahko nastavimo začetno vrednost iztrelka
    public void FireMissile (Quaternion startRotation)
    {
        if (!NetworkInfo.isNetworkMatch)
        {
            // ustvari missile
            GameObject missileInstance = Instantiate(TestMissile);

            // nastavi primerno pozicijo
            missileInstance.transform.position = firePoint.position;
            // izračuna moč iztrelka
            Vector2 force = new Vector2((firePoint.position.x - cevRotatePoint.position.x), (firePoint.position.y - cevRotatePoint.position.y));
            // doda force iztrelku
            //missileInstance.GetComponent<Rigidbody2D>().velocity = (force * maxMissileForce * firePowerValue);
            missileInstance.GetComponent<Rigidbody2D>().AddForce(force * maxMissileForce * firePowerValue);
        }
        /*
        else
        {
            GameObject[] allNetPlayers = GameObject.FindGameObjectsWithTag("NetworkPlayer");
            foreach (GameObject netPlayer in allNetPlayers)
            {
                GameObject playerGoWithAuth = netPlayer.GetComponent<NetworkPlayerManager>().getNetPlayerWithAuthority();
                if(playerGoWithAuth != null)
                {
                    CmdSpawnMissle(startRotation, playerGoWithAuth);
                    break;
                }
            }
            
        }
        */
        if (NetworkInfo.isNetworkMatch)
        {
            GameObject[] allNetPlayers = GameObject.FindGameObjectsWithTag("NetworkPlayer");
            foreach (GameObject netPlayer in allNetPlayers)
            {
                netPlayer.GetComponent<NetworkPlayerManager>().SetMissileDestroyedCount(0);
            }

            isFireing = true;

            CmdSpawnmissile();
        }
    }

    [Command]
    void CmdSpawnmissile ()
    {
        RpcSpawnMissile();
    }

    [ClientRpc]
    void RpcSpawnMissile ()
    {
        /*
        GameObject[] allNetPlayers = GameObject.FindGameObjectsWithTag("NetworkPlayer");
        foreach (GameObject netPlayer in allNetPlayers)
        {
            netPlayer.GetComponent<NetworkPlayerManager>().SetMissileCoilisionCountToZero();
        }
        */
        // ustvari missile
        GameObject missileInstance = Instantiate(TestMissile);

        missileInstance.GetComponent<MissileCollision>().isMainMissile = isFireing;
        print(isFireing);
        isFireing = false;

        // nastavi primerno pozicijo
        missileInstance.transform.position = firePoint.position;
        // izračuna moč iztrelka
        Vector2 force = new Vector2((firePoint.position.x - cevRotatePoint.position.x), (firePoint.position.y - cevRotatePoint.position.y));
        // doda force iztrelku
        //missileInstance.GetComponent<Rigidbody2D>().velocity = (force * maxMissileForce * firePowerValue);
        missileInstance.GetComponent<Rigidbody2D>().AddForce(force * maxMissileForce * firePowerValue);
    }

    /*
    [Command]
    void CmdSpawnMissle(Quaternion startRotation, GameObject playerGoWithAuthority)
    {
        // ustvari missile
        GameObject missileInstance = Instantiate(TestMissile);
        // nastavi primerno pozicijo
        missileInstance.transform.position = firePoint.position;

        // izračuna moč iztrelka
        Vector2 force = new Vector2((firePoint.position.x - cevRotatePoint.position.x), (firePoint.position.y - cevRotatePoint.position.y));
        missileInstance.GetComponent<Rigidbody2D>().AddForce(force * maxMissileForce * firePowerValue);

        NetworkServer.SpawnWithClientAuthority(missileInstance, playerGoWithAuthority);

        //RpcAddForceToMissle(missileInstance);
    }

    [ClientRpc]
    void RpcAddForceToMissle(GameObject missileInstance)
    {
        Vector2 force = new Vector2((firePoint.position.x - cevRotatePoint.position.x), (firePoint.position.y - cevRotatePoint.position.y));
        // doda force iztrelku
        //missileInstance.GetComponent<Rigidbody2D>().velocity = (force * maxMissileForce * firePowerValue);
        missileInstance.GetComponent<Rigidbody2D>().AddForce(force * maxMissileForce * firePowerValue);
    }
    */
    // funkcija za spremembo moči iztrelka
    public void ChangeFirePower(int firePowerChangeValue)
    {
        // popravimo moč na primerno vrednost
        firePowerValue += firePowerChangeValue;
        
        // popravimo InputField in slider na primerno vrednost
        FirePowerChangeBySliderOrInputField(false, false);
    }

    /// <summary>
    /// 
    /// funkcija za spremembo vrednosti Sliderja ali InputFielda
    /// prav tako se funkcijo uporablja pri premiku sliderja ali vpisu v inputField
    /// 
    /// </summary>
    /// <param name="changeBySlider"></param>
    /// ta je true ko premaknemo slider
    /// 
    /// <param name="changeByInputField"></param>
    /// ta je true ko vpišemo vrednost
    public void FirePowerChangeBySliderOrInputField(bool changeBySlider, bool changeByInputField)
    {
        if(changeBySlider)
        {
            firePowerValue = (int)firePowerSlider.value;
        }
        else if (changeByInputField)
        {
            firePowerValue = int.Parse(firePowerInputField.text);
        }
        
        firePowerSlider.value = firePowerValue;
        firePowerInputField.text = firePowerValue.ToString();
    }



    /// <summary>
    /// če bi se odločil da bi rad hitrejšo pretvorbo iz "string" to "int";
    /// zaenkrat to samo stoji tukaj za morebitno prihodnjo uporabo
    /// </summary>
    /*
    public static int IntParseFast(string value)
    {
        int result = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char letter = value[i];
            result = 10 * result + (letter - 48);
        }
        return result;
    }
    */
}