// (c) 2016-2022 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

// custom editor for conditional displaying of fields in the editor inspired by Mr.Jwolf - 
// https://forum.unity3d.com/threads/inspector-enum-dropdown-box-hide-show-variables.83054/#post-951401

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

using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Helper classes for AudioStream's common custom editor tasks
/// </summary>
namespace AudioStreamCustomInspector
{
    #region custom inspector sound directivity
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
    #region custom inspector conditional fields
    public abstract class FieldCondition<T>
    {
        public string conditionFieldName { get; set; }
        public T conditionFieldValue { get; set; }
        public string targetFieldName { get; set; }
        public bool isValid { get; set; }
        public string errorMsg { get; set; }
        /// <summary>
        /// type (descendants) filter, applied if != null
        /// </summary>
        public System.Type[] applicableForTypes { get; set; }

        public virtual string ToStringFunction()
        {
            return string.Format("'{0}', '{1}' -> '{2}'", this.conditionFieldName, this.conditionFieldValue, this.targetFieldName);
        }
    }
    /// <summary>
    /// Conditionally display field based on bool field value
    /// </summary>
    public class BoolFieldCondition : FieldCondition<bool> { }
    /// <summary>
    /// Conditionally display field based on enum field value
    /// </summary>
    public class EnumFieldCondition : FieldCondition<System.Enum> { }
    /// <summary>
    /// Conditionally display field based on type
    /// </summary>
    public class TypeOfTargetCondition : FieldCondition<Type> { }
    /// <summary>
    /// Conditionally display a field based on other string field value
    /// </summary>
    public class StringStartsWithFieldCondition : FieldCondition<string>
    {
        public string conditionFieldValueStartsWith;
        public override string ToStringFunction()
        {
            return string.Format("'{0}', '{1}' -> '{2}'", this.conditionFieldName, this.conditionFieldValueStartsWith, this.targetFieldName);
        }
    }
    /// <summary>
    /// Conditional displaying of fields based on value of other field
    /// </summary>
    public static class ConditionalFields
    {
        public static void ValidateFieldCondition<T>(FieldCondition<T> fieldCondition, UnityEngine.Object forTarget)
        {
            fieldCondition.errorMsg = "";
            fieldCondition.isValid = true;

            //Valildate 'conditionFieldName'
            if (!string.IsNullOrEmpty(fieldCondition.conditionFieldName)
                && (
                    fieldCondition.applicableForTypes == null
                    || fieldCondition.applicableForTypes.Contains(forTarget.GetType())
                    )
                )
            {
                // all instance members to include [SerializeField] protected too
                var field = forTarget.GetType().GetField(fieldCondition.conditionFieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null)
                {
                    fieldCondition.isValid = false;
                    fieldCondition.errorMsg = string.Format("[{0}] Could not find condition field '{1}' in '{2}'", fieldCondition, fieldCondition.conditionFieldName, forTarget);
                }
            }

            //Valildate 'targetFieldName'
            if (fieldCondition.isValid)
            {
                if (!string.IsNullOrEmpty(fieldCondition.targetFieldName)
                    && (
                        fieldCondition.applicableForTypes == null
                        || fieldCondition.applicableForTypes.Contains(forTarget.GetType())
                        )
                    )
                {
                    // all instance members to include [SerializeField] protected too
                    FieldInfo fieldWithCondition = forTarget.GetType().GetField(fieldCondition.targetFieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fieldWithCondition == null)
                    {
                        fieldCondition.isValid = false;
                        fieldCondition.errorMsg = string.Format("[{0}] Could not find condition target field '{1}' in '{2}'", fieldCondition, fieldCondition.targetFieldName, forTarget);
                    }
                }
            }

            if (!fieldCondition.isValid)
            {
                fieldCondition.errorMsg += string.Format("\r\n{0}", fieldCondition.ToStringFunction());
            }
        }
        public static BoolFieldCondition ShowOnBool(string boolFieldName, bool boolFieldValue, string fieldName, UnityEngine.Object forTarget, System.Type[] forTypes)
        {
            var newFieldCondition = new BoolFieldCondition()
            {
                conditionFieldName = boolFieldName,
                conditionFieldValue = boolFieldValue,
                targetFieldName = fieldName,
                isValid = true,
                applicableForTypes = forTypes
            };

            ConditionalFields.ValidateFieldCondition(newFieldCondition, forTarget);
            return newFieldCondition;
        }
        public static EnumFieldCondition ShowOnEnum(string enumFieldName, System.Enum enumValue, string fieldName, UnityEngine.Object forTarget, System.Type[] forTypes = null)
        {
            var newFieldCondition = new EnumFieldCondition()
            {
                conditionFieldName = enumFieldName,
                conditionFieldValue = enumValue,
                targetFieldName = fieldName,
                isValid = true,
                applicableForTypes = forTypes
            };

            ConditionalFields.ValidateFieldCondition(newFieldCondition, forTarget);
            return newFieldCondition;
        }

        public static TypeOfTargetCondition ShowOnTarget(string fieldName, UnityEngine.Object forTarget, System.Type[] forTypes)
        {
            var newFieldCondition = new TypeOfTargetCondition()
            {
                conditionFieldName = string.Empty,
                conditionFieldValue = null,
                targetFieldName = fieldName,
                isValid = true,
                applicableForTypes = forTypes
            };

            ConditionalFields.ValidateFieldCondition(newFieldCondition, forTarget);
            return newFieldCondition;
        }
        public static StringStartsWithFieldCondition ShowOnStringStartsWithValue(string stringFieldName, string stringFieldValueStartsWith, string fieldName, UnityEngine.Object forTarget, System.Type[] forTypes)
        {
            var newFieldCondition = new StringStartsWithFieldCondition()
            {
                conditionFieldName = stringFieldName,
                conditionFieldValueStartsWith = stringFieldValueStartsWith,
                targetFieldName = fieldName,
                isValid = true,
                applicableForTypes = forTypes
            };

            ConditionalFields.ValidateFieldCondition(newFieldCondition, forTarget);
            return newFieldCondition;
        }
    }
    #endregion
    #region custom inspector readonly field
    /// <summary>
    /// ReadOnly drawer - just disabled default drawer
    /// </summary>
    [CustomPropertyDrawer(typeof(AudioStream.ReadOnlyAttribute))]
    public class ReadOnlyDrawer : PropertyDrawer
    {
        /// <summary>
        /// The drawing of the field
        /// </summary>
        /// <param name="position"></param>
        /// <param name="property"></param>
        /// <param name="label"></param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true;
        }
    }
    #endregion
}