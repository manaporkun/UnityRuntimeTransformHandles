using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex == 0 ? 1 : 0);
        }
    }
}
