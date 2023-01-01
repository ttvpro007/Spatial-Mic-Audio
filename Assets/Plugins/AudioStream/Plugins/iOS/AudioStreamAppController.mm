//
//  AudioStreamAppController.mm
//  Unity-iPhone
//
// (c) 2022 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
//

#import "AudioStreamAppController.h"
#import <AVFoundation/AVFoundation.h>

@implementation AudioStreamAppController

-(void)startUnity:(UIApplication *)application
{
    [super startUnity:application];
    
    // UnitySetAudioSessionActive(false);
    
    // AVAudioSessionCategoryPlayback || AVAudioSessionCategoryPlayAndRecord is needed for AllowBluetooth
    // and for keeping audio (i.e. the app) in the loop while in the background
    // (& for (future) MPRemoteCommandCenter probably)

    ::printf("-> AudioStreamAppController startUnity()\n");
    
    AVAudioSession* audioSession = [AVAudioSession sharedInstance];
    
    if (UnityShouldPrepareForIOSRecording())
    {
        ::printf("-> AudioStreamAppController setting PlayAndRecord audio session\n");
        // this should allow bluetooth pairs of input/output routes
        // on iOS, but currently not w/ Unity audio it seems..
        [audioSession setCategory:AVAudioSessionCategoryPlayAndRecord
                             mode:AVAudioSessionModeDefault
                          options:AVAudioSessionCategoryOptionAllowBluetoothA2DP
                            error:nil];
    }
    else
    {
        ::printf("-> AudioStreamAppController setting Playback audio session\n");
        // implies AVAudioSessionCategoryOptionAllowBluetoothA2DP output routes
        // on iOS, but currently not w/ Unity audio it seems..
        [audioSession setCategory:AVAudioSessionCategoryPlayback
                            error:nil];
    }
    
    [audioSession setActive: YES error: nil];
}

-(void)applicationDidBecomeActive:(UIApplication *)application
{
    [self->iceTimer invalidate];
    self->iceTimer = nil;
    
    [super applicationDidBecomeActive:application];
}

-(void)applicationWillResignActive:(UIApplication *)application
{
    self->iceTimer = [NSTimer scheduledTimerWithTimeInterval:1.0 target:self selector:@selector(UpdateUnityPlayerInTheBackground) userInfo:nil repeats:YES];
    
    [super applicationWillResignActive:application];
}

- (void) UpdateUnityPlayerInTheBackground
{
    NSLog(@"⏲️");
    UnityBatchPlayerLoop();
}

@end

IMPL_APP_CONTROLLER_SUBCLASS(AudioStreamAppController);
