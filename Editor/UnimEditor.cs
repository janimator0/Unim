using System;
using System.Collections.Generic;
using System.Linq;
using Unim;
using UnityEditor;
using UnityEngine;

[CustomEditor((typeof(UnimPlayer)))]
public class UnimEditor : Editor
{
    // General vars
    private UnimPlayer unimPlayerScript;
    private string[] emptyStringArray = new string[0];
    private bool unimIsValid;
    const string kAssetPrefix = "Assets/StreamingAssets";


    // Anim Preview
    private int currFrame = 1;
    private float spf;
    private float prevFrameRate;
    private double lastUpdateTime;
    private float timeSinceLastFrame;
    private int playbackStartFrame;
    private int playbackEndFrame;
    private int playbackDuration;
    private int selectedPlaybackOption;
    private int prevSelectedPlaybackOption;
    private string[] playbackOptions;
    private static readonly List<string> defaultPlaybackOptions = new List<string>()
    {
        "- Frame Scrub -", "- Play All -"
    };

    // References lists
    private string[] spritesList;

    // Foldouts
    private bool foldoutTrack = false;
    private bool foldoutTriggers = false;
     
    // Serialized properties
    SerializedProperty trackObjectsToLink;
    SerializedProperty triggerUnityEvents;
    SerializedProperty sortingLayerId;
    SerializedProperty orderInLayer;
    int sortingLayerId_tmp = 0;
    int orderInLayerTmp = 0;
    
    
    private void OnEnable()
    {
        if (unimPlayerScript == null)
        {
            unimPlayerScript = (UnimPlayer) target;
        }
        
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }
        
        // Create custom update function in editor
        EditorApplication.update += EditorUpdate;
        
        // Assign properties
        triggerUnityEvents = serializedObject.FindProperty("triggerUnityEvents");
        trackObjectsToLink = serializedObject.FindProperty("trackObjectsToLink");
        sortingLayerId = serializedObject.FindProperty("sortingLayerId");
        orderInLayer = serializedObject.FindProperty("orderInLayer");


        if (unimPlayerScript.IsLoaded())
        {
            unimIsValid = true;
            SetupInspectorElements();
        }
        else
        {
            unimIsValid = false;
            ReloadUnim(); 
        }
    }

    private void OnDisable()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (unimIsValid)
        {
            unimPlayerScript.RenderFrame(1); // return Unim to first frame
        }

        if (EditorApplication.update != null)
        {
            EditorApplication.update -= EditorUpdate;
        }
    }



    public void ReloadUnim()
    {
        if (Application.isPlaying || !unimPlayerScript.AllUnimInputFilesLinked())
        {
            //DH: Should unimIsValid be set to false if  !unimPlayerScript.AllUnimInputFilesLinked()? 
            return;
        }
        
        // EditorUtility.UnloadUnusedAssetsImmediate();
        try 
        {
            unimPlayerScript.InitUnim();
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
            unimIsValid = false;
            return;
        }
        
        unimIsValid = true;
        SetupInspectorElements();
    }

    private void SetupInspectorElements()
    {
        PopulateClips();
        spritesList = GetSpriteList();
        unimPlayerScript.RenderFrame(1);
    }

    private void EditorUpdate()
    {
        if (unimIsValid && !Application.isPlaying)
        {
            // If in scrub mode don't update frame
            if (selectedPlaybackOption == 0)
            {
                return;
            }

            timeSinceLastFrame += (float)(EditorApplication.timeSinceStartup - lastUpdateTime);
            float currFrameRate = unimPlayerScript.GetFrameRate();
            if ( Math.Abs(prevFrameRate - currFrameRate) > 0.0001f )
            {
                timeSinceLastFrame = 0;
                spf = 1 / currFrameRate;
                prevFrameRate = currFrameRate;
            }
            
            if (timeSinceLastFrame >= spf )
            {
                //int frameOffset = (int)(1f + (float)(EditorApplication.timeSinceStartup - nextFrameTime) * fps);
                float frameOffset = Mathf.Floor(timeSinceLastFrame / spf);

                int newFrame = currFrame + (int)frameOffset;
                if (newFrame < playbackStartFrame)
                {
                    newFrame = playbackEndFrame - Mathf.FloorToInt((playbackStartFrame - newFrame) % playbackDuration );
                }
                else if (newFrame >= playbackEndFrame)
                {
                    newFrame = playbackStartFrame + Mathf.FloorToInt((newFrame - playbackEndFrame) % playbackDuration );
                }
                currFrame = newFrame;
                
                unimPlayerScript.RenderFrame(currFrame);
                SceneView.RepaintAll();
                timeSinceLastFrame -= Mathf.Abs(frameOffset * spf);
            }
            lastUpdateTime = EditorApplication.timeSinceStartup;
        }
    }
    
    
    int DrawSortingLayersPopup(int layerID) {
        var layers = SortingLayer.layers;
        var names = layers.Select(l => l.name).ToArray();
        if (!SortingLayer.IsValid(layerID))
        {
            layerID = layers[0].id;
        }
        var layerValue = SortingLayer.GetLayerValueFromID(layerID);
        var newLayerValue = EditorGUILayout.Popup("Sorting Layer", layerValue, names);
        if (newLayerValue == -1) // TODO This is a hack that needs to be fixed because newLayerValue was returning -1 for some reason.
        {
            newLayerValue = 0;
        }
        return layers[newLayerValue].id;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        // Sorting Order 
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sorting Order", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        sortingLayerId_tmp = DrawSortingLayersPopup(sortingLayerId_tmp);
        if(EditorGUI.EndChangeCheck())
        {
            sortingLayerId.intValue = sortingLayerId_tmp;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        orderInLayerTmp = EditorGUILayout.IntField("Order In Layer", orderInLayerTmp);
        if(EditorGUI.EndChangeCheck())
        {
            orderInLayer.intValue = orderInLayerTmp;
        }
        EditorGUILayout.EndHorizontal();

        // Rest
        serializedObject.Update();
        if (!Application.isPlaying)
        {
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            if (GUILayout.Button("Refresh Unim"))
            {
                // TODO Remove below code?
                if (unimPlayerScript.GetComponent<MeshRenderer>().sharedMaterial == null)
                {
                    //unimPlayerScript.AssignMaterial(); //  seperating this method from everything fixes Assigning Material in Editor Bug. 
                }
                ReloadUnim();
            }
            
            // if Unim not loaded
            if (!unimIsValid)
            {
                EditorGUILayout.BeginVertical("GroupBox");
                GUIStyle s = new GUIStyle(EditorStyles.boldLabel);
                s.normal.textColor = Color.red;
                s.alignment = TextAnchor.MiddleCenter;
                EditorGUILayout.LabelField("No valid Unim detected", s);
                EditorGUILayout.EndVertical();
            }
            // if Unim Loaded
            else
            {
                //serializedObject.Update();

                EditorGUILayout.BeginVertical("GroupBox");
                EditorGUILayout.LabelField("Animation Preview", EditorStyles.boldLabel);
                DisplayAnimationPreviewGui();
                EditorGUILayout.Space();
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical("GroupBox");
                EditorGUILayout.LabelField("Advanced Features", EditorStyles.boldLabel);
                DisplayTrackObjectsGui();
                EditorGUILayout.Space();
                DisplayTriggerGui();
                EditorGUILayout.Space();
                DisplayReferenceLists();
                EditorGUILayout.Space();
                TrackObjectsToClipboardButton();
                SpriteListToClipboardButton();
                EditorGUILayout.EndVertical();
            }
        }
        else
        {
            if (unimPlayerScript != null) // solves for instances inbetween switching from Play Mode to Editor
            {
                EditorGUILayout.BeginVertical("GroupBox");
                EditorGUILayout.LabelField("Current Frame:", unimPlayerScript.CurrentFrame.ToString());
                EditorGUILayout.LabelField("Current Clip:", unimPlayerScript.CurrentFrame.ToString());
                EditorGUILayout.EndVertical();
            }
        }
        serializedObject.ApplyModifiedProperties();
    }

    private void DisplayTriggerGui()
    {
        UnimData.Trigger[] t = unimPlayerScript.UnimData.triggers;

        // Hide Triggers in inspector if they don't exist
        if (t == null || t.Length <= 0) 
        {
            return;
        }
        
        // match the TriggerUnityEvent array size to the input trigger array size
        if (triggerUnityEvents.arraySize != t.Length)
        {
            triggerUnityEvents.arraySize = t.Length;
        }
        
        foldoutTriggers = EditorGUILayout.Foldout((bool) foldoutTriggers, "Triggers" );
        if (foldoutTriggers)
        {
            for (int i = 0; i < triggerUnityEvents.arraySize; i++)
            {
                EditorGUILayout.PropertyField(triggerUnityEvents.GetArrayElementAtIndex(i),new GUIContent(t[i].name));
            }
        }
    }

    private void TrackObjectsToClipboardButton()
    {
        if (GUILayout.Button("Track Objects To Clipboard"))
        {
            TextEditor te = new TextEditor();
            te.text =  "Track Objects not yet implemented :(";
            te.SelectAll();
            te.Copy();
        }
    }
    
    private void SpriteListToClipboardButton()
    {
        if (GUILayout.Button("Sprite List To Clipboard"))
        {
            TextEditor te = new TextEditor();
            te.text = "Sprite List not yet implemented :(";
            te.SelectAll();
            te.Copy();
        }
    }

    private void DisplayReferenceLists()
    {
        EditorGUILayout.Popup(0, spritesList ?? emptyStringArray);
    }

    private void DisplayAnimationPreviewGui()
    {
        currFrame = EditorGUILayout.IntField("Frame:", currFrame);
        currFrame = Mathf.Clamp(currFrame, 1, unimPlayerScript.UnimData.MaxFrames - 1);
        // Only update frame if does not match previous frame
        if (currFrame != unimPlayerScript.CurrentFrame)
        {
            unimPlayerScript.RenderFrame(currFrame);
            currFrame = unimPlayerScript.CurrentFrame; // In case out of range will apply it back to range.
        }

        selectedPlaybackOption =
            EditorGUILayout.Popup("Clip", selectedPlaybackOption, playbackOptions ?? emptyStringArray);
        if (prevSelectedPlaybackOption != selectedPlaybackOption)
        {
            if (selectedPlaybackOption == 0)
            {
                playbackStartFrame = 0;
                playbackEndFrame = 0;
            }
            else if (selectedPlaybackOption == 1)
            {
                playbackStartFrame = 1;
                playbackEndFrame = unimPlayerScript.UnimData.MaxFrames;
            }
            else
            {
                UnimClip tmpClip =
                    unimPlayerScript.GetClipByClipIndex(selectedPlaybackOption - defaultPlaybackOptions.Count);
                playbackStartFrame = tmpClip.startFrame;
                playbackEndFrame = tmpClip.startFrame + tmpClip.duration;
                currFrame = playbackStartFrame;
            }

            playbackDuration = playbackEndFrame - playbackStartFrame;
            prevSelectedPlaybackOption = selectedPlaybackOption;
        }

        if (GUILayout.Button("Reset"))
        {
            selectedPlaybackOption = 0;
            currFrame = 1;
        }
        
        if (GUILayout.Button("Clips To Clipboard"))
        {
            List<string> clipList = unimPlayerScript.GetListOfClips();
            
            TextEditor te = new TextEditor();
            te.text = "public enum Clip\n{";
            for (int i = 0; i < clipList.Count; i++)
            {
                te.text += "\n" + clipList[i] + ",";
            }
            te.text += "\n}";
            te.SelectAll();
            te.Copy();
        }
    }

    private void DisplayTrackObjectsGui()
    {
        UnimData.TrackObject[] tO = unimPlayerScript.UnimData.trackObjects;
        
        if (trackObjectsToLink.arraySize != tO.Length)
        {
            trackObjectsToLink.arraySize = tO.Length;
        }
        
        foldoutTrack = EditorGUILayout.Foldout( foldoutTrack, "Track Objects");
        if (foldoutTrack)
        {
            for (int i = 0; i < trackObjectsToLink.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(tO[i].name, GUILayout.Width(150));
                EditorGUILayout.PropertyField(trackObjectsToLink.GetArrayElementAtIndex(i), GUIContent.none);
                EditorGUILayout.EndHorizontal();
            }
        }
    }

    // Populate
    private void PopulateClips()
    {
        List<string> tmp = new List<string>(defaultPlaybackOptions);
        tmp.AddRange(unimPlayerScript.GetListOfClips());
        playbackOptions = tmp.ToArray();
    }

    private string[] GetSpriteList()
    {
        List<string> s = new List<string>();
        s.Add("- Sprite List -");
        s.AddRange(unimPlayerScript.GetListOfSprites());
        return s.ToArray();
    }
}


