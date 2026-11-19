using UnityEditor;
using UnityEditor.SceneManagement;

namespace Nara.Practice.Editor
{
    public static class NaraPracticeValidation
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene(NaraPracticeBuild.ScenePath);
            NaraPracticeBuild.VerifySceneDependencies();
            EditorApplication.EnterPlaymode();
        }
    }
}
