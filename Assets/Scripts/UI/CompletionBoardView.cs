using UnityEngine;
using UnityEngine.UI;

public sealed class CompletionBoardView : MonoBehaviour
{
    public MazeSessionState sessionState;
    public GameObject completedPanelRoot;
    public GameObject dormantInscriptionRoot;
    public Text elapsedTimeText;
    public XRWorldButtonInteractable replayButton;

    private void OnEnable()
    {
        if (replayButton != null)
            replayButton.Pressed += OnReplayPressed;
    }

    private void Start()
    {
        HideCompletion();
    }

    private void OnDisable()
    {
        if (replayButton != null)
            replayButton.Pressed -= OnReplayPressed;
    }

    public void ShowCompletion(string formattedDuration)
    {
        if (dormantInscriptionRoot != null)
            dormantInscriptionRoot.SetActive(false);

        if (completedPanelRoot != null)
            completedPanelRoot.SetActive(true);

        if (elapsedTimeText != null)
            elapsedTimeText.text = $"Temps de traversée : {formattedDuration}";

        if (replayButton != null)
            replayButton.SetInteractableEnabled(true);
    }

    public void HideCompletion()
    {
        if (completedPanelRoot != null)
            completedPanelRoot.SetActive(false);

        if (dormantInscriptionRoot != null)
            dormantInscriptionRoot.SetActive(true);

        if (replayButton != null)
            replayButton.SetInteractableEnabled(false);
    }

    private void OnReplayPressed()
    {
        if (sessionState != null)
            sessionState.RequestReplay();
    }
}
