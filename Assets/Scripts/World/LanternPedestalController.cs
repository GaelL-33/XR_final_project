using UnityEngine;

public sealed class LanternPedestalController : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public MazeState mazeState;
    public LanternItem lanternItem;
    public XRWorldButtonInteractable recallButton;
    public Renderer cradleRingRenderer;
    public Color waitingColor = new Color(0.95f, 0.72f, 0.3f, 1f);
    public Color solvedColor = new Color(0.45f, 0.9f, 0.65f, 1f);

    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (recallButton != null)
            recallButton.Pressed += OnRecallPressed;
        if (mazeState != null)
        {
            mazeState.LanternDockedCorrectly += OnLanternSolved;
            mazeState.MazeReset += OnMazeReset;
        }
    }

    private void OnDisable()
    {
        if (recallButton != null)
            recallButton.Pressed -= OnRecallPressed;
        if (mazeState != null)
        {
            mazeState.LanternDockedCorrectly -= OnLanternSolved;
            mazeState.MazeReset -= OnMazeReset;
        }
    }

    private void Update()
    {
        if (cradleRingRenderer == null || lanternItem == null)
            return;

        Color emission;
        if (lanternItem.IsLockedInSolvedSocket)
        {
            emission = solvedColor * 0.45f;
        }
        else if (lanternItem.IsRestingOnPedestal)
        {
            emission = waitingColor * (0.45f + 0.15f * Mathf.Sin(Time.time * 2.5f));
        }
        else
        {
            emission = waitingColor * 0.15f;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        cradleRingRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(EmissionColorId, emission);
        cradleRingRenderer.SetPropertyBlock(propertyBlock);
    }

    private void OnRecallPressed()
    {
        if (lanternItem != null && !lanternItem.IsLockedInSolvedSocket)
            lanternItem.RecallToPedestal(true);
    }

    private void OnLanternSolved(string bayId, int nicheIndex)
    {
        if (recallButton != null)
            recallButton.SetInteractableEnabled(false);
    }

    private void OnMazeReset()
    {
        if (recallButton != null)
            recallButton.SetInteractableEnabled(true);
    }
}
