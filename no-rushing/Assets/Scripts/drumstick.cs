using UnityEngine;
using UnityEngine.XR;

public enum PedalDrum
{
  Kick,
  Pedal
}

public class Drumstick : MonoBehaviour
{
  public AudioPlayer audioPlayer;
  public InputDevice inputDevice;
  public PedalDrum pedalDrum;
  public XRNode controllerNode;
  public Transform drum;

  private bool lastTriggerValue = false;
  private float pedalVelocity = 0.0f;

  private Vector3 lastPosition;
  public float impactVelocity = 0.0f;

  private bool hasImpulse = false;

  private void Start()
  {
    inputDevice = InputDevices.GetDeviceAtXRNode(controllerNode);
    inputDevice.TryGetHapticCapabilities(out HapticCapabilities caps);
    hasImpulse = caps.supportsImpulse;
  }

  void Update()
  {
    if (!inputDevice.isValid) InputDevices.GetDeviceAtXRNode(controllerNode);

    impactVelocity = (transform.position - lastPosition).magnitude / Time.deltaTime;
    impactVelocity = Mathf.Clamp01(impactVelocity * 0.33f);
    lastPosition = transform.position;

    if(shouldPlayPedal())
    {
      if (hasImpulse) 
        inputDevice.SendHapticImpulse(0, pedalVelocity, .1f);
      audioPlayer.PlaySource(pedalDrum.ToString(), pedalVelocity * .33f);
    }

    if (controllerNode.Equals(XRNode.RightHand))
    {
      inputDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool metronome);
      if (metronome) audioPlayer.ToggleMetronome();
    }

    if (controllerNode.Equals(XRNode.LeftHand))
    {
      bool bpmUp = false;
      bool bpmDown = false;

      inputDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bpmDown);
      inputDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out bpmUp);

      if (bpmUp) audioPlayer.bpm++;
      if (bpmDown) audioPlayer.bpm--;
    }

    Debug.DrawRay(drum.position, drum.forward, Color.red);
  }

  private bool shouldPlayPedal()
  {
    if (!inputDevice.isValid)
    {
      inputDevice = InputDevices.GetDeviceAtXRNode(controllerNode);
      Debug.Log($"{controllerNode} - Device invalid, re-getting. Valid now: {inputDevice.isValid}");
    }

    inputDevice.TryGetFeatureValue(CommonUsages.trigger, out pedalVelocity);

    if (pedalVelocity <= 0)
    {
      lastTriggerValue = true;
    }
    else if (lastTriggerValue && pedalVelocity > .01f )
    {
      pedalVelocity = 1;
      lastTriggerValue = false;

      return true;
    }

    pedalVelocity = 0;
    return false;
  }

  private void OnCollisionEnter(Collision collision)
  {
    if (impactVelocity <= 0.1f)
      return;

    string collidedWith = collision.gameObject.tag;

    if (hasImpulse)
    {
      inputDevice.SendHapticImpulse(0, impactVelocity, .1f);
    }

    audioPlayer.PlaySource(collidedWith, impactVelocity);
  }
}
