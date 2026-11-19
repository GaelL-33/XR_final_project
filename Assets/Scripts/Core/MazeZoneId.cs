using System;

public enum MazeZoneId
{
    Unknown = 0,
    StartCorridor = 1,
    Vestibule = 2,
    AtriumAntechamber = 3,
    LanternGallery = 4,
    AtriumEntry = 5,
    AtriumThreshold = 6,
    AtriumIsland = 7,
    AtriumFarSide = 8,
    ExitSanctuary = 9,
}

public enum AtriumPartitionPosition
{
    EntryToIsland = 0,
    IslandToExit = 1,
}

public enum ProgressionStage
{
    IntroGateClosed = 0,
    FindLantern = 1,
    DeduceLanternNiche = 2,
    ReachAtriumIsland = 3,
    CrossToAtriumFarSide = 4,
    PullExitLever = 5,
    EnterExitSanctuary = 6,
    Completed = 7,
}

public enum BayEmblemKind
{
    Crescent = 0,
    TwinPillar = 1,
    Star = 2,
    Hourglass = 3,
    Arch = 4,
    Compass = 5,
}

public static class MazeMechanismIds
{
    public const string IntroGate = "center_gate";
    public const string AtriumLock = "atrium_lock";
    public const string ExitGate = "exit_gate";
    public const string AtriumPartition = "atrium_partition";
    public const string HintOrb = "hint_orb";
}
