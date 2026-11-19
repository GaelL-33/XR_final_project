using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

public sealed class XRHapticsService : MonoBehaviour
{
    public Transform playerRigRoot;
    public HapticImpulsePlayer leftControllerHaptics;
    public HapticImpulsePlayer rightControllerHaptics;

    private void Awake()
    {
        EnsureControllerBindings();
    }

    public void SendRight(float amplitude, float duration)
    {
        EnsureControllerBindings();
        if (rightControllerHaptics != null)
            rightControllerHaptics.SendHapticImpulse(Mathf.Clamp01(amplitude), Mathf.Max(0.01f, duration));
    }

    public void SendLeft(float amplitude, float duration)
    {
        EnsureControllerBindings();
        if (leftControllerHaptics != null)
            leftControllerHaptics.SendHapticImpulse(Mathf.Clamp01(amplitude), Mathf.Max(0.01f, duration));
    }

    public void SendBoth(float amplitude, float duration)
    {
        SendLeft(amplitude, duration);
        SendRight(amplitude, duration);
    }

    public void PlayMiniatureHover() => SendRight(0.12f, 0.04f);
    public void PlayMiniatureClick() => SendRight(0.32f, 0.07f);
    public void PlayDetentTick() => SendBoth(0.28f, 0.06f);
    public void PlayMechanismRumble() => SendBoth(0.22f, 0.18f);
    public void PlayLanternChime() => SendBoth(0.45f, 0.18f);
    public void PlayRejectionPulse() => SendRight(0.35f, 0.12f);
    public void PlayVictoryPulse() => SendBoth(0.55f, 0.25f);

    private void EnsureControllerBindings()
    {
        if (leftControllerHaptics != null && rightControllerHaptics != null)
            return;

        if (playerRigRoot == null)
            return;

        foreach (var player in playerRigRoot.GetComponentsInChildren<HapticImpulsePlayer>(true))
        {
            if (player == null)
                continue;

            if (leftControllerHaptics == null && player.name.Contains("Left"))
                leftControllerHaptics = player;
            else if (rightControllerHaptics == null && player.name.Contains("Right"))
                rightControllerHaptics = player;
        }
    }
}
