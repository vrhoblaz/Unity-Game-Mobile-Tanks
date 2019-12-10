using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

public class PlayerManager : NetworkBehaviour
{
    // 2 skripti
    TankFire fireScript;
    TankMoveAndAim moveAndAimScript;

    // Slike z gumbi za premike, merjenje, moč, ...
    GameObject[] img_SelectionImageGameObjects;
    // slika ki se pokaže ko je igra končana
    public GameObject endGameImageGameObject;
    // napis na začetku runde; kateri igralec je navrsti
    GameObject playersTurnGameObject;

    // index igralca ki je navrsti; kontrolira server 
    int playerOnHisTurn;
    // array vseh tankov
    public GameObject[] tanks;

    // bool ki pove ali čakamo preden začne rundo naslednji igralec
    bool waitingForNextPlayer;

    /// timer properties
    /// 
    // koliko časa ima igralec vsako rundo
    public float startRoundTime = 30f;
    // koliko časa ima igralec še to rundo; kontrolira server - syncano na vseh clientih
    [SyncVar]
    float roundTimeLeft;
    // text za izpis preostalega časa
    Text timerText;
    // ali se čas odšteva ali ne
    bool timerIsCounting;

    /// za network
    /// 
    // index ki nam pove kateri igralec smo na networku
    int networkPlayerIndex = -1;

    
    private void Start()
    {
        /// dejmo tole nekam drugam ...
        Application.targetFrameRate = 60;
        ///

        // poiščemo slike
        img_SelectionImageGameObjects = new GameObject[4];
        img_SelectionImageGameObjects[0] = GameObject.Find("Img_00_Blank");
        img_SelectionImageGameObjects[1] = GameObject.Find("Img_01_BasicSelection");
        img_SelectionImageGameObjects[2] = GameObject.Find("Img_02_MoveSelection");
        img_SelectionImageGameObjects[3] = GameObject.Find("Img_03_AimSelection");

        // poiščemo text za timer
        timerText = GameObject.Find("Txt_Timer").GetComponent<Text>();
        // izpišemo timer z začetno vrednostjo; F1 pomeni z eno decimalko;
        timerText.text = startRoundTime.ToString("F1");

        // poišče napis za naslednjega igralca
        playersTurnGameObject = GameObject.Find("Txt_PlayersTurn");
        // deaktivira to sliko
        playersTurnGameObject.SetActive(false);
    }

    // za odštevanje časa
    private void Update()
    {
        // čas odšteva samo če ni network match ali če smo na serverju - server kontrolira čas ...
        if (!NetworkInfo.isNetworkMatch || isServer)
        {
            // če odštevamo (timerIsCounting == true);
            if (timerIsCounting)
            {
                // odštejemo za deta time
                roundTimeLeft -= Time.unscaledDeltaTime;
                // izpišemo text trenutnega časa; F1 pomeni z eno decimalko;
                timerText.text = roundTimeLeft.ToString("F1");
            }

            // če smo prišli do 0 sec
            if (roundTimeLeft < 0)
            {
                // ne bomo več odštevali
                timerIsCounting = false;
                // nastavi čas na 0; da ne bo bugov
                roundTimeLeft = 0f;
                // izpiši text; F1 pomeni z eno decimalko;
                timerText.text = roundTimeLeft.ToString("F1");
                // začni korutino za naslednjega igralca
                StartCoroutine(WaitThenStartNextPlayerTrun());
            }
        }
        // če je network match in nismo server rabimo še vseeno videti kakšen je čas ...
        else if(NetworkInfo.isNetworkMatch && !isServer)
        {
            // izpišemo text trenutnega časa; F1 pomeni z eno decimalko;
            // odštevat ne rabimo ker je sync var in bo server odšteval - tako imamo vsi enak čas
            timerText.text = roundTimeLeft.ToString("F1");
        }
    }

    // nastavi PlayerNetworkIndex; vrednost nam pošlje NetworkPlayerManager
    public void SetNetworkPlayerIndexValue(int _index)
    {
        networkPlayerIndex = _index;
    }

    /// <summary>
    /// vedno ustvari prevelik array
    /// mislim da je sama skripta prehitra in se sami tanki ne uničijo dovolj hitro
    /// ker je skripta prej tukaj kot se tanki uničijo ustvari array z dodatnimi tanki ki se potem uničijo
    /// tako ostane array z praznimi zapisi v njem...
    /// tukaj dejansko želim samo aktivne tanke tako da sem zaenkrat naredil manjši bypass
    /// </summary>
    // do sem pridemo samo z serverjem in če ni network match
    public IEnumerator NewRoundStarted()
    {
        // počaka 0,2 sekuni; zakaj? glej summary zgoraj;
        Debug.LogWarning("Attention needed in this script!\n Double Click to open.");
        yield return new WaitForSeconds(0.2f);

        // da ne odštvamo časa že takoj od začetka
        timerIsCounting = false;

        // nastavi potrebno za začetek nove runde
        SetUpNewRound();

        // nehamo čakat 
        waitingForNextPlayer = false;

        // če je endgame image active, ga daj na inactive
        if (endGameImageGameObject.activeInHierarchy)
        {
            endGameImageGameObject.SetActive(false);
        }

        // če je netwrok igra še rpc-jamo na vseh clientih
        if(isServer)
        {
            // rpc sicer poskrbi tudi za server, ampak rabimo tudi če ni network match tako da 2x preveri ... ni panike, kar pusti tako
            RpcSetEndGameImageInactive();
        }

        yield return null;
    }

    // samo deaktiviramo end game image na vseh clientih če je potrebno...
    [ClientRpc]
    void RpcSetEndGameImageInactive()
    {
        // če je endgame image active, ga daj na inactive
        if (endGameImageGameObject.activeInHierarchy)
        {
            endGameImageGameObject.SetActive(false);
        }
    }

    // samo na začtku igre, ko je ustvarjena nova mapa
    void SetUpNewRound()
    {
        // index igralca navrsti damo na -1; tako ni nihče na vrsti
        playerOnHisTurn = -1;

        // poiščemo vse tanke v igri
        tanks = GameObject.FindGameObjectsWithTag("Player");
        // vsakemu povemo kdo je navrsti
        foreach (GameObject tank in tanks)
        {
            if (tank != null)
            {
                tank.GetComponent<TankMoveAndAim>().SetSelectedTank(playerOnHisTurn);
            }
        }
        
        // če je network sporoči še vsem clientom
        if(isServer)
        {
            RpcSetSelectedTank(playerOnHisTurn);
        }

        // začnemo korutino za začetek nasljednjega igralca
        StartCoroutine(WaitThenStartNextPlayerTrun());
    }

    // sporoči vsem clientom kdo je na vrsti
    [ClientRpc]
    void RpcSetSelectedTank(int playerOnTurn)
    {
        // poiščemo vse tanke v igri
        tanks = GameObject.FindGameObjectsWithTag("Player");
        // vsakemu povemo kdo je navrsti
        foreach (GameObject tank in tanks)
        {
            if (tank != null)
                tank.GetComponent<TankMoveAndAim>().SetSelectedTank(playerOnTurn);
        }
    }
    
    // korutina za začetek nasljednjega igralca
    IEnumerator WaitThenStartNextPlayerTrun()
    {

        /// Waiting ...
        /// 
        // timerja še ne odšteva
        timerIsCounting = false;

        // please wait sliko nastavi na active
        if (!img_SelectionImageGameObjects[0].activeInHierarchy)
        {
            img_SelectionImageGameObjects[0].SetActive(true);
        }

        // še na vseh clientih ..
        if (isServer)
        {
            // parameter true pomeni da smo [0] sliko aktivira
            RpcActivateWaitAndOtherImages(true);
        }

        // čakamo dokler je true ... ; na false se postavi ko izteče timer, ali ko se uniči iztrelek; Timer se ustavi ko ustrelimo, tako da ni komplikacij tukaj
        while (waitingForNextPlayer)
        {
            yield return null;
        }
        
        /// start next tanks turn
        /// 
        // določimo index naslednjega igralca
        int nextPlayerIndex = playerOnHisTurn + 1;
        // če je index večji ali enak številu vseh tankov, je na vrsti spet tank z indexom 0;
        if (nextPlayerIndex >= tanks.Length)
        {
            nextPlayerIndex = 0;
        }

        // nastavimo index igralca na vrsti
        playerOnHisTurn = nextPlayerIndex;  // ta naj bi bil syncan tako da bi se moral takoj postaviti na vseh clientih (ampak mislim da se ne bo ker nismo v Cmd-ju - ne vem)
        // pošljemo ta index vsem tankom
        foreach (GameObject tank in tanks)
        {
            if (tank != null)
                tank.GetComponent<TankMoveAndAim>().SetSelectedTank(playerOnHisTurn);
        }

        // če je network sporoči še vsem clientom
        if (isServer)
        {
            RpcSetSelectedTank(playerOnHisTurn);
        }

        // aktiviramo vse slike z gumbi
        foreach (GameObject imageGO in img_SelectionImageGameObjects)
        {
            if (!imageGO.activeInHierarchy)
            {
                imageGO.SetActive(true);
            }
        }

        // če je network sporoči še vsem clientom
        if (isServer)
        {
            RpcActivateWaitAndOtherImages(false);
        }

        // poiščemo primerne skritpe na tankih
        fireScript = tanks[playerOnHisTurn].GetComponent<TankFire>();
        moveAndAimScript = tanks[playerOnHisTurn].GetComponent<TankMoveAndAim>();

        // če je network sporoči še vsem clientom
        if (isServer)
        {
            RpcFindTankScripts(playerOnHisTurn);
        }

        // konča premike; brez tega: če držiš tipko za premik ko se čas izteče se to shrani 
        // in ko je spet na vrsti ta tank se premika čeprav ne držiš tipke za premik!
        MoveStop();
        // deaktiviramo merjeneje z dotikom če je slučajno še aktivirano; to bi se zgodilo če se odšteje čas ko še merimo;
        TouchAimDeactivate();

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //**************************************************************************************************************
        // tole še 1x poglej 
        // mogoče je potreben Rpc 
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // nastavi sliderje in input fielde; just back check
        fireScript.FirePowerChangeBySliderOrInputField(false, false);
        moveAndAimScript.AimValueChangeBySliderOrInputField(false, false);

        if (!NetworkInfo.isNetworkMatch)
        {
            // izpiše kateri igralec je na vrsti
            playersTurnGameObject.GetComponent<Text>().text = "Player " + playerOnHisTurn + "!";
            // aktivira sliko
            playersTurnGameObject.SetActive(true);
            // počaka #.#f sekund; ta čas je napis aktiven
            while (true)
            {
                yield return new WaitForSeconds(1.5f);
                break;
            }
            // ne rabimo slike ki pravi Please Wait;
            img_SelectionImageGameObjects[0].SetActive(false);

            // deaktiviramo napis;
            playersTurnGameObject.SetActive(false);
        }
        else if (NetworkInfo.isNetworkMatch && isServer)
        {
            // določi ime
            string nextPlayerName = "Player " + playerOnHisTurn + "!";
            // izpiše na vseh clientih
            RpcPrintNextPlayer(nextPlayerName);

            // počaka #.#f sekund; ta čas je napis aktiven
            while (true)
            {
                yield return new WaitForSeconds(1.5f);
                break;
            }

            // deaktivira "Please Wait" sliko samo na primernem clientu in 
            // deaktivira sliko z imenom za naslednjega igralca na vseh clientih
            RpcDeactivateWaitAndNextPlayerImage(playerOnHisTurn);
        }

        // nastavimo preostali čas na začetni čas
        roundTimeLeft = startRoundTime;
        // začnemo šteti
        timerIsCounting = true;
        yield return null;
    }
    
    // izpiše in aktivira napis, kdo je naslednji igralec
    [ClientRpc]
    void RpcPrintNextPlayer(string nextPlayer)
    {
        // izpiše kateri igralec je na vrsti
        playersTurnGameObject.GetComponent<Text>().text = nextPlayer;
        // aktivira sliko
        playersTurnGameObject.SetActive(true);
    }

    // zbriše please wait napis in deaktivira napis za naslednjega igralca
    [ClientRpc]
    void RpcDeactivateWaitAndNextPlayerImage(int playerOnTurn)
    {
        // če je igralec na vrsti enak temu igralcu ...
        if (networkPlayerIndex == playerOnTurn)
        {
            // ne rabimo slike ki pravi Please Wait;
            img_SelectionImageGameObjects[0].SetActive(false);
        }

        // deaktiviramo napis naslednjega igralca;
        playersTurnGameObject.SetActive(false);
    }
    
    // aktivira napise in buttonse; parameter določi ali bo aktiviral samo please wait img ali bo vse; true - samo please wait; false - vse slike
    [ClientRpc]
    void RpcActivateWaitAndOtherImages(bool justWaitImg)
    {
        if (justWaitImg)
        {
            // please wait sliko nastavi na active
            if (!img_SelectionImageGameObjects[0].activeInHierarchy)
            {
                img_SelectionImageGameObjects[0].SetActive(true);
            }
        }
        else
        {
            // aktiviramo vse slike z gumbi
            foreach (GameObject imageGO in img_SelectionImageGameObjects)
            {
                if (!imageGO.activeInHierarchy)
                {
                    imageGO.SetActive(true);
                }
            }
        }
    }

    // najde primerne skripte za tank na vseh clientih; brez parametra ni delalo primerno (čeprav je kao syncano)
    [ClientRpc]
    void RpcFindTankScripts(int playerOnTurn)
    {
        // poiščemo primerne skritpe na tankih
        fireScript = tanks[playerOnTurn].GetComponent<TankFire>();
        moveAndAimScript = tanks[playerOnTurn].GetComponent<TankMoveAndAim>();
    }

    // funkcija da prenehamo čakati za začetek runde naslednjega igralca; kilcana ko je iztrelek uničen ali ko odleti iz mape;
    public void StopWaitingForNextPlayer ()
    {
        waitingForNextPlayer = false;
    }

    public void StartWaitingForNextPlayer()
    {
        waitingForNextPlayer = true;
        StartCoroutine(WaitThenStartNextPlayerTrun());
    }

    // konec igre; ko je uničen en od tankov;
    public void GameFinished(int tankDefetedIndex)
    {
        // aktiviraj sliko
        endGameImageGameObject.SetActive(true);
        // prenehaj šteti čas
        timerIsCounting = false;
        // ustavi vse koorutine v tej skripti
        StopAllCoroutines();
        // določi zmagovalca
        int winner = tankDefetedIndex + 1;
        // če je winner večji od števila vseh tankov, potem je zmagovalec igalec 0;
        if (winner >= tanks.Length)
            winner = 0;
        // poišči text in izpiši zmagovalca
        GameObject.Find("Txt_Winner").GetComponent<Text>().text = "Player " + winner + " has won!";
    }
    

    #region Button Slider & InputField Functions
    /// funkcije za gumbe
    /// 
    // gumb: končaj premike tanka; on pointer up;
    public void MoveStop()
    {
        moveAndAimScript.MoveTank(0);
    }

    // gumb: premik tanka v desno; on pointer down;
    public void MoveRight()
    {
        moveAndAimScript.MoveTank(1);
    }

    // gumb: premik tanka v levo; on pointer down;
    public void MoveLeft()
    {
        moveAndAimScript.MoveTank(-1);
    }

    // gumb: povečanje kota rotacije cevi; on click;
    public void IncreseAimAngel()
    {
        moveAndAimScript.ChangeCevRotationValue(1);
    }

    // gumb: zmanjšanje kota rotacije cevi; on click;
    public void DecreseAimAnge()
    {
        moveAndAimScript.ChangeCevRotationValue(-1);
    }

    // gumb: za aktivacijo spremembe rotacije cevi s klikom; aktivira ko gremo na aim menu; on click;
    public void TouchAimActivate()
    {
        moveAndAimScript.touchAimActive = true;
    }

    // gumb: za deaktivacijo spremembe rotacije cevi s klikom; aktivira ko gremo iz aim menuja; on click;
    public void TouchAimDeactivate()
    {
        moveAndAimScript.touchAimActive = false;
    }

    // gumb: povečanje moči iztrelka; on click;
    public void IncreseFirePower()
    {
        fireScript.ChangeFirePower(1);
    }

    // gumb: zmanjšanje moči iztrelka; on click;
    public void DecreseFirePover()
    {
        fireScript.ChangeFirePower(-1);
    }

    // gumb: iztreli missile; on click;
    public void FireMissle()
    {
        // pošlje še rotacijo cevi, da je iztrelek enako rotiran
        fireScript.FireMissile(moveAndAimScript.GetCevRotationValue());

        // začnemo čakat na naslednjega igralca
        if (!NetworkInfo.isNetworkMatch || isServer)
        {
            StartWaitingForNextPlayer();
        }
        else
        {
            GameObject[] allNetPlayers = GameObject.FindGameObjectsWithTag("NetworkPlayer");
            foreach (GameObject netPlayer in allNetPlayers)
            {
                netPlayer.GetComponent<NetworkPlayerManager>().StartWaitingForNextPlayerRemote();
            }
        }
    }

    /// sliders and inputField functions
    /// 
    // fire power slider; on value change;
    public void FirePowerSliderChanged ()
    {
        fireScript.FirePowerChangeBySliderOrInputField(true, false);
    }

    // fire power input field; on value change;
    public void FirePowerInputFieldChanged ()
    {
        fireScript.FirePowerChangeBySliderOrInputField(false, true);
    }

    // aim rotation slider; on value change;
    public void AimRotationSliderChanged()
    {
        moveAndAimScript.AimValueChangeBySliderOrInputField(true, false);
    }

    // aim rotation slider; on value change;
    public void AimRotationInputFieldChanged()
    {
        moveAndAimScript.AimValueChangeBySliderOrInputField(false, true);
    }

    #endregion
}
