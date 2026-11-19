using UnityEngine;

public sealed class EscalatingHintController : MonoBehaviour
{
    public MazeState mazeState;
    public MazeSessionState sessionState;
    public XRControllerMiniatureInteraction miniatureInteraction;
    public MiniatureLanternView miniatureLanternView;
    public LanternItem lanternItem;
    public HintConsoleView hintConsoleView;
    public ProceduralMazeAudio mazeAudio;
    public XRHapticsService haptics;

    public int CurrentHintTier { get; private set; }

    private void OnEnable()
    {
        if (sessionState != null)
            sessionState.StageChanged += OnStageChanged;

        if (miniatureInteraction != null)
            miniatureInteraction.HintButtonPressed += RequestHint;

        if (mazeState != null)
        {
            mazeState.LanternDockedWrong += OnLanternWrong;
            mazeState.PartitionMoveBlocked += OnPartitionBlocked;
            mazeState.MazeReset += OnMazeReset;
        }

        if (lanternItem != null)
            lanternItem.RecalledToPedestal += OnLanternRecalled;
    }

    private void OnDisable()
    {
        if (sessionState != null)
            sessionState.StageChanged -= OnStageChanged;

        if (miniatureInteraction != null)
            miniatureInteraction.HintButtonPressed -= RequestHint;

        if (mazeState != null)
        {
            mazeState.LanternDockedWrong -= OnLanternWrong;
            mazeState.PartitionMoveBlocked -= OnPartitionBlocked;
            mazeState.MazeReset -= OnMazeReset;
        }

        if (lanternItem != null)
            lanternItem.RecalledToPedestal -= OnLanternRecalled;
    }

    public void RequestHint()
    {
        var stage = sessionState != null ? sessionState.CurrentStage : ProgressionStage.IntroGateClosed;
        if (stage == ProgressionStage.Completed)
        {
            ShowWhisper("Traversée accomplie — appuyez sur Recommencer pour rejouer.", 4.5f);
            return;
        }

        CurrentHintTier = (CurrentHintTier % 3) + 1;

        var hintText = GetHintText(stage, CurrentHintTier, out var landmarkId);
        var header = $"Indice {CurrentHintTier}/3";

        if (hintConsoleView != null)
            hintConsoleView.ShowMessage(header, hintText, 8.0f);

        if (CurrentHintTier >= 2 && miniatureLanternView != null && !string.IsNullOrEmpty(landmarkId))
            miniatureLanternView.PulseLandmark(landmarkId, 4.2f);

        var soundPos = hintConsoleView != null ? hintConsoleView.transform.position : transform.position;
        if (mazeAudio != null)
            mazeAudio.PlayOneShot3D(MazeSoundId.HintChime, soundPos, 0.55f, 0.95f + CurrentHintTier * 0.08f);
        if (haptics != null)
            haptics.PlayMiniatureHover();
    }

    public void ShowWhisper(string text, float durationSeconds = 3.0f)
    {
        if (hintConsoleView != null && !string.IsNullOrEmpty(text))
            hintConsoleView.ShowMessage(string.Empty, text, durationSeconds);
    }

    private void OnStageChanged(ProgressionStage newStage)
    {
        CurrentHintTier = 0;
        if (hintConsoleView != null)
            hintConsoleView.HideImmediate();
    }

    private void OnLanternWrong(string bayId, int nicheIndex)
    {
        ShowWhisper("Cette niche reste silencieuse.", 2.6f);
    }

    private void OnPartitionBlocked(string reasonFr)
    {
        ShowWhisper(reasonFr, 3.0f);
    }

    private void OnLanternRecalled(LanternItem item)
    {
        ShowWhisper("La lanterne a rejoint son socle.", 2.8f);
    }

    private void OnMazeReset()
    {
        CurrentHintTier = 0;
        if (hintConsoleView != null)
            hintConsoleView.HideImmediate();
    }

    private string GetHintText(ProgressionStage stage, int tier, out string landmarkId)
    {
        landmarkId = string.Empty;
        if (mazeState != null && mazeState.Definition != null && mazeState.Definition.TryGetHint(stage, out var authored))
        {
            landmarkId = authored.highlightLandmarkId ?? string.Empty;
            var authoredText = tier == 1 ? authored.tier1Fr : tier == 2 ? authored.tier2Fr : authored.tier3Fr;
            if (!string.IsNullOrWhiteSpace(authoredText))
                return authoredText;
        }

        // Concise French fallbacks for every progression stage
        switch (stage)
        {
            case ProgressionStage.IntroGateClosed:
                landmarkId = MazeMechanismIds.IntroGate;
                return tier == 1
                    ? "Retrouvez le losange gravé devant vous sur la maquette."
                    : tier == 2
                        ? "Pointez la porte miniature avec le rayon droit."
                        : "Visez la porte sur la maquette puis appuyez sur B.";

            case ProgressionStage.FindLantern:
                landmarkId = "lantern_pedestal";
                return tier == 1
                    ? "Une lanterne repose sur son socle dans le vestibule."
                    : tier == 2
                        ? "Saisissez la lanterne pour explorer la galerie des niches."
                        : "Approchez la main droite de la lanterne et pressez Grip.";

            case ProgressionStage.DeduceLanternNiche:
                landmarkId = "bay_5";
                return tier == 1
                    ? "Sur la maquette, suivez le rail de l'atrium jusqu'au mur d'une baie."
                    : tier == 2
                        ? "Le rail rejoint le dos de la baie V (Arche), au nord-est."
                        : "Approchez la lanterne de la baie V : la veine dorée mène à la niche en haut à droite.";

            case ProgressionStage.ReachAtriumIsland:
                landmarkId = MazeMechanismIds.AtriumPartition;
                return tier == 1
                    ? "La cloison de l'atrium possède maintenant une poignée sur la maquette."
                    : tier == 2
                        ? "Ouvrez le passage côté Entrée pour atteindre l'îlot central."
                        : "Glissez la poignée vers le nord (ou visez + B), puis téléportez-vous sur l'îlot.";

            case ProgressionStage.CrossToAtriumFarSide:
                landmarkId = MazeMechanismIds.AtriumPartition;
                return tier == 1
                    ? "Vous êtes en sécurité sur l'îlot : regardez votre marqueur."
                    : tier == 2
                        ? "Faites glisser la cloison de l'autre côté pour ouvrir la sortie."
                        : "Glissez la poignée vers le sud (ou visez + B), puis traversez vers le levier.";

            case ProgressionStage.PullExitLever:
                landmarkId = "exit_lever";
                return tier == 1
                    ? "Un levier grandeur nature vous attend au sud de cette plateforme."
                    : tier == 2
                        ? "Abaissez le levier pour ouvrir la Porte de l'Aube vue au départ."
                        : "Saisissez ou activez le levier devant l'arche dorée.";

            case ProgressionStage.EnterExitSanctuary:
                landmarkId = MazeMechanismIds.ExitGate;
                return tier == 1
                    ? "La Porte de l'Aube est ouverte devant vous."
                    : tier == 2
                        ? "Suivez la lumière dorée dans le sanctuaire de sortie."
                        : "Téléportez-vous sous l'arche dorée pour achever la traversée.";

            default:
                return "Observez la maquette dans votre main gauche.";
        }
    }
}
