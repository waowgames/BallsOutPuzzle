using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Object = System.Object;

public class TSSceneLoader : MonoBehaviour
{

    private const float EmptyBarWidth = 40f;
    private const float FullBarWidth = 330f;
    private const float BarHeight = 40f;

    [SerializeField] private Image loadingBar;
    
    private void Start()
    {
        SetLoadingBarProgress(0f);
        TinySauce.SubscribeOnInitFinishedEvent(LoadScene);
    }

    private void LoadScene(bool adConsent, bool trackingConsent)
    {
        //SceneManager.LoadScene(1);
        StartCoroutine(LoadSceneAsync());
    }

    IEnumerator LoadSceneAsync()
    {
        yield return null;
        AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(1);

        while (!asyncOperation.isDone)
        {
            SetLoadingBarProgress(asyncOperation.progress);
            yield return null;
        }
    }

    private void SetLoadingBarProgress(float progress)
    {
        loadingBar.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(EmptyBarWidth, FullBarWidth, progress), BarHeight);
    }
}
