namespace SlapMania.Presentation
{
    /// Game states the camera reacts to (M16 §5). Each can have a per-state transition
    /// (`StateShot`) and/or a bound recorded `CameraSequence` on `CameraDirector`.
    /// Serialized by value: append only.
    public enum CameraCue : byte
    {
        BoutIntro,
        TurnStart,
        Impact,
        LethalImpact,
        Finisher,
        KO,
        BoutEnd,
        ChargeStart,
        ChargeEnd,
        TimingStart,
        TimingEnd,
        Windup,
        TurnEnd
    }
}
