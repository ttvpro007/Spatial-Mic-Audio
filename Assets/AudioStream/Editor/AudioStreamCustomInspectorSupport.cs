// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

// Directivity texture visualization from Resonance Audio for Unity
// Copyright 2017 Google Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using UnityEngine;

/// <summary>
/// Helper classes for AudioStream's common custom editor tasks
/// </summary>
namespace AudioStreamCustomInspector
{
    #region custom inspector sound directivity texture
    /// <summary>
    /// Polar directivity pattern for spatial sources
    /// </summary>
    public static class DirectivityPattern
    {
        /// Source directivity GUI color.
        static readonly Color ResonanceAudio_sourceDirectivityColor = 0.65f * Color.blue;

        public static void DrawDirectivityPattern(Texture2D directivityTexture, float alpha, float sharpness, int size)
        {
#if UNITY_2021_2_OR_NEWER
            directivityTexture.Reinitialize(size, size);
#else
            directivityTexture.Resize(size, size);
#endif
            // Draw the axes.
            Color axisColor = ResonanceAudio_sourceDirectivityColor.a * Color.black;
            for (int i = 0; i < size; ++i)
            {
                directivityTexture.SetPixel(i, size / 2, axisColor);
                directivityTexture.SetPixel(size / 2, i, axisColor);
            }
            // Draw the 2D polar directivity pattern.
            float offset = 0.5f * size;
            float cardioidSize = 0.45f * size;
            Vector2[] vertices = ResonanceAudio_Generate2dPolarPattern(alpha, sharpness, 180);
            for (int i = 0; i < vertices.Length; ++i)
            {
                directivityTexture.SetPixel((int)(offset + cardioidSize * vertices[i].x),
                                            (int)(offset + cardioidSize * vertices[i].y), ResonanceAudio_sourceDirectivityColor);
            }
            directivityTexture.Apply();
            // Show the texture.
            GUILayout.Box(directivityTexture);
        }

        /// Generates a set of points to draw a 2D polar pattern.
        static Vector2[] ResonanceAudio_Generate2dPolarPattern(float alpha, float order, int resolution)
        {
            Vector2[] points = new Vector2[resolution];
            float interval = 2.0f * Mathf.PI / resolution;
            for (int i = 0; i < resolution; ++i)
            {
                float theta = i * interval;
                // Magnitude |r| for |theta| in radians.
                float r = Mathf.Pow(Mathf.Abs((1 - alpha) + alpha * Mathf.Cos(theta)), order);
                points[i] = new Vector2(r * Mathf.Sin(theta), r * Mathf.Cos(theta));
            }
            return points;
        }
    }
    #endregion
}