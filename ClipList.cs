using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ClipList : PropertyAttribute {
    public delegate string[] GetStringList();

    public ClipList(params string [] list) {
        List = list;
    }

    public ClipList(Type type, string methodName) {
        var method = type.GetMethod (methodName);
        if (method != null) {
            List = method.Invoke (null, null) as string[];
        } else {
            Debug.LogError ("NO SUCH METHOD " + methodName + " FOR " + type);
        }
    }

    public string[] List {
        get;
        private set;
    }
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ClipList))]
public class ClipListDrawer : PropertyDrawer {
    // Draw the property inside the given rect
    public override void OnGUI (Rect position, SerializedProperty property, GUIContent label) {
        var clipList = attribute as ClipList;
        var list = clipList.List;
        if (property.propertyType == SerializedPropertyType.String) {
            int index = Mathf.Max (0, Array.IndexOf (list, property.stringValue));
            index = EditorGUI.Popup (position, property.displayName, index, list);

            property.stringValue = list [index];
        } else if (property.propertyType == SerializedPropertyType.Integer) {
            property.intValue = EditorGUI.Popup (position, property.displayName, property.intValue, list);
        } else {
            base.OnGUI (position, property, label);
        }
    }
}
#endif