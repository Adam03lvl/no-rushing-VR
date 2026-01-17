#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

[CustomEditor(typeof(AudioPlayer))]
public class AudioPlayerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        AudioPlayer player = (AudioPlayer)target;
        
        if (GUILayout.Button("Auto-Fill Audio Clips"))
        {
            player.SoundsList.Clear();
            AudioResource[] resources = Resources.LoadAll<AudioResource>("Drums");
            Debug.Log($"Length: {resources.Length}");
            player.SoundsList.AddRange(resources);
            EditorUtility.SetDirty(player);
        }
    }
}
#endif