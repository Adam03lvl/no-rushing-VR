using UnityEngine;
using System.Collections.Generic;

public class AudioLoudnessAnalyzer : MonoBehaviour
{
  [Header("Audio Settings")]
  [SerializeField] private int sampleSize = 1024;

  [Header("Loudness Settings")]
  [SerializeField] private float smoothing = 0.1f;
  [SerializeField] private float sensitivity = 1.5f;
  [SerializeField][Range(0f, 1f)] private float loudness = 0f;

  [Header("Material Reference")]
  [SerializeField] private List<Material> materials = new List<Material>();

  private float[] samples;
  private float currentLoudness = 0f;

  void Start()
  {
    samples = new float[sampleSize];
  }

  void Update()
  {
    AnalyzeLoudness();

    if (materials.Count > 0)
    {
      foreach (var m in materials) 
      {
        m.SetFloat("_AudioLoudness", loudness);
      }
    }
  }

  void AnalyzeLoudness()
  {
    // Get spectrum data from ALL audio sources via AudioListener
    AudioListener.GetOutputData(samples, 0);

    // Calculate RMS (Root Mean Square) for loudness
    float sum = 0f;
    for (int i = 0; i < sampleSize; i++)
    {
      sum += samples[i] * samples[i];
    }

    float rms = Mathf.Sqrt(sum / sampleSize);

    // Apply sensitivity and clamp
    float targetLoudness = Mathf.Clamp01(rms * sensitivity);

    // Smooth the loudness value
    currentLoudness = Mathf.Lerp(currentLoudness, targetLoudness, smoothing);
    loudness = currentLoudness;
  }

  // Public method to get current loudness value
  public float GetLoudness()
  {
    return loudness;
  }
}