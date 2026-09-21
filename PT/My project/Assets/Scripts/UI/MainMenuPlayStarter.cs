using UnityEngine;

public class MainMenuPlayStarter : MonoBehaviour
{
    public void HandlePlayPressed()
    {
        GameFlowController.Instance?.StartCurrentLevel();
    }
}
