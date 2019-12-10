using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class MissileCollision : NetworkBehaviour {

    // 2 skripit ki jih poiščemo v Start funkciji
    MapManager mapManagerScript;
    PlayerManager playerManagerScript;

    // informacija o mapi
    int[] heightArray;

    /// za preračunavanje kota
    /// 
    // trenutena pozicija iztrelka
    Vector3 missilePos;
    // pozicija iztrelka iz prejšnjega frame-a; -500 sm dal da lahko ločim če je že bila kdaj nastavljena 
    Vector3 prevMissilePos = new Vector3(-500,-500,0);
    // missile rigidbody
    Rigidbody2D missileRb;

    // will this missile decrese health; is this missile on current player device
    public bool isMainMissile;

    private void Start()
    {
        // poiščemo skripte
        mapManagerScript = GameObject.FindGameObjectWithTag("MapManager").GetComponent<MapManager>();
        playerManagerScript = GameObject.Find("GameManager").GetComponent<PlayerManager>();
        
        // poiščemo lastnost mape
        heightArray = mapManagerScript.heightArray;

        // poišče rigidbody componento
        missileRb = GetComponent<Rigidbody2D>();
    }

    
    private void Update()
    {
        // uniči iztrelek če je pod mapo - če odleti levo ali desno iz mape
        if (transform.position.x < -5 || transform.position.x > heightArray.Length + 5)
        {
            if (!NetworkInfo.isNetworkMatch || isServer)
            {
                // nehamo čakat na nasljednega igralca; more biti pred Destroy, ker drugače to nebi izvedlo (ničena bi bila tudi skripta);
                playerManagerScript.StopWaitingForNextPlayer();
            }
            else
            {
                print("missle");
                GameObject[] allNetPlayers = GameObject.FindGameObjectsWithTag("NetworkPlayer");
                foreach (GameObject netPlayer in allNetPlayers)
                {
                    netPlayer.GetComponent<NetworkPlayerManager>().StopWaitingForNextPlayerRemote();
                }
            }
            // uničimo iztrelek
            Destroy(gameObject);
        }

        /// by-passing ground collider; samo back check
        ///
        // če smo v mapi
        if (transform.position.x > 0 && transform.position.x < heightArray.Length - 1)
        {
            // če je pozicija iztrelka nižja od višine grounda na tem mestu
            if (transform.position.y <= heightArray[((int)transform.position.x)])
            {
                HitDetected();
            }
        }
        
        /// popravek kota iztrelka
        /// 
        // spremenljivka za vrednost kota
        float kot;
        // samo za prvič 
        if (prevMissilePos.x < -300)
        {
            // samo zapomni pozicijo
            prevMissilePos = transform.position;
            // kot ostane kar enak trenutnemu
            kot = transform.rotation.eulerAngles.z;
        }
        else
        {
            // trenutna pozicija
            missilePos = transform.position;
            // izračunamo kot
            kot = (float)(Mathf.Atan((prevMissilePos.y - missilePos.y) / (prevMissilePos.x - missilePos.x)) / 3.14 * 180);
            // popravimo kot če iztrelek potuje v levo .... nism zihr zakaj je to potrebno, mal sm že pozabu kako grejo trigonometrične funkcije
            if (missilePos.x < prevMissilePos.x)
            {
                kot -= 180f;
            }

            // zapomnimo to pozicijo za naslednji frame
            prevMissilePos = transform.position;
        }
        
        // ta if za vsak slučaj če kot napačno izračuna ...
        if (kot >= -360 && kot <= 360)
        {
            // popravimo kot iztrelka
            missileRb.MoveRotation(kot);
            //transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0, 0, kot), 5f);
        }
    }

    // če zadanemo collider
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // če smo zadeli tank
        if (collision.tag == "Player")
        {
            //if (!NetworkInfo.isNetworkMatch || hasAuthority)
            if (isMainMissile)
            {
                // zmanjšaj health tanka
                collision.GetComponent<TankHealth>().DecreseTankHealt(15);
            }
        }
        HitDetected();
    }

    // sem dal kar v svojo funkcijo da je manj kode
    void HitDetected ()
    {
        // pridobimo info o lokaciji kjer smo zadeli collider oz. ground; enaka trenutni poziciji iztrelka
        Vector2 collisionPointMap = new Vector2(transform.position.x, transform.position.y);
        
        // uniči morebitno mapo okoli te pozicije
        if(!NetworkInfo.isNetworkMatch)
            mapManagerScript.StartDestroyAroundPointOnMap((int)collisionPointMap.x, (int)collisionPointMap.y, 15);
        else
            mapManagerScript.StartDestroyAroundPointOnMap((int)collisionPointMap.x, (int)collisionPointMap.y, 15, isMainMissile);

        /// nehamo čakat na nasljednega igralca; more biti pred Destroy, ker drugače to nebi izvedlo (ničena bi bila tudi skripta);
        ///playerManagerScript.StopWaitingForNextPlayer();   // če iščes tole se je premaknilo v MapManagment skripto na konec "SendMapInfoToTanks()" funkcije

        // uničimo iztrelek
        Destroy(gameObject);
    }
}
