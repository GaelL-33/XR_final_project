using System;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public sealed class MazeSessionState : MonoBehaviour
{
    public MazeState mazeState;

    public event Action<ProgressionStage> StageChanged;
    public event Action ReplayRequested;

    public ProgressionStage CurrentStage { get; private set; } = ProgressionStage.IntroGateClosed;
    public bool HasPickedUpLantern { get; private set; }
    public float ElapsedSeconds { get; private set; }
    public bool IsTimerRunning { get; private set; } = true;

    private void OnEnable()
    {
        if (mazeState == null)
            return;

        mazeState.MechanismStateChanged += OnMechanismStateChanged;
        mazeState.LanternDockedCorrectly += OnLanternSolved;
        mazeState.PlayerZoneChanged += OnPlayerZoneChanged;
        mazeState.ExitLeverPulled += OnExitLeverPulled;
        mazeState.DemoCompleted += OnDemoCompleted;
        mazeState.MazeReset += OnMazeReset;
    }

    private void Start()
    {
        EvaluateStage();
    }

    private void OnDisable()
    {
        if (mazeState == null)
            return;

        mazeState.MechanismStateChanged -= OnMechanismStateChanged;
        mazeState.LanternDockedCorrectly -= OnLanternSolved;
        mazeState.PlayerZoneChanged -= OnPlayerZoneChanged;
        mazeState.ExitLeverPulled -= OnExitLeverPulled;
        mazeState.DemoCompleted -= OnDemoCompleted;
        mazeState.MazeReset -= OnMazeReset;
    }

    private void Update()
    {
        if (IsTimerRunning && CurrentStage != ProgressionStage.Completed)
            ElapsedSeconds += Time.deltaTime;
    }

    public void NotifyLanternPickedUp()
    {
        if (HasPickedUpLantern)
            return;

        HasPickedUpLantern = true;
        EvaluateStage();
    }

    public void RequestReplay()
    {
        HasPickedUpLantern = false;
        ElapsedSeconds = 0f;
        IsTimerRunning = true;

        if (mazeState != null)
            mazeState.ResetAllState();

        SetStage(ProgressionStage.IntroGateClosed);
        ReplayRequested?.Invoke();
    }

    public string FormatElapsedTime()
    {
        var totalSeconds = Mathf.Max(0, Mathf.FloorToInt(ElapsedSeconds));
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }

    private void OnMechanismStateChanged(string id, bool lowered)
    {
        EvaluateStage();
    }

    private void OnLanternSolved(string bayId, int nicheIndex)
    {
        HasPickedUpLantern = true;
        EvaluateStage();
    }

    private void OnPlayerZoneChanged(MazeZoneId zone, bool inTravelZone)
    {
        if (zone == MazeZoneId.ExitSanctuary && mazeState != null && mazeState.IsExitLeverPulled && !mazeState.IsDemoCompleted)
            mazeState.MarkDemoCompleted();

        EvaluateStage();
    }

    private void OnExitLeverPulled()
    {
        EvaluateStage();
    }

    private void OnDemoCompleted()
    {
        IsTimerRunning = false;
        EvaluateStage();
    }

    private void OnMazeReset()
    {
        HasPickedUpLantern = false;
        ElapsedSeconds = 0f;
        IsTimerRunning = true;
        EvaluateStage();
    }

    private void EvaluateStage()
    {
        if (mazeState == null)
            return;

        ProgressionStage nextStage;
        if (mazeState.IsDemoCompleted)
        {
            nextStage = ProgressionStage.Completed;
        }
        else if (mazeState.IsExitLeverPulled)
        {
            nextStage = ProgressionStage.EnterExitSanctuary;
        }
        else if (mazeState.CurrentPlayerZone == MazeZoneId.AtriumFarSide)
        {
            nextStage = ProgressionStage.PullExitLever;
        }
        else if (mazeState.IsLanternSolved && mazeState.CurrentPlayerZone == MazeZoneId.AtriumIsland)
        {
            nextStage = ProgressionStage.CrossToAtriumFarSide;
        }
        else if (mazeState.IsLanternSolved)
        {
            nextStage = ProgressionStage.ReachAtriumIsland;
        }
        else if (HasPickedUpLantern)
        {
            nextStage = ProgressionStage.DeduceLanternNiche;
        }
        else if (mazeState.IsLowered(MazeMechanismIds.IntroGate) || mazeState.CurrentPlayerZone != MazeZoneId.StartCorridor)
        {
            nextStage = ProgressionStage.FindLantern;
        }
        else
        {
            nextStage = ProgressionStage.IntroGateClosed;
        }

        SetStage(nextStage);
    }

    private void SetStage(ProgressionStage stage)
    {
        if (CurrentStage == stage)
            return;

        CurrentStage = stage;
        StageChanged?.Invoke(stage);
    }
}
