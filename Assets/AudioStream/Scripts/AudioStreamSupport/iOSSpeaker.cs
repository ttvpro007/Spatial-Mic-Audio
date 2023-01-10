// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using UnityEngine;
using System.Runtime.InteropServices;

/// <summary>
/// These set alternative auto route for playback and/or recording in pre Unity 2017.1 which forced autio to be played on phone speaker /rather than in that case default headphones output/
/// post Unity 2017.1 has player settings which should render this not needed
/// </summary>
public static class iOSSpeaker
{
#if UNITY_IOS && !UNITY_EDITOR
	[DllImport("__Internal")]
	private static extern void _RouteForPlayback();
	[DllImport("__Internal")]
	private static extern void _RouteForRecording();
#endif

	public static void RouteForPlayback()
    {
#if UNITY_IOS && !UNITY_EDITOR
        _RouteForPlayback();
#endif
	}

	public static void RouteForRecording()
    {
#if UNITY_IOS && !UNITY_EDITOR
        _RouteForRecording();
#endif
	}
}
