using UnityEngine.SceneManagement;

public static class SceneManagerH
{
    public static void LoadNextScene()
    {
        var activeScene = SceneManager.GetActiveScene();
        var activeSceneBuildIndex = activeScene.buildIndex;
        var nextScene = activeSceneBuildIndex + 1;
        SceneManager.LoadScene(nextScene);
    }
}