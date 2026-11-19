using System.Collections.Generic;
using UnityEngine;

public enum MazeSoundId
{
    StoneGateMove = 0,
    MiniatureClick = 1,
    LanternPickup = 2,
    LanternWrongNiche = 3,
    LanternSolvedChime = 4,
    PartitionSlide = 5,
    PartitionDetent = 6,
    BlockedAction = 7,
    LeverPull = 8,
    RouteBeaconTone = 9,
    CompletionChord = 10,
    HintChime = 11,
    LanternRecall = 12,
}

public sealed class ProceduralMazeAudio : MonoBehaviour
{
    private const int SampleRate = 22050;
    private const int VoicePoolSize = 8;

    [Range(0f, 1f)] public float masterVolume = 0.85f;
    [Range(0f, 0.25f)] public float ambientVolume = 0.06f;
    public bool playCourtyardAmbience = true;

    private readonly Dictionary<MazeSoundId, AudioClip> clips = new Dictionary<MazeSoundId, AudioClip>();
    private AudioSource[] voicePool;
    private AudioSource ambientSource;
    private int nextVoiceIndex;

    private void Awake()
    {
        BuildClipLibrary();
        CreateVoicePool();

        if (playCourtyardAmbience)
            StartAmbience();
    }

    public void PlayOneShot3D(MazeSoundId soundId, Vector3 worldPosition, float volume = 1f, float pitch = 1f)
    {
        if (voicePool == null || voicePool.Length == 0)
            return;

        if (!clips.TryGetValue(soundId, out var clip) || clip == null)
            return;

        var source = voicePool[nextVoiceIndex];
        nextVoiceIndex = (nextVoiceIndex + 1) % voicePool.Length;

        source.transform.position = worldPosition;
        source.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
        source.volume = Mathf.Clamp01(volume * masterVolume);
        source.clip = clip;
        source.Play();
    }

    public void PlayRouteNote3D(Vector3 worldPosition, int stepIndex, float volume = 0.7f)
    {
        // Pentatonic ascending interval ratios: C, D, E, G, A, C2, D2, E2
        float[] ratios = { 1f, 1.122f, 1.26f, 1.498f, 1.682f, 2f, 2.245f, 2.52f };
        var clampedIndex = Mathf.Clamp(stepIndex, 0, ratios.Length - 1);
        PlayOneShot3D(MazeSoundId.RouteBeaconTone, worldPosition, volume, ratios[clampedIndex]);
    }

    private void CreateVoicePool()
    {
        voicePool = new AudioSource[VoicePoolSize];
        for (var i = 0; i < VoicePoolSize; i++)
        {
            var voiceObj = new GameObject($"SpatialVoice_{i}");
            voiceObj.transform.SetParent(transform, false);
            var src = voiceObj.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 1.6f;
            src.maxDistance = 24f;
            src.dopplerLevel = 0f;
            voicePool[i] = src;
        }
    }

    private void StartAmbience()
    {
        var ambObj = new GameObject("CourtyardAmbience");
        ambObj.transform.SetParent(transform, false);
        ambientSource = ambObj.AddComponent<AudioSource>();
        ambientSource.playOnAwake = false;
        ambientSource.loop = true;
        ambientSource.spatialBlend = 0f;
        ambientSource.volume = ambientVolume * masterVolume;
        ambientSource.clip = SynthesizeNightBreeze(3.5f);
        ambientSource.Play();
    }

    private void BuildClipLibrary()
    {
        clips[MazeSoundId.StoneGateMove] = SynthesizeStoneRumble(0.85f, 58f, 92f);
        clips[MazeSoundId.MiniatureClick] = SynthesizeClick(0.08f, 880f, 1320f);
        clips[MazeSoundId.LanternPickup] = SynthesizeBellChord(0.55f, new[] { 523.25f, 659.25f, 783.99f }, 0.35f);
        clips[MazeSoundId.LanternWrongNiche] = SynthesizeMuffledThud(0.26f, 135f);
        clips[MazeSoundId.LanternSolvedChime] = SynthesizeBellChord(1.65f, new[] { 392f, 493.88f, 587.33f, 783.99f, 987.77f }, 0.55f);
        clips[MazeSoundId.PartitionSlide] = SynthesizeBrassSlide(0.75f);
        clips[MazeSoundId.PartitionDetent] = SynthesizeClick(0.11f, 520f, 780f);
        clips[MazeSoundId.BlockedAction] = SynthesizeDoubleTick(0.24f, 240f);
        clips[MazeSoundId.LeverPull] = SynthesizeLeverClank(0.65f);
        clips[MazeSoundId.RouteBeaconTone] = SynthesizeBellChord(0.8f, new[] { 329.63f, 659.25f }, 0.42f);
        clips[MazeSoundId.CompletionChord] = SynthesizeBellChord(2.6f, new[] { 261.63f, 329.63f, 392f, 523.25f, 659.25f, 783.99f }, 0.6f);
        clips[MazeSoundId.HintChime] = SynthesizeBellChord(0.65f, new[] { 587.33f, 880f }, 0.3f);
        clips[MazeSoundId.LanternRecall] = SynthesizeBellChord(0.5f, new[] { 440f, 659.25f, 880f }, 0.32f);
    }

    private static AudioClip CreateClip(string clipName, float[] samples)
    {
        var clip = AudioClip.Create(clipName, samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip SynthesizeStoneRumble(float duration, float fLow, float fHigh)
    {
        var count = Mathf.Max(1, Mathf.RoundToInt(duration * SampleRate));
        var data = new float[count];
        var rng = new System.Random(101);
        var smoothNoise = 0f;

        for (var i = 0; i < count; i++)
        {
            var t = i / (float)SampleRate;
            var n = i / (float)count;
            var env = Mathf.Sin(Mathf.PI * n) * Mathf.Clamp01(1f - n * 0.25f);
            var white = (float)(rng.NextDouble() * 2.0 - 1.0);
            smoothNoise = smoothNoise * 0.92f + white * 0.08f;

            var freq = Mathf.Lerp(fHigh, fLow, n);
            var tone = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.38f +
                       Mathf.Sin(2f * Mathf.PI * (freq * 0.5f) * t) * 0.25f;

            data[i] = Mathf.Clamp((tone + smoothNoise * 1.4f) * env * 0.65f, -0.95f, 0.95f);
        }

        return CreateClip("StoneGateMove", data);
    }

    private static AudioClip SynthesizeBrassSlide(float duration)
    {
        var count = Mathf.Max(1, Mathf.RoundToInt(duration * SampleRate));
        var data = new float[count];
        var rng = new System.Random(202);
        var band = 0f;
        var prev = 0f;

        for (var i = 0; i < count; i++)
        {
            var t = i / (float)SampleRate;
            var n = i / (float)count;
            var env = Mathf.Sin(Mathf.PI * n);
            var white = (float)(rng.NextDouble() * 2.0 - 1.0);
            band = band * 0.85f + (white - prev) * 0.15f;
            prev = white;

            var metal = Mathf.Sin(2f * Mathf.PI * 196f * t) * 0.22f +
                        Mathf.Sin(2f * Mathf.PI * 311f * t) * 0.14f;
            var endClunk = n > 0.86f
                ? Mathf.Sin(2f * Mathf.PI * 120f * (t - duration * 0.86f)) * Mathf.Exp(-(n - 0.86f) * 28f) * 0.6f
                : 0f;

            data[i] = Mathf.Clamp(((metal + band * 1.1f) * env * 0.55f) + endClunk, -0.95f, 0.95f);
        }

        return CreateClip("PartitionSlide", data);
    }

    private static AudioClip SynthesizeClick(float duration, float f1, float f2)
    {
        var count = Mathf.Max(1, Mathf.RoundToInt(duration * SampleRate));
        var data = new float[count];
        for (var i = 0; i < count; i++)
        {
            var t = i / (float)SampleRate;
            var env = Mathf.Exp(-t * 48f);
            var wave = Mathf.Sin(2f * Mathf.PI * f1 * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * f2 * t) * 0.4f;
            data[i] = wave * env * 0.5f;
        }

        return CreateClip("Click", data);
    }

    private static AudioClip SynthesizeMuffledThud(float duration, float baseFreq)
    {
        var count = Mathf.Max(1, Mathf.RoundToInt(duration * SampleRate));
        var data = new float[count];
        for (var i = 0; i < count; i++)
        {
            var t = i / (float)SampleRate;
            var n = i / (float)count;
            var env = Mathf.Exp(-t * 18f);
            var freq = Mathf.Lerp(baseFreq * 1.15f, baseFreq * 0.65f, n);
            var s = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.7f + Mathf.Sin(2f * Mathf.PI * (freq * 1.41f) * t) * 0.2f;
            data[i] = s * env * 0.55f;
        }

        return CreateClip("WrongNiche", data);
    }

    private static AudioClip SynthesizeDoubleTick(float duration, float freq)
    {
        var count = Mathf.Max(1, Mathf.RoundToInt(duration * SampleRate));
        var data = new float[count];
        for (var i = 0; i < count; i++)
        {
            var t = i / (float)SampleRate;
            var env1 = Mathf.Exp(-t * 36f);
            var t2 = t - 0.11f;
            var env2 = t2 >= 0f ? Mathf.Exp(-t2 * 36f) : 0f;
            var w1 = Mathf.Sin(2f * Mathf.PI * freq * t) * env1;
            var w2 = t2 >= 0f ? Mathf.Sin(2f * Mathf.PI * (freq * 0.85f) * t2) * env2 : 0f;
            data[i] = (w1 + w2) * 0.45f;
        }

        return CreateClip("BlockedAction", data);
    }

    private static AudioClip SynthesizeLeverClank(float duration)
    {
        var count = Mathf.Max(1, Mathf.RoundToInt(duration * SampleRate));
        var data = new float[count];
        for (var i = 0; i < count; i++)
        {
            var t = i / (float)SampleRate;
            var env = Mathf.Exp(-t * 7.5f);
            var strike = Mathf.Exp(-t * 35f) * Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.6f;
            var ring = (Mathf.Sin(2f * Mathf.PI * 392f * t) * 0.35f +
                        Mathf.Sin(2f * Mathf.PI * 587.33f * t) * 0.25f) * env;
            data[i] = Mathf.Clamp(strike + ring * 0.65f, -0.95f, 0.95f);
        }

        return CreateClip("LeverPull", data);
    }

    private static AudioClip SynthesizeBellChord(float duration, float[] frequencies, float gain)
    {
        var count = Mathf.Max(1, Mathf.RoundToInt(duration * SampleRate));
        var data = new float[count];
        var invCount = 1f / Mathf.Max(1, frequencies.Length);

        for (var i = 0; i < count; i++)
        {
            var t = i / (float)SampleRate;
            var attack = Mathf.Clamp01(t / 0.015f);
            var decay = Mathf.Exp(-t * (2.6f / Mathf.Max(0.4f, duration)));
            var sum = 0f;

            for (var f = 0; f < frequencies.Length; f++)
            {
                var delay = f * 0.045f;
                if (t < delay)
                    continue;

                var lt = t - delay;
                var localEnv = Mathf.Clamp01(lt / 0.012f) * Mathf.Exp(-lt * (2.8f / Mathf.Max(0.4f, duration)));
                var freq = frequencies[f];
                sum += (Mathf.Sin(2f * Mathf.PI * freq * lt) + 0.25f * Mathf.Sin(2f * Mathf.PI * freq * 2f * lt)) * localEnv;
            }

            data[i] = Mathf.Clamp(sum * invCount * attack * decay * gain * 1.4f, -0.95f, 0.95f);
        }

        return CreateClip("BellChord", data);
    }

    private static AudioClip SynthesizeNightBreeze(float duration)
    {
        var count = Mathf.Max(1, Mathf.RoundToInt(duration * SampleRate));
        var data = new float[count];
        var rng = new System.Random(303);
        var low = 0f;

        for (var i = 0; i < count; i++)
        {
            var n = i / (float)count;
            var white = (float)(rng.NextDouble() * 2.0 - 1.0);
            low = low * 0.985f + white * 0.015f;
            var swell = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * n);
            var pad = Mathf.Sin(2f * Mathf.PI * 110f * (i / (float)SampleRate)) * 0.08f;
            data[i] = Mathf.Clamp((low * 2.2f + pad) * swell, -0.5f, 0.5f);
        }

        // Crossfade ends for seamless loop
        var fadeSamples = Mathf.Min(count / 8, 2048);
        for (var i = 0; i < fadeSamples; i++)
        {
            var t = i / (float)fadeSamples;
            data[count - fadeSamples + i] = Mathf.Lerp(data[count - fadeSamples + i], data[i], t);
        }

        return CreateClip("NightBreeze", data);
    }
}
