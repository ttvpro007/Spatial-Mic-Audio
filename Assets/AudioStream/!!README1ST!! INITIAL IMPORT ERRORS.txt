
Hi, welcome and thanks for your interest in AudioStream !

Please read carefully before anything else!
================================================================================                                            

! Do NOT enter the play mode (run any demos) until FMOD is imported - asset's Editor scripts won't be run properly otherwise
(they're not critical, but will setup runtime info and populate Build Settings with demo scenes)


You'll get compile errors after importing into a project without FMOD for Unity package present
(AudioStream uses FMOD Studio functionality, which redistribution is not allowed for 3rd party SDKs such as this one):

: error CS0246: 'The type or namespace name `FMOD' could not be found. Are you missing a using directive or an assembly reference?'

: and native mixer plugin will fail to load with warning/error (depending on Unity version):
Plugins: Failed to load 'Assets/AudioStream/Plugins/x86_64/AudioPluginAudioStreamOutputDevice.dll' with error 'The specified module could not be found.'.
, followed by the error:
Effect AudioStream OutputDevice could not be found. Check that the project contains the correct native audio plugin libraries and that the importer settings are set up correctly.

This is normal and will be resolved once you manually import the FMOD for Unity package and/or add native dll for mixer effect, if you choose to use it.

You will also get error about importing included multichannel audio file used for demo purposes:
- Errors during import of AudioClip Assets/AudioStream/StreamingAssets/24ch_polywav_16bit_48k.wav:
FSBTool ERROR: Failed decoding audio clip.
Unity can't work with files like this, so nothing can be done about this.

FMOD for Unity can be found either at the Asset Store:
https://assetstore.unity.com/packages/tools/audio/fmod-for-unity-161631

or can be downloaded directly from FMOD:
https://fmod.com/download#unityintegration
under 'Unity Integration' section - using latest Unity Verified version is recommeded
(you'll need to create account to download directly from FMOD)

These two are generally exactly the same, except Asset Store version might sometimes lag behind the official release.

As mentioned in the asset's store page description, FMOD for Unity has currently free Indie License option, for all details please see here: https://fmod.com/licensing


--------------------------------------------------------------------------------
>> For new users: <<
--------------------------------------------------------------------------------

>:: 
>:: Depending on needed functionality and platform you can choose to use only the mixer plugin for routing audio to any system's output on x86/x64 Windows and macOS from the Unity AudioMixers, use only 'normal' (non mixer effect) AudioStream, or both
>::
See each option install and usage instructions below

NOTE: Native mixer plugin effect 'AudioStream OutputDevice'
- the AudioPluginAudioStreamOutputDevice.dll in Plugins/x86_64, Plugins/x86 and AudioPluginAudioStreamOutputDevice.bundle in Plugins - 
is currently available on 32 and 64-bit Windows and macOS *ONLY*.
> For other platforms/non mixer usage please still use AudioSourceOutputDevice component of AudioStream.


================================================================================
UPDATING FMOD for Unity package:
================================================================================

When doing an update of the FMOD package to a newer version in existing project please be aware that since it uses several native plugins the safest way of upgrading is to:

1] import new Asset Store package into a new separate Unity project with correct (project) Unity version as per 'FMOD for Unity package installation' below 
2] copy/overwrite all files manually outside of Unity (i.e. via filesystem) from the above temporary project into upgraded project's 'Plugins' folder with that projecty closed, possibly deleting target FMOD folder first
3] delete '_audiostream_demo_assets_prepared' in 'Assets\StreamingAssets\AudioStream' (or the whole directory) in order to copy any new/changed demo assets if you want to run demo scenes
4] deleting 'Library' folder of the project being updated also helps before reopening it


Note about 2018/2019/2020 LTS and Asset Store - if updated version of FMOD doesn't show up in your PM assets, delete the FMOD folder in Asset Store-5.x Unity asset store cache and try again.


================================================================================
FMOD for Unity package installation:
================================================================================

As mentioned above FMOD for Unity can be downloaded either from the Asset Store, or from FMOD directly.
AudioStream uses only low level/Core API of FMOD Studio and only really requires a part of the "Plugins" folder from the FMOD package.
The Plugins folder contains C# wrapper for FMOD and all necessary platform specific libraries, the rest of the package enables usage of FMOD Studio projects and objects directly in Unity, live editing of FMOD project and access to other FMOD Studio project capabilities.
In general you need only native platforms plugins, and low level FMOD C# wrapper.

The asset currently (092020) supports FMOD 2.01.04 and up.

When presented import dialog while importing FMOD for Unity package it's safe to select only:

* 'Plugins/FMOD/platforms'
* 'Plugins/FMOD/src' EXCEPT Editor folder | resp. in older versions 'Plugins/FMOD/src/Runtime/' folder (Timeline support can be omitted, too)
* 'Plugins/FMOD/staging/' folder if it's present: set libraries' platform import settings included here to Editor + appropriate CPU/OS in the Inspector to use them in Editor/Debug runs if Unity automatically doesn't set correct import settings for them
* you probably want to include 'Plugins/FMOD/platform_ios.mm' on iOS
* you should probably include 'Plugins/FMOD/LICENSE.TXT', too
* include FMODUnity.asmdef

Everything else - which is Unity Editor FMOD Studio support - does not need - to be imported for this plugin to work.

Once the FMOD Studio Unity package is successfully imported, AudioStream should be ready to use. See also next how asmdefs are setup.


You can move AudioStream folder freely anywhere in the project, for example into Plugins to reduce user scripts compile times.

Furthermore, if you don't intend to use native mixer plugin, you can delete :
- AudioStream/Demo/OutputDevice/UnityMixer folder with demo scene
- AudioPluginAudioStreamOutputDevice.bundle mac OS plugin from Plugins
- AudioPluginAudioStreamOutputDevice.dll Windows plugin from Plugins/x86_64/ and Plugins/x86

--------------------------------------------------------------------------------
IMPORTANT:
the above mentioned missing import settings for libraries in 'Plugins/FMOD/staging/' will result in 'DllNotFoundException: fmodstudioL' error when trying to run demo scenes (load FMOD) in (early) Unity 2018, 2019 LTS
--------------------------------------------------------------------------------


================================================================================
ASMDEFs:
================================================================================

Main assets assembly definition is 'AudioStream\Scripts\AudioStream.asmdef', it has two weak (non GUID) references to
'AudioStream\Support\Scripts\AudioStreamSupport.asmdef', and 
'Plugins\FMOD\FMODUnity.asmdef'
- since theses are just string / weak / references, the FMOD asmdef should always be found after FMOD is imported.

Apart from these the asset defines
'AudioStream\Editor\AudioStreamEditor.asmdef' for custom inspectors which has dependency on 'AudioStream.asmdef',
and 'AudioStreamSupport.asmdef' which mainly provides Editor time only data for runtime.




================================================================================
Windows/macOS native UnityMixer effect/plugin:
================================================================================

Included UnityMixer audio plugin/effect uses FMOD Engine/Core part directly and requires fmod.dll on Windows platforms resp. libfmod.dylib on macOS to be present.
Everything is included in the package for user convenience and since Asset Store Upload Tools can't omit them from package upload, but if you intent to use the mixer effect you should download it from FMOD and agree to FMOD EULA.

They are located under 'FMOD Studio Suite' download section, FMOD Engine, you should select the same version as is the main package.
Download and install on Windows/open .dmg on macOS respective installer. one file is needed from it -

On 64-bit Windows - copy 'fmod.dll' from C:\Program Files (x86)\FMOD SoundSystem\FMOD Studio API Windows\api\core\lib\x64 (default install location) to AudioStream/Plugins/x86_64/
On 32-bit Windows - similarly as above
- the dll *must* be placed alongside AudioPluginAudioStreamOutputDevice.dll

On macOS - copy 'libfmod.dylib' from FMOD Programmers API/api/core/lib to AudioStream/Plugins/
- alongside AudioPluginAudioStreamOutputDevice.bundle

Note that both plugins are compiled against specific version of FMOD at any given time - but should be binary compatible with earlier/future versions

If you don't need other AudioStream functionality you can delete 'everything else', meaning:
- all demo scenes, theirs scripts and resources ( possibly except AudioStream/Demo/OutputDevice/UnityMixer and 'OutputDevice/262447__xinematix__action-percussion-ensemble-fast-4-170-bpm' audio to test the plugin )
- AudioStream/Editor folder
- AudioStream/Plugins/iOS folder
- whole AudioStream/Scripts folder
(the mixer plugin does not use any c# scripts)

You might want to restart Unity once native plugins are in place.

AudioStream/Demo/OutputDevice/UnityMixer/OutputDeviceUnityMixerDemo scene should be now working playing looped AudioClip on system outputs 1 and 2 (with fallback to output 0 if either is not available)


--------------------------------------------------------------------------------

For 2022 and up:
-> please enable non secure HTTP downloads in 2022 (and up) Player settings in order to use links in the demo
| Go to Edit -> Project Settings -> Player -> Other Settings > Configuration and set 'Allow downloads over HTTP' to 'Always allowed'. Alternatively, use your own secure (HTTPS) links only.



--------------------------------------------------------------------------------

Please see _Docs\Documentation.txt and _Docs folder for description and usage of each component and few concepts guides.

Package is submitted with Unity first 2019.4 LTS version and tested on up to latest current beta, but in general a LTS version of Unity is recommended
and latest (Unity verified) version of FMOD is supported since there are often breaking changes

It's possible to use it in earlier versions, but only with manual changes / initially was submitted for Unity 5.3.x and worked in previous versions /

================================================================================


In case of any questions / suggestion feel free to ask on support forum. Often things change without notice, especially things like setting up and building to all various/mobile platforms.
And, if AudioStream served you well you might consider leaving a rating and/or review on the Asset Store page - that helps a lot thanks !

== forum link == :	https://forum.unity.com/threads/audiostream-an-audio-streaming-solution-for-all-and-everywhere.412029/
== email == :		mcv618 at gmail dot com.
== twitter == :		DMs are open at twitter https://twitter.com/r618

Thanks!

Martin
