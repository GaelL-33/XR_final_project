using UnityEngine;
using UnityEngine.UI;

public sealed class HintConsoleView : MonoBehaviour
{
    public GameObject panelRoot;
    public Text headerText;
    public Text messageText;
    public Transform lookAtCamera;

    private float remainingSeconds;

    private void Start()
    {
        HideImmediate();
    }

    private void Update()
    {
        if (remainingSeconds > 0f)
        {
            remainingSeconds -= Time.deltaTime;
            if (remainingSeconds <= 0f)
                HideImmediate();
        }

        if (panelRoot != null && panelRoot.activeSelf && lookAtCamera != null)
        {
            var toCamera = lookAtCamera.position - panelRoot.transform.position;
            if (toCamera.sqrMagnitude > 0.001f)
                panelRoot.transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
        }
    }

    public void ShowMessage(string header, string message, float durationSeconds)
    {
        if (headerText != null)
        {
            headerText.text = header ?? string.Empty;
            headerText.gameObject.SetActive(!string.IsNullOrEmpty(header));
        }

        if (messageText != null)
            messageText.text = message ?? string.Empty;

        remainingSeconds = Mathf.Max(0.5f, durationSeconds);
        if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    public void HideImmediate()
    {
        remainingSeconds = 0f;
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }
}
