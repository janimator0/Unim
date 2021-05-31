using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class UnimDropDownAttribute : PropertyAttribute
{
	public string[] enumNames;
	public bool UseDefaultTagFieldDrawer = false;

	public UnimDropDownAttribute(Type enumType)
	{
		enumNames = Enum.GetNames(enumType);
	}
}

[CustomPropertyDrawer(typeof(UnimDropDownAttribute))]
public class UnimDropDownPropertyDrawer : PropertyDrawer
{
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		if (property.propertyType == SerializedPropertyType.String)
		{
			EditorGUI.BeginProperty(position, label, property);
			var attrib = attribute as UnimDropDownAttribute;
			if (attrib == null)
			{
				return;
			}
			if (attrib.UseDefaultTagFieldDrawer)
			{
				property.stringValue = EditorGUI.TagField(position, label, property.stringValue);
			}
			else
			{
				List<string> tagList = attrib.enumNames.ToList();
				tagList.AddRange(UnityEditorInternal.InternalEditorUtility.tags);
				string propertyString = property.stringValue;
				int index = -1;
				if (propertyString == "")
				{
					//The tag is empty
					index = 0; //first index is the special <notag> entry
				}
				else
				{
					for (int i = 1; i < tagList.Count; i++)
					{
						if (tagList[i] == propertyString)
						{
							index = i;
							break;
						}
					}
				}

				//Draw the popup box with the current selected index
				index = EditorGUI.Popup(position, label.text, index, tagList.ToArray());

				//Adjust the actual string value of the property based on the selection
				if (index == 0)
				{
					property.stringValue = "";
				}
				else if (index >= 1)
				{
					property.stringValue = tagList[index];
				}
				else
				{
					property.stringValue = "";
				}
			}
			EditorGUI.EndProperty();
		}
		else
		{
			EditorGUI.PropertyField(position, property, label);
		}
	}
}