using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityInvader
{
    public sealed class Bootstrap : MonoBehaviour
    {
        private bool _loadStarted;

        private void Start()
        {
            if (_loadStarted)
            {
                return;
            }
            _loadStarted = true;
            LoadArcadeAsync().Forget(Debug.LogException);
        }

        private static async UniTask LoadArcadeAsync()
        {
            // Single loading destroys Bootstrap normally. Do not cancel its completion wait on destroy.
            await SceneManager.LoadSceneAsync("Arcade", LoadSceneMode.Single).ToUniTask();
        }
    }
}
