using UnityEngine;

public class VersionDisplay : MonoBehaviour
{
    [SerializeField] private TMPro.TextMeshProUGUI versionText;
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        string version = Application.version;
        if (versionText != null)
        {
            versionText.text = $"Version: {version}";
        }
    }
}
