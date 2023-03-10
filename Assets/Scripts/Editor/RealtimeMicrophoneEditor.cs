using System;
using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects, CustomEditor(typeof(RealtimeMicrophone))]
public class RealtimeMicrophoneEditor : Editor
{
    private void OnEnable()
    {
        // Hook to the microphone property in the RealtimeMicrophone script
        microphone = serializedObject.FindProperty("microphone");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        CacheMicrophoneDevices();

        if (!microphone.hasMultipleDifferentValues)
        {
            ShowMicrophoneDevicesDropdown();
        }
        else
        {
            EditorGUILayout.PropertyField(microphone);
        }

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Show all the cached microphone devices
    /// </summary>
    private void ShowMicrophoneDevicesDropdown()
    {
        if (microphoneDevices.Length == 0) return;
        
        int index = Array.IndexOf(microphoneDevices, microphone.stringValue);
        int newIndex = EditorGUILayout.Popup("Microphone", Mathf.Max(0, index), microphoneDevices);

        // Change microphone name if index value has changed
        if (index != newIndex)
            microphone.stringValue = microphoneDevices[newIndex];
    }

    private SerializedProperty microphone;
    private string[] microphoneDevices = new string[0];

    /// <summary>
    /// Cache the list of available microphone devices 
    /// </summary>
    private void CacheMicrophoneDevices()
    {
        if (microphoneDevices.Length == Microphone.devices.Length) return;

        microphoneDevices = Microphone.devices;
        Array.Sort(microphoneDevices);
    }
}