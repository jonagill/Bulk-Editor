using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace BulkEditor
{
    public static class SerializedPropertyExtensions
    {
        public static FieldInfo GetFieldInfo(this SerializedProperty serializedProperty, out Type fieldType)
        {
            return ScriptAttributeUtilityInternal.GetFieldInfoFromProperty( serializedProperty, out fieldType );
        }

        public static List<PropertyAttribute> GetFieldAttributes(this SerializedProperty serializedProperty)
        {
            var fieldInfo = GetFieldInfo( serializedProperty, out _ );
            return ScriptAttributeUtilityInternal.GetFieldAttributes( fieldInfo );
        }

        public static IEnumerable<T> GetFieldAttributes<T>(
            this SerializedProperty serializedProperty) where T : PropertyAttribute
        {
            var fieldInfo = GetFieldInfo( serializedProperty, out _ );
            return ScriptAttributeUtilityInternal.GetFieldAttributes<T>( fieldInfo );
        }

        public static Type GetEnumType(this SerializedProperty property)
        {
            var fieldInfo = property.GetFieldInfo( out _ );
            if ( fieldInfo != null )
            {
                return fieldInfo.FieldType.IsArray ? fieldInfo.FieldType.GetElementType() : fieldInfo.FieldType;
            }

            return null;
        }

        public static object GetEnumValue(this SerializedProperty property)
        {
            var enumType = property.GetEnumType();
            if ( enumType != null && enumType.IsEnum )
            {
                return Enum.ToObject( enumType, property.intValue );
            }

            return null;
        }

        public static object GetDefaultEnumValue(this SerializedProperty property)
        {
            var enumType = property.GetEnumType();
            var values = Enum.GetValues( enumType );
            return values.GetValue( 0 );
        }
    }
}

