using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class LoadingFirstLevel : MonoBehaviour
{
    [SerializeField] private int targetSceneIndex = 1;
    [SerializeField] private float waitBeforeLoading = 2f;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Slider loadingBar;

    private void Start()
    {
        if (levelText != null)
            levelText.text = "Level 1";
        if (loadingBar != null)
        {
            loadingBar.maxValue = waitBeforeLoading;
            loadingBar.value = 0f;
        }
        StartCoroutine(LoadAndAnimate());
    }

    private System.Collections.IEnumerator LoadAndAnimate()
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(targetSceneIndex);
        op.allowSceneActivation = false;

        var timer = 0f;
        while (timer <= waitBeforeLoading)
        {
            if (loadingBar != null)
                loadingBar.value = timer;
            timer += Time.deltaTime;
            yield return null;
        }

        while (op.progress < 0.9f)
            yield return null;

        op.allowSceneActivation = true;
        yield return new WaitForSeconds(1f);
        gameObject.SetActive(false);
    }
}
