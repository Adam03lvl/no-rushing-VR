using TMPro;
using UnityEngine;

public class AccuracyText : MonoBehaviour
{

  public AudioPlayer player;
  public TextMeshProUGUI accuracyText;
  public TextMeshProUGUI bpmText;

  void Update()
  {
    accuracyText.text =player.accuracy.ToString("00.0") + '%';
    bpmText.text = player.bpm.ToString("00");
  }
}
