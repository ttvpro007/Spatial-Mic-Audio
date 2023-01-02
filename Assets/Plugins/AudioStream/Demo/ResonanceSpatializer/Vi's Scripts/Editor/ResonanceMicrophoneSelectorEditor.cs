using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AudioStream;
using System.Linq;

[CanEditMultipleObjects, CustomEditor(typeof(ResonanceMicrophoneSelector))]
public class ResonanceMicrophoneSelectorEditor : Editor
{
    private void OnEnable()
    {
        // Hook to the properties in the RealtimeMicrophone script
        microphone = serializedObject.FindProperty("microphone");
        id = serializedObject.FindProperty("id");
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

        EditorGUILayout.PropertyField(id);

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Show all the cached microphone devices
    /// </summary>
    private void ShowMicrophoneDevicesDropdown()
    {
        if (microphoneDevices.Length == 0) return;

        // int index = Array.IndexOf(microphoneDevices, microphone.stringValue);
        int newIndex = EditorGUILayout.Popup("Microphone", Mathf.Max(0, id.intValue), microphoneDevices);

        // Change microphone name if index value has changed
        if (id.intValue != newIndex)
        {
            microphone.stringValue = microphoneDevices[newIndex];
            id.intValue = newIndex;
        }
    }

    private SerializedProperty microphone;
    private SerializedProperty id;
    private string[] microphoneDevices = new string[0];
    private ResonanceInput resonanceInput;

    /// <summary>
    /// Cache the list of available microphone devices 
    /// </summary>
    private void CacheMicrophoneDevices()
    {
        if (microphoneDevices.Length > 0) return;

        if (!resonanceInput)
            resonanceInput = Selection.activeGameObject.GetComponent<ResonanceInput>();

        if (!resonanceInput) return;
        
        var mics = FMOD_SystemW.AvailableInputs(AudioStreamSupport.LogLevel.ERROR, resonanceInput.gameObject.name, resonanceInput.OnError, true);

        if (microphoneDevices.Length == mics.Count) return;

        microphoneDevices = mics.Select(x => x.name).ToArray();
    }
}