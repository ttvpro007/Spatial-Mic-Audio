// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStream;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using static AudioStreamSupport.AudioStreamSupportEditor;

namespace AudioStreamCustomInspector
{
    [CustomEditor(typeof(AudioStreamBase), true)]
    [CanEditMultipleObjects]
    public class AudioStreamCustomInspector : Editor
    {
        /// <summary>
        /// Resonance plugin
        /// </summary>
        Texture2D directivityTexture = null;

        void SetFieldCondition()
        {
            // . custom inspector is sometimes buggily invoked for different base class what
            if (target == null)
                return;

            // AudioStreamBase
            // the reflection system cares only about the final enum member name
            this.enumFieldConditions.Add(ConditionalFields.ShowOnEnum("streamType", AudioStreamBase.StreamAudioType.RAW, "RAWSoundFormat", target));
            this.enumFieldConditions.Add(ConditionalFields.ShowOnEnum("streamType", AudioStreamBase.StreamAudioType.RAW, "RAWFrequency", target));
            this.enumFieldConditions.Add(ConditionalFields.ShowOnEnum("streamType", AudioStreamBase.StreamAudioType.RAW, "RAWChannels", target));

            this.stringFieldStartsWithConditions.Add(ConditionalFields.ShowOnStringStartsWithValue("url", "http", "playFromCache", target, new System.Type[] { typeof(AudioStream.AudioStream), typeof(AudioStream.AudioStreamMinimal) }));
            this.stringFieldStartsWithConditions.Add(ConditionalFields.ShowOnStringStartsWithValue("url", "http", "downloadToCache", target, new System.Type[] { typeof(AudioStream.AudioStream), typeof(AudioStream.AudioStreamMinimal) }));

            // AudioStreamMemory AudioStreamRuntimeImport
            // this.boolFieldConditions.Add(ConditionalFields.ShowOnBool("useDiskCache", true, "slowClipCreation", target));

            // 'outputDriverID' visible only for 'Minimal' compoments
            this.typeOfTargetConditions.Add(ConditionalFields.ShowOnTarget("outputDriverID", target, new System.Type[] { typeof(AudioStreamMinimal)
                , typeof(ResonanceSoundfield)
                , typeof(ResonanceSource)
            }));
        }

        List<EnumFieldCondition> enumFieldConditions;
        List<BoolFieldCondition> boolFieldConditions;
        List<TypeOfTargetCondition> typeOfTargetConditions;
        List<StringStartsWithFieldCondition> stringFieldStartsWithConditions;
        public void OnEnable()
        {
            this.enumFieldConditions = new List<EnumFieldCondition>();
            this.boolFieldConditions = new List<BoolFieldCondition>();
            this.typeOfTargetConditions = new List<TypeOfTargetCondition>();
            this.stringFieldStartsWithConditions = new List<StringStartsWithFieldCondition>();
            this.SetFieldCondition();

            this.directivityTexture = Texture2D.blackTexture;
        }

        public override void OnInspectorGUI()
        {
            // Update the serializedProperty - always do this in the beginning of OnInspectorGUI.
            serializedObject.Update();

            var obj = serializedObject.GetIterator();

            if (obj.NextVisible(true))
            {
                // Resonance plugin
                float? directivity = null;
                float? directivitySharpness = null;

                // Loops through all visible fields
                do
                {
                    bool shouldBeVisible = true;
                    {
                        // Tests if the field is a field that should be hidden/shown based on other's enum value
                        foreach (var fieldCondition in enumFieldConditions)
                        {
                            //If the fieldcondition isn't valid, display an error msg.
                            if (!fieldCondition.isValid)
                            {
                                Debug.LogError(fieldCondition.errorMsg);
                            }
                            else if (fieldCondition.targetFieldName == obj.name)
                            {
                                var conditionEnumValue = (System.Enum)fieldCondition.conditionFieldValue;
                                var currentEnumValue = (System.Enum)target.GetType().GetField(fieldCondition.conditionFieldName).GetValue(target);

                                //If the enum value isn't equal to the wanted value the field will be set not to show
                                if (currentEnumValue.ToString() != conditionEnumValue.ToString()
                                    || (fieldCondition.applicableForTypes != null
                                    && !fieldCondition.applicableForTypes.Contains(target.GetType()))
                                    )
                                {
                                    shouldBeVisible = false;
                                    break;
                                }
                            }
                        }
                    }

                    // if not precessed
                    if (shouldBeVisible)
                    {
                        // Tests if the field is a field that should be hidden/shown based on other's bool value
                        foreach (var fieldCondition in boolFieldConditions)
                        {
                            //If the fieldcondition isn't valid, display an error msg.
                            if (!fieldCondition.isValid)
                            {
                                Debug.LogError(fieldCondition.errorMsg);
                            }
                            else if (fieldCondition.targetFieldName == obj.name)
                            {
                                var boolField = target.GetType().GetField(fieldCondition.conditionFieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                var boolValue = (bool)boolField.GetValue(target);

                                //If the bool value isn't equal to the wanted value the field will be set not to show
                                if (boolValue != fieldCondition.conditionFieldValue
                                    || (fieldCondition.applicableForTypes != null
                                    && !fieldCondition.applicableForTypes.Contains(target.GetType()))
                                    )
                                {
                                    shouldBeVisible = false;
                                    break;
                                }
                            }
                        }
                    }

                    if(shouldBeVisible)
                    {
                        // Tests if the field is a field that should be hidden/shown based on target type
                        foreach (var typeOfTargetCondition in typeOfTargetConditions)
                        {
                            //If the fieldcondition isn't valid, display an error msg.
                            if (!typeOfTargetCondition.isValid)
                            {
                                Debug.LogError(typeOfTargetCondition.errorMsg);
                            }
                            else if (typeOfTargetCondition.targetFieldName == obj.name)
                            {
                                var targetType = target.GetType();

                                if (!typeOfTargetCondition.applicableForTypes.Contains(targetType))
                                {
                                    shouldBeVisible = false;
                                    break;
                                }
                            }
                        }
                    }

                    if (shouldBeVisible)
                    {
                        foreach (var fieldCondition in this.stringFieldStartsWithConditions)
                        {
                            //If the fieldcondition isn't valid, display an error msg.
                            if (!fieldCondition.isValid)
                            {
                                Debug.LogError(fieldCondition.errorMsg);
                            }
                            else if (fieldCondition.targetFieldName == obj.name)
                            {
                                var field = target.GetType().GetField(fieldCondition.conditionFieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                var fieldValue = (string)field.GetValue(target);

                                if (!fieldValue.StartsWith(fieldCondition.conditionFieldValueStartsWith)
                                    || (fieldCondition.applicableForTypes != null
                                    && !fieldCondition.applicableForTypes.Contains(target.GetType()))
                                    )
                                {
                                    shouldBeVisible = false;
                                    break;
                                }
                            }
                        }
                    }

                    if (shouldBeVisible)
                        EditorGUILayout.PropertyField(obj, true);

                    // Resonance plugin
                    // (these should be always visible...)
                    if (serializedObject.targetObject.GetType() == typeof(ResonanceSource))
                    {
                        if (obj.name == "directivity")
                            directivity = obj.floatValue;

                        if (obj.name == "directivitySharpness")
                            directivitySharpness = obj.floatValue;

                        if (directivity.HasValue && directivitySharpness.HasValue)
                        {
                            GUI.skin.label.wordWrap = true;

                            GUILayout.BeginHorizontal();
                            GUILayout.Label("Approximate spatial spread strength of this audio source:");
                            DirectivityPattern.DrawDirectivityPattern(directivityTexture, directivity.Value, directivitySharpness.Value,
                                                   (int)(3.0f * EditorGUIUtility.singleLineHeight));
                            GUILayout.EndHorizontal();

                            directivity = null;
                            directivitySharpness = null;
                        }
                    }

                } while (obj.NextVisible(false));
            }

            // Apply changes to the serializedProperty - always do this in the end of OnInspectorGUI.
            serializedObject.ApplyModifiedProperties();
        }
    }
}