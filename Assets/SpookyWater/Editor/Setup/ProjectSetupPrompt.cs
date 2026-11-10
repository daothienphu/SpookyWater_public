using UnityEditor;
using UnityEngine;

namespace SpookyWater.Editor
{
    [InitializeOnLoad]
    internal static class ProjectSetupPrompt
    {
        private const string PromptedKeyPrefix = "SpookyWater.ProjectSetup.Prompted.";

        static ProjectSetupPrompt()
        {
            if (Application.isBatchMode) return;
            EditorApplication.update += WaitForEditor;
        }

        private static string PromptedKey => PromptedKeyPrefix + Application.dataPath;

        private static void WaitForEditor()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= WaitForEditor;
            if (!EditorPrefs.GetBool(ProjectSetupWindow.AutoOpenKey, true)) return;
            if (EditorPrefs.GetBool(PromptedKey, false)) return;
            if (ProjectSetup.IsComplete()) return;
            EditorPrefs.SetBool(PromptedKey, true);
            Debug.Log(SetupUtility.Prefix + "Some project settings SpookyWater needs are missing, so the setup window was opened once. Reopen it any time from " + ProjectSetup.MenuPath + ".\n" + ProjectSetup.Summarize());
            ProjectSetup.Open();
        }
    }
}
