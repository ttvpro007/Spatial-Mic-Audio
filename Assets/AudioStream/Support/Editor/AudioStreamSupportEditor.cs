// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd
// custom editor for conditional displaying of fields in the editor inspired by Mr.Jwolf - 
// https://forum.unity3d.com/threads/inspector-enum-dropdown-box-hide-show-variables.83054/#post-951401

using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AudioStreamSupport
{
    public static class AudioStreamSupportEditor
    {
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
        [CustomPropertyDrawer(typeof(AudioStreamSupport.ReadOnlyAttribute))]
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
}