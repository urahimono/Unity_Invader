using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityInvader
{
    public sealed class Bootstrap : MonoBehaviour
    {
        private void Start()
        {
            SceneManager.LoadScene("Arcade", LoadSceneMode.Single);
        }
    }
}
