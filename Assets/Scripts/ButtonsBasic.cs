using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ButtonsBasic : MonoBehaviour {

    public void GoToMainMenu ()
    {
        SceneManager.LoadScene("MainMenu");
    }

	public void MainMenu_Play ()
    {
        SceneManager.LoadScene("GamePlay");
    }

    public void MainMenu_GoToNetworkMenu()
    {
        NetworkInfo.isNetworkMatch = true;
        SceneManager.LoadScene("MenuNetwork");
    }

    public void MainMenu_ExitGame ()
    {
        Application.Quit();
    }
}
