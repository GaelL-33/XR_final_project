using UnityEngine;
using UnityEngine.UI;

public sealed class DiegeticTextPlaque : MonoBehaviour
{
    public MazeSessionState sessionState;
    public Text titleText;
    public Text bodyText;
    public CanvasGroup canvasGroup;

    [Header("Optional Stage Filtering")]
    public bool filterByProgressionStage;
    public ProgressionStage minVisibleStage = ProgressionStage.IntroGateClosed;
    public ProgressionStage maxVisibleStage = ProgressionStage.Completed;

    private void OnEnable()
    {
        if (sessionState != null)
            sessionState.StageChanged += OnStageChanged;
    }

    private void Start()
    {
        if (filterByProgressionStage && sessionState != null)
            OnStageChanged(sessionState.CurrentStage);
    }

    private void OnDisable()
    {
        if (sessionState != null)
            sessionState.StageChanged -= OnStageChanged;
    }

    public void SetText(string title, string body)
    {
        if (titleText != null)
        {
            titleText.text = title ?? string.Empty;
            titleText.gameObject.SetActive(!string.IsNullOrEmpty(title));
        }

        if (bodyText != null)
            bodyText.text = body ?? string.Empty;
    }

    public void SetVisible(bool visible)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = visible ? 1f : 0f;
        else
            gameObject.SetActive(visible);
    }

    private void OnStageChanged(ProgressionStage stage)
    {
        if (!filterByProgressionStage)
            return;

        var visible = stage >= minVisibleStage && stage <= maxVisibleStage;
        SetVisible(visible);
    }
}
