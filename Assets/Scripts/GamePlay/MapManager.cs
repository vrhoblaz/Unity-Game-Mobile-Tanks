using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Networking;

public class MapManager : NetworkBehaviour {

    #region Variables
    // zapisano koliko naj bo vsak stolpec visok
    [HideInInspector]
    public int[] heightArray;
    // končni zapis mape v 2D array intigerjev
    int[,] mapGrid;
    // število delcev po x & y osi
    public int mapWidth;
    public int mapHeight;
    // uporabljeno pri generiranju mape - 0 - flat line; 100
    int hightDifferenceBetweenTwoColumns;
    // Dejansko glajenje mape
    [Range(0, 100)]
    public int smoothCount;

    // ground tileMap
    public GameObject groundTileMapGameObcjet;
    Tilemap groundTileMap;

    // grass tiles
    public TileBase[] grassTiles;
    // normal ground tiles
    public TileBase[] groundNormalTiles;
    // tile za basic ground
    public TileBase groundBaseTiles;

    // Tank prefab
    public GameObject tankPrefab;

    /// network variables
    /// 
    // ali je sploh newtork igra ali je samo na local napravi
    bool isNetworkGame;
    // player index aka connection id
    int playerIndex;
    // število igralcev ki so pripravljeni da se zgenerira mapa
    public int numOfPlayersReady = 0;


    #endregion

    private void Awake()
    {
        // velikost mape oz število delcev
        mapWidth = 500;
        mapHeight = 220;

        // smooth level
        hightDifferenceBetweenTwoColumns = 5;

        // pretvotimo smoothening v procentno vrednost
        hightDifferenceBetweenTwoColumns = mapHeight * hightDifferenceBetweenTwoColumns / 100;

        // kolikokrat bo šel čez funkcijo glajenja - večkrat kot gre bolj je vse ravno
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // razmišljam da bi pustil to možnost odprto igralcu!!
        smoothCount = 10;
    }

    private void Start()
    {
        // pogledamo ali je igra preko networka ali samo na lokali napravi
        isNetworkGame = NetworkInfo.isNetworkMatch;

        // pridobi ground tilemap componento
        groundTileMap = groundTileMapGameObcjet.GetComponent<Tilemap>();
        
        // nastavi primerno velikost in pozicijo kamere
        Camera.main.orthographicSize = mapHeight / 2 + 30;
        Camera.main.transform.position = new Vector3(mapWidth / 2f, mapHeight / 2f, Camera.main.transform.position.z);

        // postavi novo mapo
        SetUpNewMap();
    }


    public void SetUpNewMap()
    {
        /// Samo če ni network match!
        if (!isNetworkGame)
        {
            // zbrišemo mapo in tanke če slučajno že obstaja
            DeleteMapAndTanksOnStart();

            GenerateMap();

            // izriše mapo
            DrawMapOnTileMap();

            // spawna 2 tanka; prvi ima index 0 in drugi index 1
            SpawnTank(0);
            SpawnTank(1);

            // pošlje map info vsem tankom
            SendMapInfoToTanks();

            // sporočimo PlayerManagerju da se je začela nova igra
            StartCoroutine(GameObject.Find("GameManager").GetComponent<PlayerManager>().NewRoundStarted());
        }
        /// če je networkMatch!
        else if (isNetworkGame)
        {
            // če smo na serverju
            if (isServer)
            {
                // mapo zgeneriramo samo na serverju
                GenerateMap();

                // počakaj na vse igralce da jim odpre scene; če smo tukaj je serverju itaq že naložilo, moramo pa počakat na drugega igralca če ga še ni
                StartCoroutine(WaitForAllPlayers());
            }

            /// to je zdaj za vse
            // poiščemo vse player objecte (local client)
            GameObject[] allPlayerObjects = GameObject.FindGameObjectsWithTag("NetworkPlayer");
            foreach (GameObject player in allPlayerObjects)
            {
                // v vsakemu sprožimo skripto da poveča število pripravljenih igralcev; samo če je to njegov objekt
                player.GetComponent<NetworkPlayerManager>().SetPlayerReadyForGamePlay();
            }
        }
        /// samo za vsak slučaj; tukaj notri pridemo samo če bi bil "bool = null"
        else
        {
            Debug.LogError("\"isNetworkGame\" bool not set up correctly!");
        }
    }
    
    // korutina za server; čakamo na vse igralce ko se gameplay začne
    IEnumerator WaitForAllPlayers ()
    {
        // dokler nista oba igralca pripravljena, čakamo
        while (numOfPlayersReady < 2)
        {
            yield return new WaitForSeconds(.5f);   
        }

        // zgenerira in zriše mapo na vsakem clientu posebaj
        RpcGridGenerateAndDrawMap(heightArray);

        // postavimo vrednost na 0 da smo pripravljeni za naslednjič
        numOfPlayersReady = 0;

        // komanda da se spawna tank☺
        CmdSpawnTankForLocal();
    }

    // zriše mapo na vseh lokalinh napravah
    [ClientRpc]
    void RpcGridGenerateAndDrawMap(int[] serverHeightArray)
    {
        /// za vsakega client-a posebaj: 
            // zbriše mapo
            // nastavi primeren heightarray
            // zgenerira svoj mapgrid 
            // zriše mapo
        if (isClient)
        {
            // zbriši trenutno mapo
            DeleteMapAndTanksOnStart();

            // nastavi primeren height array
            heightArray = serverHeightArray;//serverHeightArray;

            // zgenerira mapgrid
            CreateMapGrid();

            // izriše mapo
            DrawMapOnTileMap();
        }
    }

    // na serverju poiščemo vse igralce
    [Command]
    void CmdSpawnTankForLocal()
    {
        // najdemo vse igralce
        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("NetworkPlayer");
        foreach (GameObject player in allPlayers)
        {
            // za vsak objekt sprožimo rpc; tako da se skripta sproži na vseh napravah
            RpcSpawnT(player);
        }
    }

    // sproži skripto za tak; sprejmemo to skripto
    [ClientRpc]
    void RpcSpawnT(GameObject player)
    {
        // sprožimo skripto; podamo informacijo o mapi
        player.GetComponent<NetworkPlayerManager>().SpawnMyTank(heightArray);
    }
    
    #region MapSmooth_Draw_CorrectOnLevitate

    // Generiranje mape
    void GenerateMap()
    {
        // spremenjlivka, ki beleži novo random vrednost za zapsi v array
        // za začetek smo nekje med 1/4 in 3/4 višine
        int tempInt = Random.Range(mapHeight / 4, mapHeight * 3 / 4);
        // določitev velikosti arraya
        heightArray = new int[mapWidth];
        

        // ker je trenutno večja vrjetnost da je mapa na levi višja kot na desni sem dal naj random izbere iz katere strani bo polnilo heightArray
        int fillArraySide = Random.Range(0, 2); // 0 - iz leve proti desni; 1 - iz desne proti levi
        // iz leve proti desni
        if (fillArraySide == 0)
        {
            for (int i = 0; i < mapWidth; i++)
            {
                // zapis v array
                heightArray[i] = tempInt;
                // nastavimo low in high value (glede na prejšno vrednost in smoothening)
                int lowInt = tempInt - hightDifferenceBetweenTwoColumns;
                int highInt = tempInt + hightDifferenceBetweenTwoColumns;

                // če smo izven mape popravi da smo nazaj v mapi
                if (lowInt < 0) lowInt = 0;
                if (highInt > mapHeight) highInt = mapHeight;

                //eksponentno padanaje/naraščanje, če smo previsoko/prenizko
                #region exponentFallAndRise
                // če smo med vrednostjo highMin in highMax je velika verjetnost da gremo navzdol
                int highMin = 85 * mapHeight / 100;
                int highMax = 95 * mapHeight / 100;

                if (tempInt > highMin)
                {
                    float tempDiff = tempInt - highMin;
                    float procentDiff = 1 - (tempDiff / (highMax - highMin));
                    if (procentDiff < 0) procentDiff = 0f;
                    highInt = Mathf.RoundToInt((highInt - tempInt) * procentDiff + tempInt);
                }

                // če smo med vrednostjo lowMin in lowMax je velika verjetnost da gremo navzgor
                int lowMin = 7 * mapHeight / 100;
                int lowMax = 23 * mapHeight / 100;

                if (tempInt < lowMax)
                {
                    float tempDiff = lowMax - tempInt;
                    float procentDiff = 1 - (tempDiff / (lowMax - lowMin));
                    if (procentDiff < 0) procentDiff = 0f;
                    lowInt = Mathf.RoundToInt(tempInt - (tempInt - lowInt) * procentDiff);
                }
                #endregion

                // generiranje nove random vrednosti
                tempInt = Random.Range(lowInt, highInt);
            }
        }
        // in še iz druge strani; identično kot zgoraj, le da zapisujemo v array iz konca proti začetku
        else
        {
            for (int i = mapWidth - 1; i >= 0; i--)
            {
                // zapis v array
                heightArray[i] = tempInt;
                // nastavimo low in high value (glede na prejšno vrednost in smoothening)
                int lowInt = tempInt - hightDifferenceBetweenTwoColumns;
                int highInt = tempInt + hightDifferenceBetweenTwoColumns;

                // če smo izven mape popravi da smo nazaj v mapi
                if (lowInt < 0) lowInt = 0;
                if (highInt > mapHeight) highInt = mapHeight;

                //eksponentno padanaje/naraščanje, če smo previsoko/prenizko
                #region exponentFallAndRise
                // če smo med vrednostjo highMin in highMax je velika verjetnost da gremo navzdol
                int highMin;
                int highMax;
                highMin = 85 * mapHeight / 100;
                highMax = 95 * mapHeight / 100;

                if (tempInt > highMin)
                {
                    float tempDiff = tempInt - highMin;
                    float procentDiff = 1 - (tempDiff / (highMax - highMin));
                    if (procentDiff < 0) procentDiff = 0f;
                    highInt = Mathf.RoundToInt((highInt - tempInt) * procentDiff + tempInt);
                }

                // če smo med vrednostjo lowMin in lowMax je velika verjetnost da gremo navzgor
                int lowMin;
                int lowMax;
                lowMin = 7 * mapHeight / 100;
                lowMax = 23 * mapHeight / 100;

                if (tempInt < lowMax)
                {
                    float tempDiff = lowMax - tempInt;
                    float procentDiff = 1 - (tempDiff / (lowMax - lowMin));
                    if (procentDiff < 0) procentDiff = 0f;
                    lowInt = Mathf.RoundToInt(tempInt - (tempInt - lowInt) * procentDiff);
                }
                #endregion
                // generiranje nove random vrednosti
                tempInt = Random.Range(lowInt, highInt);
            }
        }

        // zgladimo mapo
        SmoothMap();
    }

    // zbriše vse delce pred generiranjem nove mape
    void DeleteMapAndTanksOnStart()
    {
        // počisti vse tile-e
        groundTileMap.ClearAllTiles();

        // poišči tanke
        GameObject[] tanks = GameObject.FindGameObjectsWithTag("Player");
        // uniči najdene tanke
        foreach (GameObject tank in tanks)
        {
            Destroy(tank);
        }
    }

    // Zgladitev mape
    void SmoothMap()
    {
        // popravimo začetne in končne vredosti, ki se same ne zgladijo
        // easiest way I could think of in this moment :D
        #region startEndValueCorrection
        int tempHeight;
        //začetne vrednosti
        tempHeight = heightArray[3];
        for (int i = 0; i < 3; i++)
        {
            heightArray[i] = tempHeight;
        }
        // končne vrednosti
        tempHeight = heightArray[heightArray.Length - 3];
        for (int i = heightArray.Length - 3; i < heightArray.Length; i++)
        {
            heightArray[i] = tempHeight;
        }
        #endregion

        // dejansko glajenje
        for (int i = 0; i < smoothCount; i++)
        {
            for (int j = 3; j < heightArray.Length - 3; j++)
            {
                heightArray[j] = (heightArray[j - 3] + heightArray[j - 2] + heightArray[j - 1] + heightArray[j] + heightArray[j + 1] + heightArray[j + 2] + heightArray[j + 3]) / 7;
            }
        }

        // samo če ni network igra
        if (!isNetworkGame || isServer)
        {
            // Zgenerira map grid
            CreateMapGrid();
        }
    }

    // detiermine what tile will be where (grass, basetile, groundtile,..)
    void CreateMapGrid()
    {
        // določa obseg vsake vrste tile-ov
        int grassCount = 3;
        int groundCount1 = 5;
        int groundCount2 = 30;

        // kako veliko naj bo prekrivanje med različnimi vrstami tile-ov
        int borderGrassGround = 1;
        int borderGroudnGround1 = 1;

        // spreznimo oz. ustvarimo nove mapGrid
        mapGrid = new int[mapWidth, mapHeight];

        // za vsak x ...
        for (int x = 0; x < mapWidth; x++)
        {
            // določimo kako veliko naj bi bilo prekrivanje na posameznem x-u
            int borderGrassGroundCorrection = Random.Range(-borderGrassGround, borderGrassGround);
            int borderGroudnGroundCorrection1 = Random.Range(-borderGroudnGround1, borderGroudnGround1 + 1);

            // izračun nove velikosti posamezne vrste tile-a
            int grasCountCorrected = grassCount - borderGrassGroundCorrection;
            int groundCountCorrected1 = groundCount1 + borderGrassGroundCorrection - borderGroudnGroundCorrection1;
            int groundCountCorrected2 = groundCount2 + borderGroudnGroundCorrection1;

            // šteje y iz vrha proti dnu;
            int yCount = 0;
            // za vsak y iz vrha proti dnu ...
            for (int y = mapHeight; y >= 0; y--)
            {
                // če je y manjši od določene višine mape
                if (y < heightArray[x])
                {
                    yCount++;
                    // base ground tile
                    if (y < 2)
                    {
                        mapGrid[x, y] = 1;
                    }
                    // ground tile 1
                    else if (yCount > grasCountCorrected + groundCountCorrected1 + groundCountCorrected2)
                    {
                        mapGrid[x, y] = 2;
                    }
                    // ground tile 2
                    else if (yCount > grasCountCorrected + groundCountCorrected1)
                    {
                        mapGrid[x, y] = 3;
                    }
                    // ground tile 3
                    else if (yCount > grasCountCorrected)
                    {
                        mapGrid[x, y] = 4;
                    }
                    // grass tile
                    else
                    {
                        mapGrid[x, y] = 5;
                    }
                }
            }
        }
    }

    // zriše primerne Tile-e
    void DrawMapOnTileMap()
    {
        // za vsak x in vsak y v mapGrid-u
        for (int x = 0; x < mapGrid.GetLength(0); x++)
        {
            for (int y = 0; y < mapGrid.GetLength(1); y++)
            {
                // trenutna pozicija
                Vector3Int position = new Vector3Int(x, y, 0);
                // random vrednost za ground tile array index
                int arrayIndex;
                
                // zdaj pa glede na določeno vrednost daj pravi tile
                switch (mapGrid[x, y])
                {
                    case 1: // base ground tile
                        groundTileMap.SetTile(position, groundBaseTiles);
                        break;
                    case 2: // ground tile 1
                        arrayIndex = Random.Range(3, 6);
                        groundTileMap.SetTile(position, groundNormalTiles[arrayIndex]);
                        break;
                    case 3: // ground tile 2
                        arrayIndex = Random.Range(2, 4);
                        groundTileMap.SetTile(position, groundNormalTiles[arrayIndex]);
                        break;
                    case 4: // ground tile 3
                        arrayIndex = Random.Range(0, 2);
                        groundTileMap.SetTile(position, groundNormalTiles[arrayIndex]);
                        break;
                    case 5: // grass tiles
                        arrayIndex = Random.Range(0, 2);
                        groundTileMap.SetTile(position, grassTiles[arrayIndex]);
                        break;
                }
            }
        }
        // zgenerira edgeCollider za ground
        GenerateGroundEdgeCollider();
    }

    // zgnerira edge collider; Mapgrid collider je preprosto prezahteven tudi če uporabiš Composite collider zraven ...
    void GenerateGroundEdgeCollider()
    {
        // array točk za collider; dolžina je +5 ker sem 5 točk sam dodal da gre collider ogrog in okrog mape; to ni glih nujno ampak zdj je narjen :)
        Vector2[] colliderPoints = new Vector2[mapWidth + 5];
        // za vsako x pozicijo dodaj točko v array
        for (int x = 0; x < mapWidth; x++)
        {
            // + 5 je zato ker sem 5 točk sam dodal; +0.5f je zato ker želimo točko na sredini kvadrata; +1 ne vem zakaj je - nekje sm mogu zajebat neki
            colliderPoints[x + 5] = new Vector2(x + 0.5f, heightArray[x] + 1);
        }
        // dodanih 5 točk
        // desno zgoraj je enaka zadnji točki v arrayu (da je sklenjen krog)
        colliderPoints[0] = colliderPoints[colliderPoints.Length - 1];
        // čisti zgornji desni kot mape
        colliderPoints[1] = colliderPoints[colliderPoints.Length - 1];
        colliderPoints[1].x += 0.5f;
        // spodnji desno kot mape
        colliderPoints[2] = new Vector2(mapWidth - 1, 0);
        // spodnji levi kot mape
        colliderPoints[3] = new Vector2(0, 0);
        // čisti zgornji levi kot mape
        colliderPoints[4] = colliderPoints[5];
        colliderPoints[4].x -= 0.5f;

        // poiščemo komponento
        EdgeCollider2D coll = GameObject.Find("Ground").GetComponent<EdgeCollider2D>();
        // komponenti določimo naše na novo ustvarjene točke
        coll.points = colliderPoints;
    }

    // Dropa kar je v zraku
    IEnumerator DropLevitatedTiles()
    {
        // to bo true dokler je kaj v zraku; dokler je to true se while zanka ponavlja
        bool correctionNeeded = true;

        // osnovna wile zanka ki se ponavlja
        while (correctionNeeded)
        {
            // damo na false, da takoj rečemo naj ne ponavlja več; kasneje če najde dvignjen delec se da nazaj na ture;
            correctionNeeded = false;

            // za vsak x in y ...
            for (int x = 0; x < mapWidth; x++)
            {
                for (int y = 1; y < mapHeight; y++)
                {
                    // če tile ni prazen in tile pod njim je prazen
                    if (mapGrid[x, y] != 0 && mapGrid[x, y - 1] == 0)
                    {
                        // nastavimo na true da se bo zanka ponovila
                        correctionNeeded = true;

                        // da pogledam če se je y kaj spremenil oz. ali je samo en delec dvignjen
                        int yStart = y; 

                        // trenutna pozicija tile-a ki jo čekiramo
                        Vector3Int tilePosition = new Vector3Int(x, y, 0);
                        // odštejemo 1 da smo na praznem tile-u
                        tilePosition.y -= 1;

                        // da gremo čez naslednjo zanko vsaj 1x; potrebno če bi bil na neki x poziciji v zraku samo 1 delec
                        bool doWhileLoopAtleastOnce = true;

                        // dokler je tile nad tem poln (ni null);
                        while (mapGrid[x, y + 1] != 0 || doWhileLoopAtleastOnce)
                        {
                            // tega ne rabimo več; smo že zagotovili da smo šli vsaj 1x v zanko
                            doWhileLoopAtleastOnce = false;

                            // če trenutni tile ni enak temu nad njim; ne gleda prav tile, ampak samo katere vrste je (imam več tile-ov za eno vrsto, da lepše zgleda);
                            if (mapGrid[x, y] != mapGrid[x, y - 1])
                            {
                                // nastavimo vrednost spodnjega da se ujema z zgornjim
                                mapGrid[x, y - 1] = mapGrid[x, y];

                                // podobno kot pri DrawMapOnTileMap()
                                int arrayIndex; // za random tile iz arraya
                                // poglej kateri tile želimo
                                switch (mapGrid[x, y])
                                {
                                    case 1: // base ground
                                        groundTileMap.SetTile(tilePosition, groundBaseTiles);
                                        break;
                                    case 2: // ground 1
                                        arrayIndex = Random.Range(3, 6);
                                        groundTileMap.SetTile(tilePosition, groundNormalTiles[arrayIndex]);
                                        break;
                                    case 3: // ground 2
                                        arrayIndex = Random.Range(2, 4);
                                        groundTileMap.SetTile(tilePosition, groundNormalTiles[arrayIndex]);
                                        break;
                                    case 4: // ground 3
                                        arrayIndex = Random.Range(0, 2);
                                        groundTileMap.SetTile(tilePosition, groundNormalTiles[arrayIndex]);
                                        break;
                                    case 5: // grass
                                        arrayIndex = Random.Range(0, 2);
                                        groundTileMap.SetTile(tilePosition, grassTiles[arrayIndex]);
                                        break;
                                }
                            }
                            // povečaj y in tilePos.y
                            y++;
                            tilePosition.y++;
                        }
                        //če se y še ni spremenil se bo povečal za 1
                        if (y == yStart)
                        {
                            y++;
                        }
                        // nastavimo vrednost zgornjega dela na ;
                        mapGrid[x, y] = 0;
                        tilePosition.y++;
                        // zbriše zgornji tile od dvignjenega dela
                        groundTileMap.SetTile(tilePosition, null);
                    }
                }
            }
            yield return null;
        }
        // na novo zgenerira colider
        //groundTileMapGameObcjet.GetComponent<CompositeCollider2D>().GenerateGeometry();   // vrjetno ne bom uporabljal

        // pošlje map info vsem tankom
        SendMapInfoToTanks();
        yield return null;
    }


    /// <summary>
    /// 
    /// zaenkrat sem se odločil da tega ne bom uporavljal
    /// 
    /// ne vem zakaj ampak ne dela drgač
    /// išči naprej zakaj je temu tako!!
    /// ubistvu niti ne vem še kakšen mode želim. Zaenkrat bom pustu avtomatsko
    /// </summary>
    /// <returns></returns>
    /*
    IEnumerator GenerateCompositeCollider()
    {
        yield return new WaitForSeconds(0.2f);
        groundTileMapGameObcjet.GetComponent<CompositeCollider2D>().GenerateGeometry();
        yield return null;
    }
    */

    #region testing dropanje grounda
    /*
IEnumerator DropLevitatedTiles()
{
    bool correctionNeeded = true;

    while (correctionNeeded)
    {
        correctionNeeded = false;

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 1; y < mapHeight; y++)
            {
                if (mapGrid[x, y] != 0 && mapGrid[x, y - 1] == 0)
                {
                    correctionNeeded = true;
                    int yStart = y; // da pogledam če se je y kaj spremenil oz. ali je samo en delec dvignjen

                    Vector3Int tilePosition = new Vector3Int(x, y, 0);

                    mapGrid[x, y - 1] = mapGrid[x, y];

                    TileBase oldTile = groundTileMap.GetTile(tilePosition);
                    tilePosition.y -= 1;
                    groundTileMap.SetTile(tilePosition, oldTile);

                    while (mapGrid[x, y + 1] != 0)
                    {
                        mapGrid[x, y - 1] = mapGrid[x, y];

                        oldTile = groundTileMap.GetTile(tilePosition);
                        tilePosition = new Vector3Int(x, y - 1, 0);
                        groundTileMap.SetTile(tilePosition, oldTile);
                        y++;
                        tilePosition.y = y;
                    }
                    //če se y še ni spremenil se bo povečal za 1
                    if (y == yStart)
                    {
                        y++;
                    }
                    mapGrid[x, y] = 0;
                    // zbriše zgornji tile od dvignjenega dela
                    groundTileMap.SetTile(tilePosition, null);
                }
            }
        }
        //groundTileMapGameObcjet.GetComponent<CompositeCollider2D>().GenerateGeometry();
        yield return null;
    }
    // na novo zgenerira colider
    //groundTileMapGameObcjet.GetComponent<CompositeCollider2D>().GenerateGeometry();

    // pošlje map info vsem tankom
    SendMapInfoToTanks();
}
*/

    /*
     * well i tried
     */
    /*
IEnumerator DropLevitatedTiles()
{
   // ta bo true dokler bo kaj v zraku
   bool correctionNeeded = true;
   // lista vseh x pozicij; to listo zmanjšujemo tako da ne preverjamo ves čas cele mape
   List<int> xPosToCheck = new List<int>();
   for (int x = 0; x < mapWidth; x++)
   {
       xPosToCheck.Add(x);
   }

   // glavna while zanka ki poteka dokler je kaj v zraku (aka doker je correctionNeeded = true)
   while (correctionNeeded)
   {
       // damo na false tako da se while zaključi če ni nič več v zraku
       correctionNeeded = false;

       // za vsako pozicijo v listi, preveri ali je v zraku
       //foreach (int x in xPosToCheck)
       for (int x = 0; x < mapWidth; x++)
       {
           // potuj po vseh y vrednostih
           for (int y = 1; y < mapHeight; y++)
           {
               // nastavimo pozicijo ki jo preverjamo
               Vector3Int tilePosition = new Vector3Int(x, y, 0);
               // če je na tej poziciji tile + ga pod to pozicijo ni
               if (groundTileMap.GetTile(tilePosition) != null && groundTileMap.GetTile(tilePosition + new Vector3Int(0,-1,0)) == null)
               {
                   // ker je bil najden dvignjen delec bomo vse ponovili še 1x
                   correctionNeeded = true;
                   // to bo kvadrat ki ga bomo premaknili
                   BoundsInt moveArea = new BoundsInt();
                   // nastavimo levi spodnji kot kvadrata
                   moveArea.zMin = 0;
                   moveArea.xMin = x;
                   moveArea.yMin = y;

                   // da si zapomnemo začetno pozicijo
                   int startX = x;
                   int startY = y;
                   // poiščemo desni zgornji kot kvadrata
                   y--;
                   x++;
                   tilePosition.y--;
                   tilePosition.x++;
                   // najprej čekiramo desno
                   while (groundTileMap.GetTile(tilePosition) == null)
                   {
                       x++;
                       tilePosition.x++;
                   }
                   // smo našli končno desno pozicijo;
                   int xEndPos = x - 1;

                   // preverimo še max višino
                   int yMax = 0;
                   x = startX;
                   for (x = startX; x <= xEndPos; x++)
                   {
                       y = startY;
                       tilePosition = new Vector3Int (x, y, 0);

                       while (groundTileMap.GetTile(tilePosition) != null)
                       {
                           y++;
                           tilePosition.y++;
                       }
                       if (y > yMax)
                       {
                           yMax = y;
                       }
                   }
                   // nastavimo desni zgornji kot kvadrata
                   moveArea.xMax = xEndPos;
                   moveArea.yMax = yMax+1;
                   moveArea.zMax = 1;

                   TileBase[] movingTiles = groundTileMap.GetTilesBlock(moveArea);
                   moveArea.yMin -= 1;
                   moveArea.yMax -= 1;

                   groundTileMap.SetTilesBlock(moveArea, movingTiles);

                   y = yMax;
                   x = xEndPos;
               }
           }
       }
       //groundTileMapGameObcjet.GetComponent<CompositeCollider2D>().GenerateGeometry();
       yield return null;
   }
   // na novo zgenerira colider
   //groundTileMapGameObcjet.GetComponent<CompositeCollider2D>().GenerateGeometry();

   // pošlje map info vsem tankom
   SendMapInfoToTanks();
}
*/
    #endregion

    #endregion

    #region DestroyGround
    
    // samo kliče koroutino in poda iste informacije naprej ...
    public void StartDestroyAroundPointOnMap(int xHitPoint, int yHitPoint, int _radius)
    {
        // ne vem zakaj ampak če sm dau public IEnumerator ni delal tko kt bi mogl ... 
        StartCoroutine(DestroyAroundPointOnMap(xHitPoint, yHitPoint, _radius));    // samo podam naprej isto kot je bilo podano sem ...
        
        // stara koda ... ; nova je boljša ker je malo animacije + bolj smooth je vse skupaj;

        /*
        // tole je brez animacije
        // za vsak x in y v kvadratu z stranico 2x radius
        for (int x = xHitPoint - radius; x <= xHitPoint + radius; x++)
        {
            for (int y = yHitPoint - radius; y <= yHitPoint + radius; y++)
            {
                // ali sta x in y v mapi
                if (x >= 0 && y > 1 && x < mapWidth && y < mapHeight)
                {
                    // razdalja x in y od začetne točke
                    float xDifference = Mathf.Abs(xHitPoint - x);
                    float yDifference = Mathf.Abs(yHitPoint - y);
                    
                    // naredimo krog namesto kvadrata
                    if (xDifference * xDifference + yDifference * yDifference <= radius * radius)
                    {
                        // če tile ni prazen
                        if (mapGrid[x, y] != 0)
                        {
                            // izprazni tile
                            mapGrid[x, y] = 0;
                            groundTileMap.SetTile(new Vector3Int(x, y, 0), null);
                        }
                    }
                }
            }
        }
        // poglej in dropaj če je kaj v zraku
        StartCoroutine(DropLevitatedTiles());
        */
    }

    bool isMainMissile = false;
    // overload metode za network; main missile mi pove če je ta glavni, in če se bomo po tem orientirali za mapo
    public void StartDestroyAroundPointOnMap(int xHitPoint, int yHitPoint, int _radius, bool _isMainMissile)
    {
        isMainMissile = _isMainMissile;
        // ne vem zakaj ampak če sm dau public IEnumerator ni delal tko kt bi mogl ... 
        StartCoroutine(DestroyAroundPointOnMap(xHitPoint, yHitPoint, _radius));    // samo podam naprej isto kot je bilo podano sem 
    }

    // uniči ground okoli podane točke z podanim radijem
    IEnumerator DestroyAroundPointOnMap(int xHitPoint, int yHitPoint, int _radius)
    {
        // test test
        int radius = 0;

        while (radius <= _radius)
        {
            // za vsak x in y v kvadratu z stranico 2x radius
            for (int x = xHitPoint - radius; x <= xHitPoint + radius; x++)
            {
                for (int y = yHitPoint - radius; y <= yHitPoint + radius; y++)
                {
                    // ali sta x in y v mapi
                    if (x >= 0 && y > 1 && x < mapWidth && y < mapHeight)
                    {
                        // razdalja x in y od začetne točke
                        float xDifference = Mathf.Abs(xHitPoint - x);
                        float yDifference = Mathf.Abs(yHitPoint - y);

                        // naredimo krog namesto kvadrata
                        if (xDifference * xDifference + yDifference * yDifference <= radius * radius)
                        {
                            // če tile ni prazen
                            if (mapGrid[x, y] != 0)
                            {
                                // izprazni tile
                                mapGrid[x, y] = 0;
                                groundTileMap.SetTile(new Vector3Int(x, y, 0), null);
                            }
                        }
                    }
                }
            }

            radius++;

            yield return null;
        }
        // poglej in dropaj če je kaj v zraku
        StartCoroutine(DropLevitatedTiles());
        // end test test
        yield return null;
    }

    #endregion

    #region TankRelatedStuff
    // spawna tank s podanim indexom tanka
    void SpawnTank(int tankIndex)
    {
        // določi pozicijo tanka
        int xStartPosOfTank = (int)((2 * tankIndex + 1) / 4f * mapWidth);
        int yStartPosOfTank = heightArray[xStartPosOfTank];

        // ustvari tank
        GameObject tankInstance = (GameObject)Instantiate(tankPrefab, new Vector3(xStartPosOfTank, yStartPosOfTank, 0), Quaternion.identity);
        // resetira rotacijo
        //tankInstance.transform.rotation = Quaternion.identity;
        // nastavi pozicijo
        //tankInstance.transform.position = new Vector3(xStartPosOfTank, yStartPosOfTank, 0);
        // ustvari ime tanka
        string tankName = "Tank" + tankIndex;
        // preimenujemo tank
        tankInstance.name = tankName;

        // pošljemo tank index tanku
        tankInstance.GetComponent<TankMoveAndAim>().SetTankIndex(tankIndex);
        if (isNetworkGame)
        {
            NetworkServer.Spawn(tankInstance);
        }
    }

    // popravi in pošlje informacijo o spremenjeni mapi vsem tankom
    public void SendMapInfoToTanks()
    {
        // popravi heightArray
        for (int x = 0; x < mapGrid.GetLength(0); x++)
        {
            for (int y = 0; y < mapGrid.GetLength(1); y++)
            {
                if (mapGrid[x, y] == 0)
                {
                    heightArray[x] = y - 1;
                    break;
                }
            }
        }
        

        // poiščemo vse tanke
        GameObject[] tanks = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject tank in tanks)
        {
            TankMoveAndAim tankMoveScript = tank.GetComponent<TankMoveAndAim>();

            // pošljemo vsem tankom novo mapo
            tankMoveScript.mapHightArray = heightArray;
            // rečemo naj pogleda če se rabi tank kaj premakniti
            tankMoveScript.SetCoorectionBool(true);

            if (isNetworkGame)
            {
                tankMoveScript.CorrectTankNetworkPosition();
            }
        }

        // če je network match pošlji info o mapi še drugemu network igralcu
        if (NetworkInfo.isNetworkMatch)
        {
            GameObject[] allNetPlayers = GameObject.FindGameObjectsWithTag("NetworkPlayer");
            foreach (GameObject netPlayer in allNetPlayers)
            {
                netPlayer.GetComponent<NetworkPlayerManager>().SetMissileDestroyedCount(1);

                if (isMainMissile)
                    netPlayer.GetComponent<NetworkPlayerManager>().CheckMap(heightArray);
            }
        }


        //if (!isNetworkGame || isServer)
        if (!isNetworkGame)// || isServer)
        {
            GameObject.Find("GameManager").GetComponent<PlayerManager>().StopWaitingForNextPlayer();
        }
        /*
        else if (isNetworkGame || !isServer)
        {
            GameObject[] allNetPlayers = GameObject.FindGameObjectsWithTag("NetworkPlayer");
            foreach (GameObject netPlayer in allNetPlayers)
            {
                netPlayer.GetComponent<NetworkPlayerManager>().StopWaitingForNextPlayerRemote();
            }
        }
        */
        // zgenerira edgeCollider za ground
        GenerateGroundEdgeCollider();
    }
    #endregion

    public void MapCompatibility(int[] correctHeightArray)
    {
        if (heightArray != correctHeightArray && !isMainMissile)
        {
            for (int x = 0; x < heightArray.Length; x++)
            {
                if (heightArray[x] > correctHeightArray[x])
                {
                    for (int y = heightArray[x]; y > correctHeightArray[x]; y--)
                    {
                        mapGrid[x, y] = 0;
                        Vector3Int tilePosition = new Vector3Int(x, y, 0);
                        groundTileMap.SetTile(tilePosition, null);
                    }
                }
                else if (heightArray[x] < correctHeightArray[x])
                {
                    for (int y = heightArray[x]; y <= correctHeightArray[x]; y++)
                    {
                        mapGrid[x, y] = 5;
                        Vector3Int tilePosition = new Vector3Int(x, y, 0);
                        int arrayIndex = Random.Range(0, 2);
                        groundTileMap.SetTile(tilePosition, grassTiles[arrayIndex]);
                    }
                }
            }

            heightArray = correctHeightArray;

            // poiščemo vse tanke
            GameObject[] tanks = GameObject.FindGameObjectsWithTag("Player");
            foreach (GameObject tank in tanks)
            {
                TankMoveAndAim tankMoveScript = tank.GetComponent<TankMoveAndAim>();

                // pošljemo vsem tankom novo mapo
                tankMoveScript.mapHightArray = heightArray;
                // rečemo naj pogleda če se rabi tank kaj premakniti
                tankMoveScript.SetCoorectionBool(true);

                if (isNetworkGame)
                {
                    tankMoveScript.CorrectTankNetworkPosition();
                }

            }
        }

        // zgenerira edgeCollider za ground
        GenerateGroundEdgeCollider();
        /*
        if (isServer)
        {
            GameObject.Find("GameManager").GetComponent<PlayerManager>().StopWaitingForNextPlayer();
        }
        
        else
        */
        {
            GameObject[] allNetPlayers = GameObject.FindGameObjectsWithTag("NetworkPlayer");
            foreach (GameObject netPlayer in allNetPlayers)
            {
                netPlayer.GetComponent<NetworkPlayerManager>().StopWaitingForNextPlayerRemote();
            }
        }
    }






    /// <summary>
    /// //////////////////////////////////////////////////////////////////////////////////////
    /// </summary>
    // od tu naprej samo za namene testiranja

    private void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            SetUpNewMap();
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            numOfPlayersReady++;
        }

        if (Input.GetKeyDown(KeyCode.A))
        {
            if (hasAuthority)
                print("Got It");
            else
                print("Didnt get It");
        }

        /*
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mouseClickPosition = Camera.main.ScreenToViewportPoint(Input.mousePosition);
            int xPos = (int)(mouseClickPosition.x * mapWidth);
            int yPos = (int)((mouseClickPosition.y * mapHeight) - (30f / 280f * mapHeight * (1-mouseClickPosition.y)));
            StopAllCoroutines();
            DestroyAroundPointOnMap(xPos, yPos, 30);
        }

        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            Vector3 touchClickPosition = Camera.main.ScreenToViewportPoint(Input.GetTouch(0).position);
            int xPos = (int)(touchClickPosition.x * mapWidth);
            int yPos = (int)(touchClickPosition.y * mapHeight);
            StopCoroutine(DropLevitatedTiles());
            DestroyAroundPointOnMap(xPos, yPos, 7);
        }
        */
    }

}
