using UnityEngine;

public class DrumHitExpand : MonoBehaviour
{
  public float returnSpeed = 10f;

  Material mat;
  float currentExpand;

  void Awake()
  {
    mat = GetComponent<Renderer>().material;
  }

  void OnCollisionEnter(Collision collision)
  {
    currentExpand = collision.gameObject.GetComponent<Drumstick>().impactVelocity;
    mat.SetFloat("_Expand", currentExpand);
  }

  void Update()
  {
    // Smoothly return to zero
    currentExpand = Mathf.Lerp(currentExpand, 0, Time.deltaTime * returnSpeed);
    mat.SetFloat("_Expand", currentExpand);
  }
}