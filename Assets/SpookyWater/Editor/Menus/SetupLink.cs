using UnityEditor;

namespace SpookyWater.Editor
{
    public static class SetupLink
    {
        private const double CacheSeconds = 1.0;

        private static double _checkedAt = double.NegativeInfinity;
        private static string _cachedProblem;

        public static bool TryGetProblem(out string message)
        {
            var now = EditorApplication.timeSinceStartup;
            if (now - _checkedAt > CacheSeconds || now < _checkedAt)
            {
                _checkedAt = now;
                _cachedProblem = ProjectSetup.IsComplete() ? null : "The project setup is incomplete:\n" + ProjectSetup.Summarize();
            }

            message = _cachedProblem;
            return message != null;
        }

        public static void Open()
        {
            ProjectSetup.Open();
        }
    }
}
