using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using System.Linq;
using System;

public class AudioPlayer : MonoBehaviour
{
  public List<AudioSource> SourcesList = new List<AudioSource>();
  public List<AudioResource> SoundsList = new List<AudioResource>();
  public AudioSource metronome = new AudioSource();

  private Dictionary<string, AudioSource> sources = new Dictionary<string, AudioSource>();
  private Dictionary<string, AudioResource> resources = new Dictionary<string, AudioResource>();

  public float bpm = 90f;
  public float accuracy = 0.0f;

  [Header("Check Window")]
  [Range(2.0f, 6.0f)]
  public float checkFraction = 3.0f;

  [Header("Outlier Detection")]
  [Range(1.0f, 3.0f)]
  public float outlierThreshold = 2.0f;

  private Coroutine metronomeCoroutine;
  public bool IsMetronomeRunning => metronomeCoroutine != null;

  private double nextTickTime;
  private double beatInterval;
  private int beatCount = 0;

  private double currentBarStartTime = 0;
  private List<double> recentBeatTimes = new List<double>();
  private List<HitData> hitsInCurrentBar = new List<HitData>();
  private List<float> barAccuracyScores = new List<float>();

  private const int beatsPerBar = 4;
  private const int maxBarHistory = 16;

  [Header("Beat Weights")]
  [Range(1.0f, 2.0f)]
  public float downbeatWeight = 2f;
  [Range(1.0f, 2.0f)]
  public float beat3Weight = 1.5f;

  [Header("Accuracy Thresholds (% of beat interval)")]
  [Range(0.05f, 0.25f)]
  public float perfectThresholdPercent = 0.12f;
  [Range(0.10f, 0.35f)]
  public float greatThresholdPercent = 0.18f;
  [Range(0.15f, 0.45f)]
  public float excellentThresholdPercent = 0.24f;
  [Range(0.20f, 0.55f)]
  public float goodThresholdPercent = 0.30f;
  [Range(0.25f, 0.65f)]
  public float decentThresholdPercent = 0.36f;

  private float perfectThreshold;
  private float greatThreshold;
  private float excellentThreshold;
  private float goodThreshold;
  private float decentThreshold;
  private float checkWindowMs;

  private struct HitData
  {
    public double hitTime;
    public double targetTime;
    public int beatNumber;
    public float offset;
    public bool isOutlier;
  }

  void Start()
  {
    foreach (AudioSource source in SourcesList)
    {
      sources.Add(source.name.ToLower(), source);
    }

    foreach (AudioResource rsc in SoundsList)
    {
      resources.Add(rsc.name.ToLower(), rsc);
    }
  }

  private void UpdateThresholds()
  {
    float beatDurationMs = (60f / bpm) * 1000f;

    perfectThreshold = beatDurationMs * perfectThresholdPercent;
    greatThreshold = beatDurationMs * greatThresholdPercent;
    excellentThreshold = beatDurationMs * excellentThresholdPercent;
    goodThreshold = beatDurationMs * goodThresholdPercent;
    decentThreshold = beatDurationMs * decentThresholdPercent;
    checkWindowMs = beatDurationMs / checkFraction;
  }

  public void ToggleMetronome()
  {
    if (IsMetronomeRunning)
    {
      StopCoroutine(metronomeCoroutine);
      metronomeCoroutine = null;
      beatCount = 0;
      recentBeatTimes.Clear();
    }
    else
    {
      beatCount = 0;
      hitsInCurrentBar.Clear();
      barAccuracyScores.Clear();
      recentBeatTimes.Clear();
      UpdateThresholds();
      metronomeCoroutine = StartCoroutine(MetronomeRoutine());
    }
  }

  private IEnumerator MetronomeRoutine()
  {
    beatInterval = 60.0 / bpm;
    nextTickTime = AudioSettings.dspTime + beatInterval;
    currentBarStartTime = nextTickTime;

    while (true)
    {
      float newBeatInterval = 60f / bpm;
      if (Mathf.Abs((float)beatInterval - newBeatInterval) > 0.001f)
      {
        beatInterval = newBeatInterval;
        UpdateThresholds();
      }

      double currentTime = AudioSettings.dspTime;

      if (currentTime >= nextTickTime)
      {
        double beatTime = nextTickTime;
        recentBeatTimes.Add(beatTime);

        if (recentBeatTimes.Count > 8)
        {
          recentBeatTimes.RemoveAt(0);
        }

        // Schedule playback at exact DSP time
        metronome.PlayScheduled(beatTime);

        if (beatCount == 0)
        {
          currentBarStartTime = beatTime;
        }

        beatCount++;

        if (beatCount >= beatsPerBar)
        {
          CalculateBarAccuracy();
          beatCount = 0;
          hitsInCurrentBar.Clear();
        }

        nextTickTime += beatInterval;
      }

      yield return null;
    }
  }

  public void PlaySource(string sourceName, float velocity)
  {
    sourceName = sourceName.ToLower();
    if (!sources.ContainsKey(sourceName)) return;

    int velocityClip = Mathf.CeilToInt(Mathf.Clamp01(velocity) * 8);
    string resourceName = $"{sourceName}-{velocityClip}";

    AudioSource source = sources[sourceName];
    source.resource = resources[resourceName];

    double hitTime = AudioSettings.dspTime;

    source.Play();

    if (IsMetronomeRunning)
    {
      RecordHit(hitTime);
    }
  }

  private void RecordHit(double hitTime)
  {
    if (recentBeatTimes.Count == 0) return;

    double checkWindow = beatInterval / checkFraction;
    double closestDistance = double.MaxValue;
    double closestTarget = 0;
    int closestBeatIndex = -1;

    for (int i = 0; i < recentBeatTimes.Count; i++)
    {
      double beatTime = recentBeatTimes[i];
      double distance = Math.Abs(hitTime - beatTime);

      if (distance <= checkWindow && distance < closestDistance)
      {
        closestDistance = distance;
        closestTarget = beatTime;
        closestBeatIndex = i;
      }
    }

    HitData hit;

    if (closestBeatIndex == -1)
    {
      double absoluteClosest = double.MaxValue;
      double absoluteClosestBeat = 0;

      for (int i = 0; i < recentBeatTimes.Count; i++)
      {
        double distance = Math.Abs(hitTime - recentBeatTimes[i]);
        if (distance < absoluteClosest)
        {
          absoluteClosest = distance;
          absoluteClosestBeat = recentBeatTimes[i];
        }
      }

      double timeSinceBarStart = absoluteClosestBeat - currentBarStartTime;
      int beatNumberInBar = (int)Math.Round(timeSinceBarStart / beatInterval) % beatsPerBar;
      if (beatNumberInBar < 0) beatNumberInBar += beatsPerBar;

      hit = new HitData
      {
        hitTime = hitTime,
        targetTime = 0,
        beatNumber = beatNumberInBar,
        offset = float.MaxValue,
        isOutlier = false
      };
    }
    else
    {
      double timeSinceBarStart = closestTarget - currentBarStartTime;
      int beatNumberInBar = (int)Math.Round(timeSinceBarStart / beatInterval) % beatsPerBar;
      if (beatNumberInBar < 0) beatNumberInBar += beatsPerBar;

      hit = new HitData
      {
        hitTime = hitTime,
        targetTime = closestTarget,
        beatNumber = beatNumberInBar,
        offset = (float)(hitTime - closestTarget),
        isOutlier = false
      };
    }

    hitsInCurrentBar.Add(hit);
  }

  private void CalculateBarAccuracy()
  {
    if (hitsInCurrentBar.Count == 0) return;

    // First pass: identify outliers using standard deviation
    List<float> validOffsets = new List<float>();

    foreach (HitData hit in hitsInCurrentBar)
    {
      if (hit.offset != float.MaxValue)
      {
        validOffsets.Add(Mathf.Abs(hit.offset) * 1000f); // Convert to ms
      }
    }

    // Need at least 3 hits to detect outliers
    if (validOffsets.Count >= 3)
    {
      float mean = validOffsets.Average();
      float sumSquaredDiffs = validOffsets.Sum(offset => Mathf.Pow(offset - mean, 2));
      float stdDev = Mathf.Sqrt(sumSquaredDiffs / validOffsets.Count);

      // Mark outliers
      for (int i = 0; i < hitsInCurrentBar.Count; i++)
      {
        HitData hit = hitsInCurrentBar[i];

        if (hit.offset != float.MaxValue)
        {
          float distanceMs = Mathf.Abs(hit.offset) * 1000f;
          float zScore = Mathf.Abs(distanceMs - mean) / (stdDev + 0.001f); // Add small value to avoid division by zero

          if (zScore > outlierThreshold)
          {
            hit.isOutlier = true;
            hitsInCurrentBar[i] = hit;
          }
        }
      }
    }

    // Second pass: calculate accuracy excluding outliers
    float totalWeightedAccuracy = 0f;
    float totalWeight = 0f;
    int excludedCount = 0;

    foreach (HitData hit in hitsInCurrentBar)
    {
      // Skip outliers
      if (hit.isOutlier)
      {
        excludedCount++;
        continue;
      }

      float hitAccuracy;

      if (hit.offset == float.MaxValue)
      {
        hitAccuracy = 0f;
      }
      else
      {
        float distanceMs = Mathf.Abs(hit.offset) * 1000f;

        if (distanceMs <= perfectThreshold)
        {
          float t = distanceMs / perfectThreshold;
          hitAccuracy = Mathf.Lerp(100f, 98f, t);
        }
        else if (distanceMs <= greatThreshold)
        {
          float t = (distanceMs - perfectThreshold) / (greatThreshold - perfectThreshold);
          hitAccuracy = Mathf.Lerp(98f, 92f, t);
        }
        else if (distanceMs <= excellentThreshold)
        {
          float t = (distanceMs - greatThreshold) / (excellentThreshold - greatThreshold);
          hitAccuracy = Mathf.Lerp(92f, 85f, t);
        }
        else if (distanceMs <= goodThreshold)
        {
          float t = (distanceMs - excellentThreshold) / (goodThreshold - excellentThreshold);
          hitAccuracy = Mathf.Lerp(85f, 78f, t);
        }
        else if (distanceMs <= decentThreshold)
        {
          float t = (distanceMs - goodThreshold) / (decentThreshold - goodThreshold);
          hitAccuracy = Mathf.Lerp(78f, 68f, t);
        }
        else
        {
          float beyondDecent = distanceMs - decentThreshold;
          float maxDistance = checkWindowMs - decentThreshold;
          float t = Mathf.Clamp01(beyondDecent / maxDistance);
          hitAccuracy = Mathf.Lerp(68f, 50f, t);
        }
      }

      float weight = 1.0f;
      if (hit.beatNumber == 0)
        weight = downbeatWeight;
      else if (hit.beatNumber == 2)
        weight = beat3Weight;

      totalWeightedAccuracy += hitAccuracy * weight;
      totalWeight += weight;
    }

    // Only calculate if we have valid hits after excluding outliers
    if (totalWeight > 0)
    {
      float barAccuracy = totalWeightedAccuracy / totalWeight;
      barAccuracyScores.Add(barAccuracy);

      if (barAccuracyScores.Count > maxBarHistory)
      {
        barAccuracyScores.RemoveAt(0);
      }

      accuracy = barAccuracyScores.Average();
    }
  }
}