using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

public class TankMoveAndAim : NetworkBehaviour
{
    #region Variables
    // dostop do MapManager skripte
    MapManager MapManagerScript;

    // map properties for calculations
    [HideInInspector]
    public int[] mapHightArray;

    /// Lastnosti tanka
    ///
    // tank index da lahko primerjamo z tankSelected;
    int tankIndex;
    // da vidimo kateri tank je izbran; pridobi informacijo iz PlayerManager skripte
    int tankSelected;

    /// premiki tanka
    /// 
    // tank position and rotation properties
    float tankWidth = 3f;    // širina ki se uporabi za izračun rotacije
    int maxTankAngle = 50;   // maximalni dovoljeni nagib tanka
    float targetTankAngle;    // ciljana rotacija tanka; ko spreminjamo rotacijo tanka
    // true če je potreben popravek rotacije ali pozicije (npr če se razbijejo tla pod tankom)
    bool correctionNeeded = true;
    // za preverjanje če so se lastnosti tanka kaj spremenile
    float oldTankPosY;
    float oldTankRotation;

    // Tank rigidbody; uporaba pri premikih tanka
    Rigidbody2D rb;
    // from keyboard input in tudi preračuna iz pritiskov na gumb (moveDirectionIndex); 
    Vector2 moveVelocity;
    // pridobi informacijo iz gumba za premik levo/desno; -1 : move left; 0 : dont move; 1 : move right;
    int moveDirectionIndex = 0;
    // mulitplier for left/right movement speed
    int moveSpeedMultiplier = 15;
    // multiplier for vertical movement speed
    float yPosChangeSpeed = 3f;
    // multiplier for tank rotation speed
    float rotationSpeed = 2f;
    
    /// rotacija cevi
    ///
    // transform od gameobject za rotacijo cevi
    Transform cevRotatePoint;
    // vrednost rotacije cevi
    [Range(-360,360)]
    float cevRotationValue;
    // cev rotation speed
    float cevRotationSpeed = 3f;

    // Aim slider
    Slider aimRotationSlider;
    // Aim input field
    InputField aimRotationInputField;
    
    // da lahko namerimo cev s prtitoskom na mapo kadar je true
    [HideInInspector]
    public bool touchAimActive;
    
    /// Fuel properties
    ///
    // fuel start value
    float startTankFuelValue = 100f;
    // current fuel value
    float tankFuelValue;
    // hitrost porabe goriva
    float fuelConsumSpeed = 6f;

    // fuel slider
    Slider fuelSlider;
    #endregion

    void Start()
    {
        // poišče map manager skripto
        MapManagerScript = GameObject.FindGameObjectWithTag("MapManager").GetComponent<MapManager>();

        // pridobivanje informacij o mapi
        mapHightArray = MapManagerScript.heightArray;

        // finds rigidbody component from tank
        rb = GetComponent<Rigidbody2D>();
        // najde točko rotiranja cevi
        cevRotatePoint = transform.Find("CevRotatePoint");
        // nastavi začetno vrednost cevi; se pravi 30 in 150 stopinj
        cevRotationValue = 30f + 120 * tankIndex;
        cevRotatePoint.rotation = Quaternion.Euler(0, 0, cevRotationValue);

        // poišče slider in inputField za aim rotation(cev rotation)
        aimRotationSlider = GameObject.Find("Slider_CevAngle").GetComponent<Slider>();
        aimRotationInputField = GameObject.Find("InputField_CevAnge").GetComponent<InputField>();

        // nastavi vrednost goriva na polno
        tankFuelValue = startTankFuelValue;
    }

    void Update()
    {
        // samo če ni network match ali če mamo authority ali če je potreben popravek
        if (!NetworkInfo.isNetworkMatch || hasAuthority)
        {
            // se izvaja samo če je izbran ta tank
            if (tankSelected == tankIndex)
            {
                // Za premik levo desno z tipkovnico
                moveVelocity = new Vector2(Input.GetAxisRaw("Horizontal"), 0).normalized * moveSpeedMultiplier;   // za test
                if (moveVelocity.x == 0)    // zato da dela še vedno tudi s tipkami
                {
                    // nastavimo moveVelocity na primerno vrednost; vrednost pridobimo iz pritiska na "move gumbe"
                    moveVelocity = new Vector2(moveDirectionIndex, 0).normalized * moveSpeedMultiplier;
                }

                // if max angle is reached, stop moving in this direction
                if (moveVelocity.x > 0 && targetTankAngle >= maxTankAngle)
                {
                    moveVelocity.x = 0;
                }
                if (moveVelocity.x < 0 && targetTankAngle <= -maxTankAngle)
                {
                    moveVelocity.x = 0;
                }

                // if map edge is reached, stop moving in this direction
                if (rb.position.x < 4 && moveVelocity.x < 0)
                {
                    moveVelocity.x = 0;
                }
                if (rb.position.x > mapHightArray.Length - 5 && moveVelocity.x > 0)
                {
                    moveVelocity.x = 0;
                }

                // poraba goriva ko se premikamo
                if (moveVelocity.x != 0 && tankFuelValue > 0)
                {
                    // gorivo se zmanjša s hitrostjo fuelConsumSpeed
                    tankFuelValue -= Time.unscaledDeltaTime * fuelConsumSpeed;
                    // če smo izpraznili gorivo nastavi vrednost na 0; samo zato da ni bugov
                    if (tankFuelValue < 0)
                    {
                        tankFuelValue = 0;
                    }
                    // nastavi fuel slider na primerno vrednost
                    fuelSlider.value = tankFuelValue;
                }
            }

            // se izvaja samo če je izbran ta tank ali če je bila spremenjena mapa (popravi pozicijo tanka če je potrebno)
            if (correctionNeeded || tankSelected == tankIndex)
            {
                // geting info of current tank position
                int tankMapGridPosX = (int)rb.position.x;

                // calculating target angle of the tank
                targetTankAngle = Mathf.Atan((mapHightArray[tankMapGridPosX + (int)tankWidth] - mapHightArray[tankMapGridPosX - (int)tankWidth]) / (tankWidth * 2 + 1)) * 180 / 3.1415f;

                // limiting max shown angle of the tank; this angle is bigger than angle that limits movement
                if (targetTankAngle > maxTankAngle + 10)
                {
                    targetTankAngle = maxTankAngle + 10;
                }
                else if (targetTankAngle < -maxTankAngle - 10)
                {
                    targetTankAngle = -maxTankAngle - 10;
                }

                // rotating tank to proper value
                rb.transform.rotation = Quaternion.RotateTowards(Quaternion.Euler(rb.transform.eulerAngles), Quaternion.Euler(new Vector3(0, 0, targetTankAngle)), rotationSpeed);

                // calculating avarage y position for the tank from map data
                float averageWorldPosY = 0;
                for (int i = -3; i < 4; i++)
                {
                    averageWorldPosY += mapHightArray[tankMapGridPosX + i];
                }
                averageWorldPosY /= 7;

                // zapis trenutne rotacije tanka; svoja spremenljivka ker rabim odštet 360 če je kot večji kot 180
                float currTankAngle = transform.rotation.eulerAngles.z;
                // če je kot večji kot 180 odštejemo 360 stopinj
                if (currTankAngle > 180)
                {
                    currTankAngle -= 360;
                }

                // primerjamo absolutne razlike pozicije in rotacije; če je razlika manjša od 0.001f potem nehaj spreminjati;
                if (Mathf.Abs(transform.position.y - averageWorldPosY) < 0.001f && Mathf.Abs(currTankAngle - targetTankAngle) < 0.001f)
                {
                    // ne rabimo več popravkov rotacije ali pozicije tanka
                    correctionNeeded = false;
                }

                // moving the tank on the y axis
                Vector2 targetWorldPosY = new Vector2(transform.position.x, averageWorldPosY);  // nova pozicija tanka
                rb.transform.position = Vector2.MoveTowards(rb.transform.position, targetWorldPosY, yPosChangeSpeed);   // premaknemo tank na novo pozicijo

                // Aiming by touch
                if (touchAimActive)
                {
                    // Za miško - testiranje na kompu
                    if (Input.GetMouseButton(0))
                    {
                        // pridobljena informacija o poziciji miške
                        Vector3 mousePosition = Camera.main.ScreenToViewportPoint(Input.mousePosition);
                        // dela samo če nismo na spodnjem delu canvasa, kjer so gumbi 
                        if (mousePosition.y > 30f / 280f)
                        {
                            // pozicija miške z koordinatami iz mapGrid
                            float mousePosX = mousePosition.x * MapManagerScript.mapWidth;
                            // če spremenim višino canvasa bom mogu spremenit tud to formulo!
                            float mousePosY = ((mousePosition.y - 30f / 280f) * 249 / (1 - 30f / 280f));    // 30/280 -> (30 je višina spodnjega dela z gumbi, 280 je višina canvasa); 249 -> višina dejanske mape na canvasau;

                            // spremneljivka nove rotacije
                            double newRotation;
                            // izračun nove rotacije; 2x if da so koti take vrednosti kot jih želim
                            if (mousePosX > cevRotatePoint.position.x)
                            {
                                newRotation = Mathf.Atan((mousePosY - cevRotatePoint.position.y) / (mousePosX - cevRotatePoint.position.x)) * 180 / 3.14;
                            }
                            else
                            {
                                newRotation = 180 - Mathf.Atan((mousePosY - cevRotatePoint.position.y) / (cevRotatePoint.position.x - mousePosX)) * 180 / 3.14;
                            }
                            // nastavimo cevRotationValue na novo izračunano vrednost
                            cevRotationValue = (float)newRotation;
                            // spremenimo še slider in InputField
                            AimValueChangeBySliderOrInputField(false, false);
                        }
                    }

                    // za dejanski touch!
                    if (Input.touchCount > 0)
                    {
                        // pridobimo informacijo o poziciji tucha 
                        Touch touch = Input.GetTouch(0);
                        Vector3 touchposition = Camera.main.ScreenToViewportPoint(touch.position);

                        // dela samo če nismo na spodnjem delu canvasa z gumbi 
                        if (touchposition.y > 30f / 280f)
                        {
                            // pozicija dotika z koordinatami iz mapGrid
                            float touchPosX = touchposition.x * MapManagerScript.mapWidth;
                            // če spremenim višino canvasa bom mogu spremenit tud to formulo!
                            float touchPosY = ((touchposition.y - 30f / 280f) * 249 / (1 - 30f / 280f));    // 30/280 -> (30 je višina spodnjega dela z gumbi, 280 je višina canvasa); 249 -> višina dejanske mape na canvasau;

                            // spremneljivka nove rotacije
                            double newRotation;
                            // izračun nove rotacije; 2x if stavek, da so koti take vrednosti kot jih želim
                            if (touchPosX > cevRotatePoint.position.x)
                            {
                                newRotation = Mathf.Atan((touchPosY - cevRotatePoint.position.y) / (touchPosX - cevRotatePoint.position.x)) * 180 / 3.14;
                            }
                            else
                            {
                                newRotation = 180 - Mathf.Atan((touchPosY - cevRotatePoint.position.y) / (cevRotatePoint.position.x - touchPosX)) * 180 / 3.14;
                            }
                            // nastavimo cevRotationValue na novo izračunano vrednost
                            cevRotationValue = (float)newRotation;
                            // spremenimo še slider in InputField
                            AimValueChangeBySliderOrInputField(false, false);
                        }
                    }
                }
            }
        }
    }
    
    private void FixedUpdate()
    {
        // da imam ločeno preverjanje ujemanja rotacij in dejanske vrednosti
        float checkCevRotationValue = cevRotationValue;

        // spremenimo če je kot prevelik ali premajen
        if (checkCevRotationValue > 360)
        {
            checkCevRotationValue -= 360;
        }
        else if (checkCevRotationValue < 0)
        {
            checkCevRotationValue += 360;
        }

        // se izvaja samo če je izbran ta tank; izvaja tudi če je rotacija cevi napačna
        if (tankSelected == tankIndex || Mathf.Abs(cevRotatePoint.rotation.eulerAngles.z - checkCevRotationValue) > 0.001f)
        {
            // se ne moremo premakniti če nimamo več goriva
            if (tankFuelValue > 0)
            {
                // actualy moving tank on the x axis
                rb.MovePosition(rb.position + moveVelocity * Time.fixedDeltaTime);
            }

            // ob premiku nastavi primerno vrednost rotacije cevi
            RotateCev(cevRotationValue);
        }
    }

    // pridobimo informacijo o izbranem tanku
    public void SetSelectedTank (int tankSelecedIndex)
    {
        tankSelected = tankSelecedIndex;
    }

    // nastavimo index tanka na primerno vrednost; potrebno za kasnejše primerjanje izbranega tanka
    public void SetTankIndex (int _tankIndex)
    {
        tankIndex = _tankIndex;
        // pošljem tank index še po drugih skriptah (mogoče bi blo lažje če bi dau samo public int tukaj in dostopal do tega)
        GetComponent<TankHealth>().SetTankIndex(_tankIndex);

        /// tukaj nastavimo še sliderje za gorivo na polne vrednosti;
        ///
        // poišče ime sliderja za gorivo
        string fuelSliderName = "Slider_Fuel_Tank_" + tankIndex.ToString();
        // poiščemo slider za gorivo
        fuelSlider = GameObject.Find(fuelSliderName).GetComponent<Slider>();
        // nastavimo slider na polno vrednost
        fuelSlider.value = startTankFuelValue;
    }

    // nastavimo bool, ki pove ali je potreben popravek y pozicije ali rotacije tanka; true ko se uniči kakšen kos mape
    public void SetCoorectionBool (bool _correctionNeededBool)
    {
        correctionNeeded = _correctionNeededBool;
    }
    
    // zarotira cev na primerno vrednost
    void RotateCev (float newCevRotationValue)
    {
        cevRotatePoint.rotation = Quaternion.RotateTowards(cevRotatePoint.rotation, Quaternion.Euler(0, 0, cevRotationValue), cevRotationSpeed);
    }

    // za spremembo rotacije cevi z gumbi
    public void ChangeCevRotationValue(float changeRotationValue)
    {
        // povečamo za changeRotationValue
        cevRotationValue += changeRotationValue;

        // popravimo še vrednosti na sliderju in InputFieldu za rotacijo cevi
        AimValueChangeBySliderOrInputField(false, false);
    }

    public void AimValueChangeBySliderOrInputField (bool changeBySlider, bool changeByInputField)
    {
        // spremenimo če je kot prevelik ali premajhen
        if (cevRotationValue > 180)
        {
            cevRotationValue -= 360;
        }
        else if (cevRotationValue <= -180)
        {
            cevRotationValue += 360;
        }
        // če je bila spremenjena vrednost sliderja
        if (changeBySlider)
        {
            cevRotationValue = (int)aimRotationSlider.value;
        }
        // če je bila spremenjena vrednost InputFielda
        else if (changeByInputField)
        {
            int tempRotationValue = int.Parse(aimRotationInputField.text);
            if (tempRotationValue > 180)
                tempRotationValue = 180;
            cevRotationValue = tempRotationValue;
        }
        // nastavimo pravilne vrednosti Sliderja in InputFielda
        aimRotationSlider.value = (int)cevRotationValue;
        aimRotationInputField.text = ((int)cevRotationValue).ToString();
    }
    
    // za premik tanka z gumbi
    public void MoveTank (int directionIndex)
    {
        // directionIndex: -1 - left; 0 - no move; 1 - right;
        moveDirectionIndex = directionIndex;
    }

    public Quaternion GetCevRotationValue()
    {
        return cevRotatePoint.rotation;
    }


    public void CorrectTankNetworkPosition()
    {
        StartCoroutine(CorrectTankNetworkPositionCourutine());
    }
    IEnumerator CorrectTankNetworkPositionCourutine()
    {
        if (!hasAuthority)
            yield break;

        print(hasAuthority);
        while (correctionNeeded)
        {
            yield return new WaitForSeconds(0.5f);
        }

        Vector3 correctTankPosition = gameObject.transform.position;
        CmdCorrectTankPosition(correctTankPosition);
    }

    [Command]
    void CmdCorrectTankPosition(Vector3 correctTankPosition)
    {
        gameObject.transform.position = correctTankPosition;

        RpcCorrectClientPosition(correctTankPosition);
    }

    [ClientRpc]
    void RpcCorrectClientPosition(Vector3 correctTankPosition)
    {
        gameObject.transform.position = correctTankPosition;
    }
    
}
