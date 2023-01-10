// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using UnityEngine;
using UnityEngine.Events;

namespace AudioStreamSupport
{
    // ========================================================================================================================================
    #region (at the time) missing definitions for Unity events types
    [System.Serializable]
    public class EventWithStringParameter : UnityEvent<string> { };
    [System.Serializable]
    public class EventWithStringBoolParameter : UnityEvent<string, bool> { };
    [System.Serializable]
    public class EventWithStringStringParameter : UnityEvent<string, string> { };
    [System.Serializable]
    public class EventWithStringStringObjectParameter : UnityEvent<string, string, object> { };
    [System.Serializable]
    public class EventWithStringAudioClipParameter : UnityEvent<string, AudioClip> { };
    [System.Serializable]
    public class EventWithStringStringArrayParameter : UnityEvent<string, string[]> { };
    #endregion
}