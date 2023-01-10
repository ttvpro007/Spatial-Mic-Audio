//
//  AudioStreamAppController.h
//  Unity-iPhone
//
// (c) 2022 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
//
#pragma once

#import "UnityAppController.h"

@interface AudioStreamAppController : UnityAppController
{
    NSTimer* iceTimer;
}

-(void) UpdateUnityPlayerInTheBackground;

@end
