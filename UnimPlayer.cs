// © 2020 JAY EDRY ALL RIGHTS RESERVED

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using FullSerializer;

namespace Unim
{
	[RequireComponent(typeof(MeshRenderer))]
	[RequireComponent(typeof(MeshFilter))]
	public class UnimPlayer : MonoBehaviour
	{
		#region FIELDS

		// Shared Unim Data
		public UnimData UnimData; // Stores shared Unim variables

		[Header("Input Files")]
		[SerializeField] protected Texture2D unimSheetAsset;
		[SerializeField] protected TextAsset unimDataAsset;
		[SerializeField] private bool loadDataAsBytes;

		[Header("General Init Settings")]
		[SerializeField] private ClipUpdateMode updateMode = ClipUpdateMode.Time;
		[SerializeField] private float frameRate = 30;
		[SerializeField] private bool initOnAwake = true;
		[SerializeField] private bool enhancedColorOperations = true;
		[SerializeField] private bool playOnAwake = true;

		[Header("Additional Init Settings")]
		[SerializeField] private ScaleTypes scaleMode = ScaleTypes.ScaleToFitUnitWidth;
		[SerializeField] private float scaleMultiplier = 1;
		[Tooltip("Set pivot position.\n\n" +
		         "Bottom Left ( -1, -1 )\n" +
		         "Top Right ( 1, 1 )")]
		[SerializeField] private Vector2 pivotCoordinates;
		[SerializeField] private float spriteDepth = 0.001f;
		[SerializeField] private Vector2 cullingBoundsScaler = Vector2.one;

		// Hidden Sorting properties
		[HideInInspector] [SerializeField] private int sortingLayerId;
		[HideInInspector] [SerializeField] private int orderInLayer;

		// Hidden Parameters
		[HideInInspector] [SerializeField] private GameObject[] trackObjectsToLink; // Linked to trackObjects by index
		[HideInInspector] [SerializeField] private UnityEvent[] triggerUnityEvents; // Linked to triggerEvents by index

		// Vertex Variables
		private ColorOffsetType fillColorOffset; // The overwrite animation color
		private ColorOffsetType[][] spriteColorOffsets; // The overwrite sprite color
		private Color[] finalFrameVertexRenderColorsSub; // Subtractive colors for each vertex passed to shader
		private Vector2[]
			finalFrameVertexRenderColorsAddRG; // Red & Green additive colors for each vertex passed to shader
		private Vector2[]
			finalFrameVertexRenderColorsAddBA; // Blue & Alpha additive colors for each vertex passed to shader

		// Material Properties
		private Material mat;
		private Texture2D spriteSheet;
		private MeshRenderer rend;

		// Mesh properties
		private Mesh mesh;
		private string meshName = "Sprite Mesh";
		private Matrix4x4 pivotScaleOffsetMatrix; // Mesh pivot & scaler offset matrix
		private Vector3[][] finalTransformedVertices;

		// Delegates
		public delegate void OnTriggerEvent(); // Defines OnTriggerEvent  event

		private Action unimUnloadHandler; // Handles destruction of Unim
		public Action ClipStartDelegate; // TODO implement this
		public Action ClipCompleteDelegate; // TODO implement this

		// TriggerEvents
		private TriggerEvent[] triggerEvents;
		public Dictionary<int, List<TriggerEvent>>
			triggerEventByFrameNumber; // Used for invoking trigger events on played frames
		public Dictionary<string, TriggerEvent> triggerEventByTriggerName; // Used for subscribing to triggers

		// Trackers
		private Tracker[] trackers;
		private float scaleValue;
		private Vector3 trackPosOffset;

		// Frame
		public int CurrentFrame => currentFrame;
		private int currentFrame = 1;
		private int prevFrame;

		// Coroutine for animated color operations
		private Coroutine fadeFillCo;
		private Coroutine fadeSpriteCo;

		// Optimization bools
		private bool prevRenderHasAnimation = true; // Prevents processes from two consecutive blank frames.
		private bool forceRender; // Guarantees render for that frame
		private bool isCustomSheetCreated;
		private bool currentFrameHasFillCol;
		private bool prevFrameHasFillCol;
		private bool currentFrameHasSpriteCol;
		private bool prevFrameHasSpriteCol;

		// Playback parameters
		private readonly Queue<PlayableAnimation> animationQueue = new Queue<PlayableAnimation>(); // Anims to be played
		private PlayableAnimation currentAnim;
		private float elapsedFrameTime;
		private float elapsedClipTime;
		private float prevFrameRate;
		public bool IsPlaying { get; private set; }
		private int clipLoopCount;

		private float spfAbsolute;
		private float spf;
		public float Spf // Seconds per frame
		{
			get { return spf; }
			private set
			{
				spf = value;
				spfAbsolute = Mathf.Abs(spf);
			}
		}

		#endregion

		#region CLASSES AND STRUCTS

		private class ColorOffsetType
		{
			public Color colorSub;
			public Color colorAdd;
			public ColorOperation colorOperation;

			public ColorOffsetType()
			{
				colorSub = Color.white;
				colorAdd = Color.clear;
				colorOperation = ColorOperation.Combine;
			}

			public void Reset()
			{
				colorSub = Color.white;
				colorAdd = Color.clear;
				colorOperation = ColorOperation.Combine;
			}
		}

		public enum ColorOperation
		{
			Overwrite,
			Combine
		}

		public enum ScaleTypes
		{
			OnePixelPerUnit,
			ScaleToFitUnitHeight,
			ScaleToFitUnitWidth
		}

		public enum ClipUpdateMode
		{
			Time,
			UnscaledTime,
			Manual
		}

		// Playables
		public class PlayableAnimation
		{
			public UnimClip Clip { get; private set; }
			public PlayType PlayType { get; private set; }
			public int FrameOffset { get; private set; }
			public int MaxCount { get; private set; }
			public float MaxPlayTime { get; private set; }
			public Action OnStart { get; private set; }
			public Action OnEnd { get; private set; }
			public Action OnExit { get; private set; }

			public bool isInfLoop
			{
				get { return MaxCount <= 0; }
			}

			public PlayableAnimation(UnimClip clip, PlayType playType = PlayType.Loop, int startFrameOffset = 0,
				int maxCount = -1, float maxPlayTime = Mathf.Infinity, Action onStart = null, Action onEnd = null,
				Action onExit = null)
			{
				Clip = clip;
				PlayType = playType;
				FrameOffset = startFrameOffset;
				MaxCount = maxCount;
				MaxPlayTime = maxPlayTime;

				// delegates
				OnStart = onStart;
				OnEnd = onEnd;
				OnExit = onExit;
			}
		}

		public class TriggerEvent
		{
			public UnimData.Trigger trigger;
			private event OnTriggerEvent onTriggerEvent;

			public void SubscribeToTrigger(OnTriggerEvent method)
			{
				onTriggerEvent += method;
			}

			public void UnSubscribeToTrigger(OnTriggerEvent method)
			{
				onTriggerEvent -= method;
			}

			public void RunTrigger()
			{
				if (onTriggerEvent != null)
				{
					onTriggerEvent();
				}
			}
		}

		public class Tracker
		{
			public UnimData.TrackObject trackObject;
			public GameObject gameObject;
		}

		public enum PlayType
		{
			Loop,
			PlayOnce,
			SingleFrame
		}

		#endregion

		#region UNIM INITIALIZERS

		private void Awake()
		{
			Spf = fpsToSpf(frameRate); // Frames Per Second to Seconds Per Frame. Automatically sets spfAbsolute

			if (initOnAwake)
			{
				InitUnim();
			}

			if (playOnAwake)
			{
				UnimClip playAll = new UnimClip(" ALL ", 1, UnimData.MaxFrames - 1);
				BeginNewAnim(new PlayableAnimation(playAll));
			}
		}

		private void Destroy()
		{
			if (unimUnloadHandler != null)
			{
				unimUnloadHandler();
			}
		}

		public void InitUnim()
		{
			InitUnim(unimDataAsset, unimSheetAsset, loadDataAsBytes);
		}

		protected void InitUnim(TextAsset unimData, Texture2D sheet, bool loadAsBytes = false)
		{
			// Validate Unim Files
			if (unimData == null)
			{
				throw new Exception("No UnimData asset assigned!");
			}

			// Validate Sheet
			if (sheet == null)
			{
				throw new Exception("No UnimSheet asset assigned!");
			}

			// Initialize differently if in Play or Editor mode
			currentFrame = 1;
			if (rend == null)
			{
				rend = GetComponent<MeshRenderer>();
			}

			if (Application.isPlaying)
			{
				InitMain(unimData, sheet, loadAsBytes);
			}
			else
			{
				EditorInitMain(unimData, sheet, loadAsBytes);
			}
		}

		private void InitMain(TextAsset unimData, Texture2D spriteSheet, bool loadAsBytes)
		{
			UnimData = LoadUnimData(unimData, loadAsBytes);
			rend.sharedMaterial = LoadUnimMaterial(spriteSheet);
			SetSortingLayer(sortingLayerId, orderInLayer);
			InitPivotScaleOffset();
			InitTriggerEvents();
			InitTrackers();
			InitColorOperations();
			InitRenderVariables(this);
		}

		private void EditorInitMain(TextAsset file, Texture2D spriteSheet, bool loadAsBytes)
		{
			UnimData = UnimCache.Instance.LoadUnimData(file, loadAsBytes, true);
			rend.material = UnimCache.Instance.LoadUnimMaterial(spriteSheet, true);
			SetSortingLayer(sortingLayerId, orderInLayer);
			InitPivotScaleOffset();
			InitColorOperations();
			InitRenderVariables(this);
		}

		public void SetSortingLayer(int layerId, int order)
		{
			rend.sortingLayerID = layerId;
			rend.sortingOrder = order;
		}

		private void InitPivotScaleOffset()
		{
			// Reset Matrix
			pivotScaleOffsetMatrix = Matrix4x4.identity;

			// Scale Width and Height
			scaleValue = 1;
			switch (scaleMode)
			{
				case ScaleTypes.OnePixelPerUnit:
					scaleValue = 1f * scaleMultiplier;
					break;
				case ScaleTypes.ScaleToFitUnitHeight:
					scaleValue = 1f / (float) UnimData.sceneHeight * scaleMultiplier;
					break;
				case ScaleTypes.ScaleToFitUnitWidth:
					scaleValue = 1f / (float) UnimData.sceneWidth * scaleMultiplier;
					break;
			}

			pivotScaleOffsetMatrix.m03 = pivotCoordinates.x * -(UnimData.sceneWidth * scaleValue / 2); //posX;
			pivotScaleOffsetMatrix.m13 = pivotCoordinates.y * -(UnimData.sceneHeight * scaleValue / 2); //posY;

			// PivotOffset
			pivotScaleOffsetMatrix.m00 = scaleValue; //scaleX;
			pivotScaleOffsetMatrix.m11 = scaleValue; //scaleY;

			// Scale Sprite Depth
			pivotScaleOffsetMatrix.m22 = spriteDepth; //scaleZ;

			// ReCalculate Culling Bounds
			if (mesh != null)
			{
				mesh.bounds = new Bounds(
					new Vector3(pivotScaleOffsetMatrix.m03, pivotScaleOffsetMatrix.m13,
						(spriteDepth * UnimData.maxSprites) / 2),
					new Vector3(UnimData.sceneWidth * scaleValue * cullingBoundsScaler.x,
						UnimData.sceneHeight * scaleValue * cullingBoundsScaler.y, spriteDepth * UnimData.maxSprites)
				);
			}

			finalTransformedVertices = new Vector3[UnimData.frameData.Length][];
			for (int i = 0; i < UnimData.frameData.Length; i++)
			{
				finalTransformedVertices[i] = new Vector3[UnimData.frameData[0].vertTransforms.Length];
				for (int j = 0; j < UnimData.frameData[i].vertTransforms.Length; j++)
				{
					finalTransformedVertices[i][j] =
						pivotScaleOffsetMatrix.MultiplyPoint(UnimData.frameData[i].vertTransforms[j]);
				}
			}
		}

		private void InitColorOperations()
		{
			// Instantiates global color offset as: Color.clear, ColorOperation.Combine
			fillColorOffset = new ColorOffsetType();

			if (enhancedColorOperations)
			{
				// populate individual sprite specific variables
				UnimData.animSpriteUsagePositionsBySpriteName = GenerateSpriteDataBySpriteNameDict(this);

				// Instantiates an array for each animation vertex as: Color.clear, ColorOperation.Add
				spriteColorOffsets = InitSpriteColorsOffset(this);
			}
		}

		private void InitTriggerEvents()
		{
			if (UnimData.triggersExist)
			{
				// Create look up tables
				triggerEventByFrameNumber = new Dictionary<int, List<TriggerEvent>>();
				triggerEventByTriggerName = new Dictionary<string, TriggerEvent>();

				// Create triggers instance
				triggerEvents = new TriggerEvent[UnimData.triggers.Length];
				for (int i = 0; i < triggerEvents.Length; i++)
				{
					triggerEvents[i] = new TriggerEvent();
					triggerEvents[i].trigger = UnimData.triggers[i]; // Assign UnimData.trigger to triggerEvents
					int frmNum = triggerEvents[i].trigger.frame; // Store frameNum

					// Add trigger event to lookup table by frame number
					if (!triggerEventByFrameNumber.ContainsKey(frmNum))
					{
						triggerEventByFrameNumber.Add(frmNum, new List<TriggerEvent>() {triggerEvents[i]});
					}
					else
					{
						triggerEventByFrameNumber[frmNum].Add(triggerEvents[i]);
					}

					// Add trigger event to lookup table by trigger name
					if (!triggerEventByTriggerName.ContainsKey(triggerEvents[i].trigger.name))
					{
						triggerEventByTriggerName.Add(triggerEvents[i].trigger.name, triggerEvents[i]);
					}
					else
					{
						Debug.LogError("There can not be two instances of the same Trigger Name '" +
						               triggerEvents[i].trigger.name + "'");
					}
				}
			}
		}

		private void InitTrackers()
		{
			if (UnimData.trackObjectsExist)
			{
				// Set Track Offset Position for use in updates.
				trackPosOffset.x = 0.5f * UnimData.sceneWidth * scaleValue * (-pivotCoordinates.x - 1);
				trackPosOffset.y = 0.5f * UnimData.sceneHeight * scaleValue * (-pivotCoordinates.y - 1);

				// Create trackers instance
				trackers = new Tracker[UnimData.trackObjects.Length];
				for (int i = 0; i < trackers.Length; i++)
				{
					trackers[i] = new Tracker();
					trackers[i].trackObject = UnimData.trackObjects[i];

					// Create Game Track GameObject and parent it
					trackers[i].gameObject = new GameObject(UnimData.trackObjects[i].name);
					trackers[i].gameObject.transform.parent = transform;

					// If obj exists in scene parent it to TrackObj otherwise instantiate to TrackObj
					if (i < trackObjectsToLink.Length && trackObjectsToLink[i] != null)
					{
						if (trackObjectsToLink[i].scene.IsValid())
						{
							trackObjectsToLink[i].transform.parent = trackers[i].gameObject.transform;
						}
						else
						{
							Instantiate(trackObjectsToLink[i], trackers[i].gameObject.transform);
						}
					}
				}
			}
		}

		private UnimData LoadUnimData(TextAsset unimData, bool loadAsBytes)
		{
			// If animation data already exists load from file, otherwise load from cache
			UnimData u = UnimCache.Instance.LoadUnimData(unimData, loadAsBytes);
			unimUnloadHandler += () => { UnimCache.Instance.UnloadUnimData(unimData); };
			return u;
		}

		private Material LoadUnimMaterial(Texture2D spriteSheetPath)
		{
			Material m = UnimCache.Instance.LoadUnimMaterial(spriteSheetPath);
			unimUnloadHandler += () => { UnimCache.Instance.UnloadUnimMaterial(spriteSheetPath); };
			return m;
		}

		private void InitRenderVariables(UnimPlayer unimPlayer)
		{
			// create Mesh
			MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
			meshFilter.sharedMesh = CreateUnimMesh();

			// create meshes to display sprites
			mesh = GetComponent<MeshFilter>().sharedMesh;

			// allocate space for maximum number of verticies needed in animation
			finalFrameVertexRenderColorsSub = new Color[UnimData.TotalVertexCount];
			finalFrameVertexRenderColorsAddRG = new Vector2[UnimData.TotalVertexCount];
			finalFrameVertexRenderColorsAddBA = new Vector2[UnimData.TotalVertexCount];

			// Instantiate with default values
			for (int i = 0; i < UnimData.TotalVertexCount; i++)
			{
				finalFrameVertexRenderColorsSub[i] = Color.white;
				finalFrameVertexRenderColorsAddRG[i] = new Vector2(0, 0);
				finalFrameVertexRenderColorsAddBA[i] = new Vector2(0, 0);
			}
		}

		private Mesh CreateUnimMesh()
		{
			// mesh init                                                                              -1                            1
			Mesh m = new Mesh();
			m.name = meshName;

			// set initial vertices to match scene width and height. Used for culling bounds
			float px = pivotCoordinates.x;
			float py = pivotCoordinates.y;
			float sw = (float) UnimData.sceneWidth;
			float sh = (float) UnimData.sceneHeight;
			float left = -sw / 2 - px / 2 * sw;
			float right = sw / 2 - px / 2 * sw; //
			float bottom = -sh / 2 - py / 2 * sh;
			float top = sh / 2 - py / 2 * sh; //
			Vector2 cullTopLeft = new Vector2(left, top);
			Vector2 cullTopRight = new Vector2(right, top);
			Vector2 cullBottomRight = new Vector2(right, bottom);
			Vector2 cullBottomLeft = new Vector2(left, bottom);

			//Positions rest of verts in center of cull area
			Vector2 centerPoint = Vector2.Lerp(cullBottomLeft, cullTopRight, 0.5f);

			// set up vertecies for sprite mesh
			Vector3[] vertexArray = new Vector3[UnimData.TotalVertexCount];
			for (int i = 0;
				i < vertexArray.Length;
				i = i + 4) // set all sprites to be a square 1 unit mesh except for the last mesh which is set to mimic the size of the flash scene
			{
				if (i == 0)
				{
					vertexArray[i + 0] = cullTopLeft;
					vertexArray[i + 1] = cullTopRight;
					vertexArray[i + 2] = cullBottomRight;
					vertexArray[i + 3] = cullBottomLeft;
				}
				else
				{
					vertexArray[i + 0] = centerPoint;
					vertexArray[i + 1] = centerPoint;
					vertexArray[i + 2] = centerPoint;
					vertexArray[i + 3] = centerPoint;
				}
			}

			m.vertices = vertexArray;

			// setup uv's for vertecies of sprite mesh
			Vector2[] uvArray = new Vector2[UnimData.TotalVertexCount];
			for (int i = 0; i < uvArray.Length; i++)
			{
				uvArray[i] = Vector2.zero; // make all uv's reference the bottom left point of the sprite sheet.
			}

			m.uv = uvArray;

			// setup triangle faces for vertecies
			int numTrianglesPoints =
				UnimData.maxSprites * 2 * 3; // each sprite has 2  triangles. Each triangle has 3 points.
			int[] trianglePointArray = new int[numTrianglesPoints];
			int triangleCount = 0;
			for (int i = 0;
				i < trianglePointArray.Length;
				i = i + 6) // add triangles to all vertecies minus the last 4( vertecies
			{
				trianglePointArray[i] = 0 + triangleCount;
				trianglePointArray[i + 1] = 1 + triangleCount;
				trianglePointArray[i + 2] = 2 + triangleCount;

				trianglePointArray[i + 3] = 0 + triangleCount;
				trianglePointArray[i + 4] = 2 + triangleCount;
				trianglePointArray[i + 5] = 3 + triangleCount;

				triangleCount = triangleCount + 4;
			}

			m.triangles = trianglePointArray;

			// set up normals
			m.RecalculateNormals();

			return m;
		}

		private ColorOffsetType[][] InitSpriteColorsOffset(UnimPlayer unimPlayer)
		{
			ColorOffsetType[][] s = new ColorOffsetType[UnimData.MaxFrames][];
			for (int i = 0; i < s.Length; i++)
			{
				s[i] = new ColorOffsetType[UnimData.TotalVertexCount];
				for (int j = 0; j < s[i].Length; j++)
				{
					s[i][j] = new ColorOffsetType();
				}
			}

			return s;
		}

		private Dictionary<string, List<int[]>> GenerateSpriteDataBySpriteNameDict(UnimPlayer unimPlayer)
		{
			Dictionary<string, List<int[]>> dict = new Dictionary<string, List<int[]>>();

			// For each sprite key create a list of int arrays
			for (int i = 0; i < UnimData.sprites.Length; i++)
			{
				dict.Add(UnimData.sprites[i].name, new List<int[]>());
			}

			for (int i = 0; i < UnimData.animationData.Length; i++)
			{
				for (int j = 0; j < UnimData.animationData[i].Length; j++)
				{
					string imageName = UnimData.sprites[UnimData.animationData[i][j].spriteID].name;
					dict[imageName].Add(new int[2] {i, j});
				}
			}

			return dict;
		}

		#endregion

		#region PLAYBACK METHODS

		private void Update()
		{
			if (updateMode == ClipUpdateMode.Time)
			{
				UpdateClip(Time.deltaTime);
			}
			else if (updateMode == ClipUpdateMode.UnscaledTime)
			{
				UpdateClip(Time.unscaledDeltaTime);
			}
		}

		public void UpdateClip(float deltaTime)
		{
			if (prevFrameRate != frameRate)
			{
				Spf = fpsToSpf(frameRate); // Automatically sets spfAbsolute
				prevFrameRate = frameRate;
			}

			if (IsPlaying)
			{
				elapsedFrameTime += deltaTime;
				if (elapsedFrameTime >= spfAbsolute)
				{
					UpdateFrame(deltaTime);
					elapsedFrameTime -= spfAbsolute * Mathf.Floor(elapsedFrameTime / spfAbsolute);
				}
			}
		}

		private void OnWillRenderObject()
		{
			if (prevFrame != currentFrame || forceRender)
			{
				RunUnimFrame(currentFrame);
				prevFrame = currentFrame;
				forceRender = false;
			}
		}

		private int GetFrameOffsetFromOutsideClipBounds(int frame, UnimClip unimClip)
		{
			if (frame < unimClip.startFrame)
			{
				return frame - unimClip.startFrame;
			}
			else if (frame >= unimClip.endFrame)
			{
				return frame - unimClip.endFrame + 1;
			}
			else
			{
				return 0;
			}
		}

		private void EndPlayback()
		{
			IsPlaying = false;
			elapsedFrameTime = 0;
		}

		private void UpdateFrame(float deltaTime)
		{
			// Update time the clip has been playing
			elapsedClipTime += deltaTime;

			// Null check
			if (currentAnim == null)
			{
				Debug.LogError("no animation in Queue");
				return;
			}

			// Exit Clip if playing past Max Play Time
			if (elapsedClipTime >= currentAnim.MaxPlayTime)
			{
				if (animationQueue.Count > 0)
				{
					BeginNewAnim(animationQueue.Dequeue());
				}
				else
				{
					EndPlayback();
				}
			}

			// Update next frame
			int newFrame = currentFrame + (int) Mathf.Floor(elapsedFrameTime / Spf); // Increment frame
			int offset =
				GetFrameOffsetFromOutsideClipBounds(newFrame,
					currentAnim.Clip); // Returns how many frames outside of the clips designated frames

			// SingleFrame update
			if (currentAnim.PlayType == PlayType.SingleFrame)
			{
				clipLoopCount++;
				if (clipLoopCount >= currentAnim.MaxCount)
				{
					if (animationQueue.Count > 0)
					{
						BeginNewAnim(animationQueue.Dequeue());
					}
					else
					{
						EndPlayback();
					}
				}

				return;
			}

			// PlayOnce update
			else if (currentAnim.PlayType == PlayType.PlayOnce)
			{
				if (offset != 0) // If newFrame is out of Clips frame bounds
				{
					clipLoopCount++; // Increment Loop Counter

					// If an animation is available in the queue then start playing it.
					if (animationQueue.Count > 0)
					{
						BeginNewAnim(animationQueue.Dequeue());
						newFrame = currentFrame; // currentFrame is set by AssignNextAnimInQueue()
					}
					else
					{
						if (offset < 0)
						{
							newFrame = currentAnim.Clip.startFrame;
						}
						else
						{
							newFrame = currentAnim.Clip.endFrame - 1;
						}

						EndPlayback();
					}
				}
			}

			// Loop update
			else if (currentAnim.PlayType == PlayType.Loop)
			{
				if (offset != 0) // If newFrame is out of Clips frame bounds
				{
					clipLoopCount++; // Increment Loop Counter

					// If max loop count reached
					if (!currentAnim.isInfLoop && clipLoopCount >= currentAnim.MaxCount)
					{
						// If an animation is available in the queue then start playing it.
						if (animationQueue.Count > 0)
						{
							BeginNewAnim(animationQueue.Dequeue());
							newFrame = currentFrame; // currentFrame is set by AssignNextAnimInQueue()
						}
						else
						{
							if (offset > 0)
							{
								newFrame = currentAnim.Clip.endFrame;
							}
							else
							{
								newFrame = currentAnim.Clip.startFrame;
							}

							EndPlayback();
						}
					}
					else if (offset < 0)
					{
						newFrame = currentAnim.Clip.endFrame -
						           Mathf.FloorToInt((currentAnim.Clip.startFrame - newFrame) %
						                            currentAnim.Clip.duration);
					}
					else if (newFrame >= currentAnim.Clip.endFrame)
					{
						newFrame = currentAnim.Clip.startFrame +
						           Mathf.FloorToInt((newFrame - currentAnim.Clip.endFrame) %
						                            currentAnim.Clip.duration);
					}
				}
			}

			currentFrame = newFrame; // Update to next frame
		}

		public void PlayNextAnimInQueue()
		{
			if (animationQueue.Count > 0)
			{
				BeginNewAnim(animationQueue.Dequeue());
			}
			else
			{
				Debug.Log("No animations available in queue");
			}
		}

		private void BeginNewAnim(PlayableAnimation anim)
		{
			// Setting Clip to null and MaxCount to -1 triggers a stop
			if (anim.Clip == null && anim.MaxCount == -1)
			{
				Stop();
				return;
			}

			// If current animation is instantly interruptable and animation in queue
			if ((anim.MaxCount == 0 || anim.PlayType == PlayType.SingleFrame && anim.MaxCount <= 0) &&
			    animationQueue.Count > 0)
			{
				BeginNewAnim(animationQueue.Dequeue());
				return;
			}

			currentAnim = anim;
			clipLoopCount = 0; // Reset loop counter
			elapsedClipTime = 0;

			// Set first frame to play
			if (currentAnim.FrameOffset > 0)
			{
				currentFrame = currentAnim.Clip.startFrame + currentAnim.FrameOffset;
				if (currentFrame >= currentAnim.Clip.endFrame)
				{
					currentFrame = currentAnim.Clip.endFrame - 1;
					Debug.LogWarning("Attempted to offset frame out of clip range.");
				}
			}
			else if (currentAnim.FrameOffset < 0)
			{
				currentFrame = currentAnim.Clip.endFrame + currentAnim.FrameOffset - 1;
				if (currentFrame < currentAnim.Clip.startFrame)
				{
					currentFrame = currentAnim.Clip.startFrame;
					Debug.LogWarning("Attempted to offset frame out of clip range.");
				}
			}
			else
			{
				currentFrame = currentAnim.Clip.startFrame;
			}

			//  isPlaying set to false only when SingleFrame play type with 0 or less MaxCount
			if (currentAnim.PlayType == PlayType.SingleFrame && currentAnim.MaxCount <= 0)
			{
				IsPlaying = false;
			}
			else
			{
				IsPlaying = true;
			}
		}

		public void PlayClip<T>(T clipEnum, PlayType playType, int startFrameOffset = 0, int maxCount = -1,
			float maxPlayTime = Mathf.Infinity, Action onStart = null, Action onEnd = null, Action onExit = null) where T : Enum
		{
			PlayClip(Convert.ToInt32(clipEnum), playType, startFrameOffset, maxCount, maxPlayTime, onStart, onEnd, onExit);
		}
		
		public void PlayClip(string clipName, PlayType playType, int startFrameOffset = 0, int maxCount = -1,
			float maxPlayTime = Mathf.Infinity, Action onStart = null, Action onEnd = null, Action onExit = null)
		{
			if (UnimData.clipIndexByClipName.ContainsKey(clipName))
			{
				PlayClip(UnimData.clipIndexByClipName[clipName], playType, startFrameOffset, maxCount, maxPlayTime,
					onStart,
					onEnd, onExit);
			}
			else
			{
				Debug.LogError($"Clip name '{clipName}' does not exist.");
			}
		}

		public void PlayClip(int clipIndex, PlayType playType, int startFrameOffset = 0, int maxCount = -1,
			float maxPlayTime = Mathf.Infinity, Action onStart = null, Action onEnd = null, Action onExit = null)
		{
			if (clipIndex >= 0 && clipIndex < UnimData.clips.Length)
			{
				ClearAnimationQueue();
				BeginNewAnim(new PlayableAnimation(UnimData.clips[clipIndex], playType, startFrameOffset, maxCount,
					maxPlayTime, onStart,
					onEnd, onExit));
			}
			else
			{
				Debug.LogError($"Index out of clip range");
			}
		}

		public void Stop()
		{
			ClearAnim();
		}

		public void QueueStop()
		{
			animationQueue.Enqueue(new PlayableAnimation(null, maxCount: -1)); // Specific parameters to trigger stop
		}

		private void ClearAnim()
		{
			ClearAnimationQueue();
			currentAnim = null;
			clipLoopCount = 0;
			elapsedClipTime = 0;
			currentFrame = 0;
			IsPlaying = false;
		}
		
		public void QueueClip<T>(T clipEnum, PlayType playType, int startFrameOffset = 0, int maxCount = -1,
			float maxPlayTime = Mathf.Infinity, Action onStart = null, Action onEnd = null, Action onExit = null) where T : Enum
		{
			QueueClip(Convert.ToInt32(clipEnum), playType, startFrameOffset, maxCount, maxPlayTime,
				onStart, onEnd, onExit);
		}

		public void QueueClip(string clipName, PlayType playType, int startFrameOffset = 0, int maxCount = -1,
			float maxPlayTime = Mathf.Infinity, Action onStart = null, Action onEnd = null, Action onExit = null)
		{
			if (UnimData.clipIndexByClipName.ContainsKey(clipName))
			{
				QueueClip(UnimData.clipIndexByClipName[clipName], playType, startFrameOffset, maxCount, maxPlayTime,
					onStart, onEnd, onExit);
			}
			else
			{
				Debug.LogError($"Clip name '{clipName}' does not exist.");
			}
		}

		public void QueueClip(int clipIndex, PlayType playType, int startFrameOffset = 0, int maxCount = -1,
			float maxPlayTime = Mathf.Infinity, Action onStart = null, Action onEnd = null, Action onExit = null)
		{
			// Play queued animation immediately if nothing else is queued or playing
			if (animationQueue.Count <= 0 && IsPlaying == false)
			{
				PlayClip(clipIndex, playType, startFrameOffset, maxCount, maxPlayTime, onStart, onEnd, onExit);
				return;
			}

			animationQueue.Enqueue(new PlayableAnimation(UnimData.clips[clipIndex], playType, startFrameOffset,
				maxCount,
				maxPlayTime, onStart,
				onEnd, onExit));
		}

		public virtual void ClearAnimationQueue()
		{
			animationQueue.Clear();
		}

		// Frames Per Second to Seconds Per Frame
		private float fpsToSpf(float fps)
		{
			return (1f / fps);
		}

		public float GetFrameRate()
		{
			return frameRate;
		}

		public void RunUnimFrame(int frameNumber)
		{
			if (UnimData.triggersExist)
			{
				InvokeTriggers(frameNumber);
			}

			if (UnimData.trackObjectsExist)
			{
				UpdateTrackObjects(frameNumber);
			}

			RenderFrame(frameNumber);
		}


		private void UpdateTrackObjects(int frameNumber)
		{
			foreach (Tracker t in trackers)
			{
				if (t.trackObject.transformByFrameNumber.ContainsKey(frameNumber))
				{
					if (t.gameObject.activeSelf == false)
					{
						t.gameObject.SetActive(true);
					}

					Vector3 newT = new Vector3();
					newT.x = t.trackObject.transformByFrameNumber[frameNumber].position.x * scaleValue +
					         trackPosOffset.x;
					newT.y = t.trackObject.transformByFrameNumber[frameNumber].position.y * scaleValue +
					         trackPosOffset.y;
					newT.z = t.trackObject.transformByFrameNumber[frameNumber].position.z * spriteDepth;
					t.gameObject.transform.localPosition = newT;
					t.gameObject.transform.localScale =
						(Vector3) t.trackObject.transformByFrameNumber[frameNumber].scale + Vector3.forward;
					t.gameObject.transform.eulerAngles =
						Vector3.forward * t.trackObject.transformByFrameNumber[frameNumber].rotation;
				}
				else
				{
					if (t.gameObject.activeSelf == true)
					{
						t.gameObject.SetActive(false);
					}
				}
			}
		}

		private void InvokeTriggers(int frameNumber)
		{
			if (triggerEventByFrameNumber.ContainsKey(frameNumber))
			{
				foreach (TriggerEvent t in triggerEventByFrameNumber[frameNumber])
				{
					// Run Delegate
					t.RunTrigger();

					// Run UnityEvent
					if (triggerUnityEvents[t.trigger.index] != null)
					{
						triggerUnityEvents[t.trigger.index].Invoke();
					}
				}
			}
		}

		#endregion

		// TODO support UNIM Bytes integration

		#region BYTE INTEGRATION

		// Byte file manipulation
		class ByteManipulation
		{
			/// <summary>
			/// Converts 4 bytes into an <see cref="int"/>.
			/// </summary>
			/// <param name="bytes">Reference to a byte array.</param>
			/// <param name="pointer">Reference to the pointer that should point to the first byte of the integer. This will be increased by 4.</param>
			/// <returns>The newly create int</returns>
			public static int ToInt(ref byte[] bytes, ref int pointer)
			{
				int val = BitConverter.ToInt32(bytes, pointer);
				pointer += 4;
				return val;
			}

			/// <summary>
			/// Converts a byte into a <see cref="char"/>
			/// </summary>
			/// <param name="bytes">Reference to a byte array.</param>
			/// <param name="pointer">Reference to the pointer that should point to the first byte of the integer. This will be increased by 1.</param>
			/// <returns>The newly create char</returns>
			public static char ToChar(ref byte[] bytes, ref int pointer)
			{
				char val = BitConverter.ToChar(bytes, pointer);
				pointer += 1;
				return val;
			}

			/// <summary>
			/// Converts 4 bytes into a <see cref="float"/>
			/// </summary>
			/// <param name="bytes">Reference to a byte array.</param>
			/// <param name="pointer">Reference to the pointer that should point to the first byte of the integer. This will be increased by 4.</param>
			/// <returns>The newly create char</returns>
			public static float ToFloat(ref byte[] bytes, ref int pointer)
			{
				float val = BitConverter.ToSingle(bytes, pointer);
				pointer += 4;
				return val;
			}

			/// <summary>
			/// Converts a set of bytes into a <see cref="string"/>
			/// </summary>
			/// <param name="bytes">Reference to a byte array.</param>
			/// <param name="pointer">Reference to the pointer that should point to the first byte of the integer. This will be increased by the passed size.</param>
			/// <param name="size">The size of the string to extract from the bytes.</param>
			/// <returns>The newly create string</returns>
			public static string ToString(ref byte[] bytes, ref int pointer, int size)
			{
				string val = BitConverter.ToString(bytes, pointer, size);
				pointer += size;

				string[] splitBytes = val.Split('-');

				StringBuilder builder = new StringBuilder();
				for (int i = 0; i < splitBytes.Length; i++)
				{
					char newChar = (char) Int16.Parse("00" + splitBytes[i], NumberStyles.AllowHexSpecifier);
					builder.Append(newChar);
				}

				return builder.ToString();
			}


			/// <summary>
			/// Converts a byte into a <see cref="bool"/>
			/// </summary>
			/// <param name="bytes">Reference to a byte array.</param>
			/// <param name="pointer">Reference to the pointer that should point to the first byte of the integer. This will be increased by 1.</param>
			/// <returns>The newly create bool</returns>
			public static bool ToBool(ref byte[] bytes, ref int pointer)
			{
				bool bVal = BitConverter.ToBoolean(bytes, pointer);
				pointer += 1;
				return bVal;
			}
		}

		#endregion

		// TODO Set up init from bytes

		#region INIT FROM BYTES

		/*
		/// <summary>
		/// Extracts Unim data from a series of bytes. The bytes must be generated by the <i>unim-tools</i> python tool.
		/// </summary>
		/// <param name="unimBytes">The bytes to process.</param>
		/// <param name="centreCoordX">The x position for the center point.</param>
		/// <param name="centreCoordY">The y position for the center point.</param>
		private void InitUnimDataFromBytes(string bytesFilePath, float centreCoordX, float centreCoordY)
		{
			// Load bytes from file or cache
			byte[] unimBytes = ResourceCache.Instance.LoadBytes(bytesFilePath);
			unimUnloadHandler += () => { ResourceCache.Instance.UnloadBytes(bytesFilePath); };
	
			// Get all Unim data from the bytes file
			int dataPointer = 0;
			UnimAnim anim = new UnimAnim()
			{
				maxSprites = ByteManipulation.ToInt(ref unimBytes, ref dataPointer),
				sheetWidth = ByteManipulation.ToInt(ref unimBytes, ref dataPointer),
				sheetHeight = ByteManipulation.ToInt(ref unimBytes, ref dataPointer),
				sceneWidth = ByteManipulation.ToInt(ref unimBytes, ref dataPointer),
				sceneHeight = ByteManipulation.ToInt(ref unimBytes, ref dataPointer)
			};
	
			if (anim.sheetWidth <= 0)
			{
				Debug.LogWarning(string.Format("Sheet width is invalid: {0}", anim.sheetWidth));
				anim.sheetWidth = 1;
			}
	
			if (anim.sheetHeight <= 0)
			{
				Debug.LogWarning(string.Format("Sheet height is invalid: {0}", anim.sheetHeight));
				anim.sheetHeight = 1;
			}
	
			ReadSheetImagesFromBytes(ref unimBytes, ref dataPointer, ref anim);
			ReadFramesFromBytes(ref unimBytes, ref dataPointer, ref anim);
	
			ProcessSpriteData(anim);
			ProcessAnimData(anim, centreCoordX, centreCoordY);
	
			// individual sprite coloring
			animSpriteUsagePositionsBySpriteName = CreateSpriteUsagePositionsBySpriteNameDict(anim);
			individualAnimSpriteColorOffsets = InitIndividualAnimSpriteColorsOffset(unimAnim);
		}
	
		/// <summary>
		/// Extracts information for each image packed in the texture sheet.
		/// </summary>
		/// <param name="bytes">Reference to the bytes currently being processed.</param>
		/// <param name="dataPointer">Reference to the current point in the byte array.</param>
		/// <param name="anim">Reference to the final Unim data.</param>
		private void ReadSheetImagesFromBytes(ref byte[] bytes, ref int dataPointer, ref UnimAnim anim)
		{
			int arrLength = ByteManipulation.ToInt(ref bytes, ref dataPointer);
	
			UnimAnim.Sprite[] sheets = new UnimAnim.Sprite[arrLength];
			for (int i = 0; i < arrLength; i++)
			{
				sheets[i] = new UnimAnim.Sprite()
				{
					i = ByteManipulation.ToInt(ref bytes, ref dataPointer), // Sprite index
					name = ByteManipulation.ToString(ref bytes, ref dataPointer, 25), // Sprite name
					rotated = ByteManipulation.ToBool(ref bytes, ref dataPointer), // Rotation
					x = ByteManipulation.ToInt(ref bytes, ref dataPointer), // X position
					y = ByteManipulation.ToInt(ref bytes, ref dataPointer), // Y position
					width = ByteManipulation.ToInt(ref bytes, ref dataPointer), // Width
					height = ByteManipulation.ToInt(ref bytes, ref dataPointer) // Height
				};
			}
	
			anim.sprites = sheets;
		}
	
		/// <summary>
		/// Reads information for all frames of animation for Unim currently being processed.
		/// </summary>
		/// <param name="bytes">Reference to the bytes currently being processed.</param>
		/// <param name="dataPointer">Reference to the current point in the byte array.</param>
		/// <param name="anim">Reference to the final Unim data.</param>
		private void ReadFramesFromBytes(ref byte[] bytes, ref int dataPointer, ref UnimAnim anim)
		{
			// Get the frame array length
			int arrLength = ByteManipulation.ToInt(ref bytes, ref dataPointer);
	
			// Create a new array that is the same size of the frame count
			UnimAnim.AnimFrame[] frames = new UnimAnim.AnimFrame[arrLength];
			for (int i = 0; i < arrLength; i++)
			{
				// Retrieve the size of the sprite array for the frame
				int spriteArrLength = ByteManipulation.ToInt(ref bytes, ref dataPointer);
				frames[i] = new UnimAnim.AnimFrame();
				frames[i].s = new UnimAnim.AnimationFrame[spriteArrLength];
				for (int j = 0; j < spriteArrLength; j++)
				{
					frames[i].s[j] = new UnimAnim.AnimationFrame();
					frames[i].s[j].i = ByteManipulation.ToInt(ref bytes, ref dataPointer); // Sprite index
					frames[i].s[j].r = ByteManipulation.ToFloat(ref bytes, ref dataPointer); // Red
					frames[i].s[j].g = ByteManipulation.ToFloat(ref bytes, ref dataPointer); // Green
					frames[i].s[j].b = ByteManipulation.ToFloat(ref bytes, ref dataPointer); // Blue
					frames[i].s[j].a = ByteManipulation.ToFloat(ref bytes, ref dataPointer); // Alpha
					frames[i].s[j].sx = ByteManipulation.ToFloat(ref bytes, ref dataPointer);
					frames[i].s[j].sy = ByteManipulation.ToFloat(ref bytes, ref dataPointer);
					frames[i].s[j].qx = ByteManipulation.ToFloat(ref bytes, ref dataPointer);
					frames[i].s[j].qy = ByteManipulation.ToFloat(ref bytes, ref dataPointer);
					frames[i].s[j].px = ByteManipulation.ToFloat(ref bytes, ref dataPointer);
					frames[i].s[j].py = ByteManipulation.ToFloat(ref bytes, ref dataPointer);
					frames[i].s[j].pz = ByteManipulation.ToFloat(ref bytes, ref dataPointer);
				}
			}
	
			anim.animationFrames = frames;
		}
	*/

		#endregion

		#region COLOR OPERATIONS

		public void SetFillColor(Color color, bool isAdditive = false,
			ColorOperation colorOperation = ColorOperation.Combine, bool simplifyAlpha = true)
		{
			if (isAdditive)
			{
				SetFillColor(null, color, colorOperation, simplifyAlpha);
			}
			else
			{
				SetFillColor(color, null, colorOperation, simplifyAlpha);
			}
		}

		public void SetFillColor(Color? colorSubtractive = null, Color? colorAdditive = null,
			ColorOperation colorOperation = ColorOperation.Combine, bool simplifyAlpha = true)
		{
			Color sub = colorSubtractive ?? Color.white; // if null assign default color
			Color add;
			if (simplifyAlpha)
			{
				add = colorAdditive ?? Color.black; // if null assign default color
				sub.a = Mathf.Min(add.a, sub.a);
				add.a = 0;
			}
			else
			{
				add = colorAdditive ?? Color.clear; // if null assign default color
			}

			fillColorOffset.colorSub = sub;
			fillColorOffset.colorAdd = add;
			fillColorOffset.colorOperation = colorOperation;
			forceRender = true;

			// If fill color is same as default then current frame doesn't have to render fill color
			if (sub == Color.white && add == Color.clear && colorOperation == ColorOperation.Combine)
			{
				currentFrameHasFillCol = false;
			}
			else
			{
				currentFrameHasFillCol = true;
			}
		}

		public void SetSpriteColor(string spriteName, Color color, bool isAdditive = false,
			ColorOperation colorOperation = ColorOperation.Combine, bool simplifyAlpha = true)
		{
			if (isAdditive)
			{
				SetSpriteColor(spriteName, null, color, colorOperation, simplifyAlpha);
			}
			else
			{
				SetSpriteColor(spriteName, color, null, colorOperation, simplifyAlpha);
			}
		}

		public void SetSpriteColor(string spriteName, Color? colorSubtractive = null, Color? colorAdditive = null,
			ColorOperation colorOperation = ColorOperation.Combine, bool simplifyAlpha = true)
		{
			Color sub = colorSubtractive ?? Color.white; // if null assign default color
			Color add;
			if (simplifyAlpha)
			{
				add = colorAdditive ?? Color.black; // if null assign default color
				sub.a = Mathf.Min(add.a, sub.a);
				add.a = 0;
			}
			else
			{
				add = colorAdditive ?? Color.clear; // if null assign default color
			}

			PopulateSpriteColorOffset(spriteName, sub, add, colorOperation);
		}

		private void PopulateSpriteColorOffset(string spriteName, Color colorSubtractive, Color colorAdditive,
			ColorOperation colorOperation = ColorOperation.Combine)
		{
			if (!UnimData.animSpriteUsagePositionsBySpriteName.ContainsKey(spriteName))
			{
				Debug.LogWarning($"There is no sprite named '{spriteName}' in this animation.");
				return;
			}

			int frame;
			int sprite;
			List<int[]> animSpriteUsagePositions = UnimData.animSpriteUsagePositionsBySpriteName[spriteName];
			for (int i = 0; i < animSpriteUsagePositions.Count; i++)
			{
				frame = animSpriteUsagePositions[i][0];
				sprite = animSpriteUsagePositions[i][1] * 4;

				// Set color for each vert on the sprite quad
				spriteColorOffsets[frame][sprite + 0].colorSub = colorSubtractive;
				spriteColorOffsets[frame][sprite + 1].colorSub = colorSubtractive;
				spriteColorOffsets[frame][sprite + 2].colorSub = colorSubtractive;
				spriteColorOffsets[frame][sprite + 3].colorSub = colorSubtractive;

				spriteColorOffsets[frame][sprite + 0].colorAdd = colorAdditive;
				spriteColorOffsets[frame][sprite + 1].colorAdd = colorAdditive;
				spriteColorOffsets[frame][sprite + 2].colorAdd = colorAdditive;
				spriteColorOffsets[frame][sprite + 3].colorAdd = colorAdditive;

				// Set color operation type for each vert on the sprite quad
				spriteColorOffsets[frame][sprite + 0].colorOperation = colorOperation;
				spriteColorOffsets[frame][sprite + 1].colorOperation = colorOperation;
				spriteColorOffsets[frame][sprite + 2].colorOperation = colorOperation;
				spriteColorOffsets[frame][sprite + 3].colorOperation = colorOperation;
			}

			currentFrameHasSpriteCol = true;
		}

		public void ResetFillColorOffset()
		{
			fillColorOffset.Reset();
			currentFrameHasFillCol = false;
		}

		public void ResetSpriteColorsOffset()
		{
			if (spriteColorOffsets == null)
			{
				return;
			}

			for (int i = 0; i < spriteColorOffsets.Length; i++)
			{
				for (int j = 0; j < spriteColorOffsets[i].Length; j++)
				{
					spriteColorOffsets[i][j].Reset();
				}
			}

			currentFrameHasSpriteCol = false;
		}

		// Aniimated Color Operations
		public void FadeFillColor(Color startColor, Color endColor, float duration, float delay = 0,
			bool isAdditive = false, ColorOperation colorOperation = ColorOperation.Combine, bool simplifyAlpha = true,
			Action onComplete = null)
		{
			if (isAdditive)
			{
				FadeFillColor(null, null, startColor, endColor, duration, delay, colorOperation, simplifyAlpha,
					onComplete);
			}
			else
			{
				FadeFillColor(startColor, endColor, null, null, duration, delay, colorOperation, simplifyAlpha,
					onComplete);
			}
		}

		public void FadeFillColor(Color? startColorSubtractive = null, Color? endColorSubtractive = null,
			Color? startColorAdditive = null, Color? endColorAdditive = null, float duration = 1, float delay = 0,
			ColorOperation colorOperation = ColorOperation.Combine, bool simplifyAlpha = true, Action onComplete = null)
		{
			Color sSub = startColorSubtractive ?? Color.white; // if null assign default color
			Color eSub = endColorSubtractive ?? Color.white; // if null assign default color

			Color sAdd;
			Color eAdd;
			if (simplifyAlpha)
			{
				sAdd = startColorAdditive ?? Color.black; // if null assign default color
				eAdd = endColorAdditive ?? Color.black; // if null assign default color
				sSub.a = Mathf.Min(sAdd.a, sSub.a);
				eSub.a = Mathf.Min(eAdd.a, eSub.a);
				sAdd.a = 0;
				eAdd.a = 0;
			}
			else
			{
				sAdd = startColorAdditive ?? Color.clear; // if null assign default color
				eAdd = endColorAdditive ?? Color.clear; // if null assign default color
			}

			if (fadeFillCo != null)
			{
				StopCoroutine(fadeFillCo);
			}

			fadeFillCo = StartCoroutine(FadeFillColorCoroutine(sSub, eSub, sAdd, eAdd, duration, delay, colorOperation,
				simplifyAlpha, onComplete));
		}

		private IEnumerator FadeFillColorCoroutine(Color startColorSubtractive, Color endColorSubtractive,
			Color startColorAdditive, Color endColorAdditive, float duration, float delay = 0,
			ColorOperation colorOperation = ColorOperation.Combine, bool simplifyAlpha = true, Action onComplete = null)
		{
			if (delay > 0)
			{
				yield return new WaitForSeconds(delay);
			}

			for (float colorFadeTime = 0; colorFadeTime < duration; colorFadeTime += Time.deltaTime)
			{
				float t = colorFadeTime / duration;
				SetFillColor(Color.Lerp(startColorSubtractive, endColorSubtractive, t),
					Color.Lerp(startColorAdditive, endColorAdditive, t),
					colorOperation,
					false);
				yield return null;
			}

			SetFillColor(endColorSubtractive, endColorAdditive, colorOperation, false);

			if (onComplete != null)
			{
				onComplete();
			}

			yield return null;
		}

		public void FadeSpriteColor(string name, Color startColor, Color endColor, float duration, float delay = 0,
			bool isAdditive = false, ColorOperation colorOperation = ColorOperation.Combine, bool simplifyAlpha = true,
			Action onComplete = null)
		{
			if (isAdditive)
			{
				FadeSpriteColor(name, null, null, startColor, endColor, duration, delay, colorOperation, simplifyAlpha,
					onComplete);
			}
			else
			{
				FadeSpriteColor(name, startColor, endColor, null, null, duration, delay, colorOperation, simplifyAlpha,
					onComplete);
			}
		}

		public void FadeSpriteColor(string name, Color? startColorSubtractive = null, Color? endColorSubtractive = null,
			Color? startColorAdditive = null, Color? endColorAdditive = null, float duration = 1, float delay = 0,
			ColorOperation colorOperation = ColorOperation.Combine, bool simplifyAlpha = true, Action onComplete = null)
		{
			Color sSub = startColorSubtractive ?? Color.white; // if null assign default color
			Color eSub = endColorSubtractive ?? Color.white; // if null assign default color

			Color sAdd;
			Color eAdd;
			if (simplifyAlpha)
			{
				sAdd = startColorAdditive ?? Color.black; // if null assign default color
				eAdd = endColorAdditive ?? Color.black; // if null assign default color
				sSub.a = Mathf.Min(sAdd.a, sSub.a);
				eSub.a = Mathf.Min(eAdd.a, eSub.a);
				sAdd.a = 0;
				eAdd.a = 0;
			}
			else
			{
				sAdd = startColorAdditive ?? Color.clear; // if null assign default color
				eAdd = endColorAdditive ?? Color.clear; // if null assign default color
			}

			if (fadeSpriteCo != null)
			{
				StopCoroutine(fadeSpriteCo);
			}

			fadeSpriteCo = StartCoroutine(FadeSpriteColorCoroutine(name, sSub, eSub, sAdd, eAdd, duration, delay,
				colorOperation, simplifyAlpha, onComplete));
		}

		private IEnumerator FadeSpriteColorCoroutine(string name, Color startColorSubtractive,
			Color endColorSubtractive,
			Color startColorAdditive, Color endColorAdditive, float duration, float delay = 0,
			ColorOperation colorOperation = ColorOperation.Combine, bool simplifyAlpha = true, Action onComplete = null)
		{
			if (delay > 0)
			{
				yield return new WaitForSeconds(delay);
			}

			for (float colorFadeTime = 0; colorFadeTime < duration; colorFadeTime += Time.deltaTime)
			{
				float t = colorFadeTime / duration;
				SetSpriteColor(name, Color.Lerp(startColorSubtractive, endColorSubtractive, t),
					Color.Lerp(startColorAdditive, endColorAdditive, t),
					colorOperation,
					false);
				yield return null;
			}

			SetSpriteColor(name, endColorSubtractive, endColorAdditive, colorOperation, false);

			if (onComplete != null)
			{
				onComplete();
			}

			yield return null;
		}

		#endregion

		#region RENDER STUFF

		public void RenderFrame(int frameNumber)
		{
			currentFrame = frameNumber;
			Render();
		}

		private void Render()
		{
			// If current frame is out of range do nothing
			if (currentFrame < 0 || currentFrame >= UnimData.MaxFrames)
			{
				currentFrame = Mathf.Clamp(currentFrame, 0, UnimData.MaxFrames - 1);
				Debug.LogWarning("Outside of Unim frame range.");
			}

			// Only render if current frame has something to render and something was rendered previously
			if (UnimData.frameData[currentFrame].vertTransforms.Length > 0 || prevRenderHasAnimation)
			{
				GenerateFinalRenderColors();
				UpdateMesh();
				prevRenderHasAnimation = UnimData.frameData[currentFrame].vertTransforms.Length > 0 ? true : false;
			}
		}

		void GenerateFinalRenderColors()
		{
			// If no fill color operations called then fill sprite with exported colors
			if (!prevFrameHasFillCol && !currentFrameHasFillCol)
			{
				for (int i = 0; i < finalFrameVertexRenderColorsSub.Length; i++)
				{
					if (UnimData.colorOperationsCombined)
					{
						finalFrameVertexRenderColorsSub[i] = fillColorOffset.colorSub;
					}
					else
					{
						finalFrameVertexRenderColorsSub[i] =
							UnimData.frameData[currentFrame].vertColors[i] * fillColorOffset.colorSub;
					}

					finalFrameVertexRenderColorsAddRG[i][0] =
						UnimData.frameData[currentFrame].vertColorsAdd[i].r;
					finalFrameVertexRenderColorsAddRG[i][1] =
						UnimData.frameData[currentFrame].vertColorsAdd[i].g;
					finalFrameVertexRenderColorsAddBA[i][0] =
						UnimData.frameData[currentFrame].vertColorsAdd[i].b;
					finalFrameVertexRenderColorsAddBA[i][1] =
						UnimData.frameData[currentFrame].vertColorsAdd[i].a;
				}
			}
			// If fill color operation is needed then process colors
			else
			{
				for (int i = 0; i < finalFrameVertexRenderColorsSub.Length; i++)
				{
					// Fill Colors
					if (fillColorOffset.colorOperation == ColorOperation.Overwrite || !UnimData.colorsExported)
					{
						finalFrameVertexRenderColorsSub[i] = fillColorOffset.colorSub;
						finalFrameVertexRenderColorsAddRG[i][0] = fillColorOffset.colorAdd.r;
						finalFrameVertexRenderColorsAddRG[i][1] = fillColorOffset.colorAdd.g;
						finalFrameVertexRenderColorsAddBA[i][0] = fillColorOffset.colorAdd.b;
						finalFrameVertexRenderColorsAddBA[i][1] = fillColorOffset.colorAdd.a;
					}
					else if (fillColorOffset.colorOperation == ColorOperation.Combine)
					{
						if (UnimData.colorOperationsCombined)
						{
							finalFrameVertexRenderColorsSub[i] = fillColorOffset.colorSub;
						}
						else
						{
							finalFrameVertexRenderColorsSub[i] =
								UnimData.frameData[currentFrame].vertColors[i] * fillColorOffset.colorSub;
						}

						finalFrameVertexRenderColorsAddRG[i][0] =
							UnimData.frameData[currentFrame].vertColorsAdd[i].r + fillColorOffset.colorAdd.r;
						finalFrameVertexRenderColorsAddRG[i][1] =
							UnimData.frameData[currentFrame].vertColorsAdd[i].g + fillColorOffset.colorAdd.g;
						finalFrameVertexRenderColorsAddBA[i][0] =
							UnimData.frameData[currentFrame].vertColorsAdd[i].b + fillColorOffset.colorAdd.b;
						finalFrameVertexRenderColorsAddBA[i][1] =
							UnimData.frameData[currentFrame].vertColorsAdd[i].a + fillColorOffset.colorAdd.a;
					}
				}
			}

			if (enhancedColorOperations && (prevFrameHasSpriteCol || currentFrameHasSpriteCol))
			{
				for (int i = 0; i < finalFrameVertexRenderColorsSub.Length; i++)
				{
					switch (spriteColorOffsets[currentFrame][i].colorOperation)
					{
						case ColorOperation.Overwrite:
							finalFrameVertexRenderColorsSub[i] = spriteColorOffsets[currentFrame][i].colorSub;
							finalFrameVertexRenderColorsAddRG[i][0] = spriteColorOffsets[currentFrame][i].colorAdd.r;
							finalFrameVertexRenderColorsAddRG[i][1] = spriteColorOffsets[currentFrame][i].colorAdd.g;
							finalFrameVertexRenderColorsAddBA[i][0] = spriteColorOffsets[currentFrame][i].colorAdd.b;
							finalFrameVertexRenderColorsAddBA[i][1] = spriteColorOffsets[currentFrame][i].colorAdd.a;
							break;
						case ColorOperation.Combine:
							finalFrameVertexRenderColorsSub[i] *= spriteColorOffsets[currentFrame][i].colorSub;
							finalFrameVertexRenderColorsAddRG[i][0] += spriteColorOffsets[currentFrame][i].colorAdd.r;
							finalFrameVertexRenderColorsAddRG[i][1] += spriteColorOffsets[currentFrame][i].colorAdd.g;
							finalFrameVertexRenderColorsAddBA[i][0] += spriteColorOffsets[currentFrame][i].colorAdd.b;
							finalFrameVertexRenderColorsAddBA[i][1] += spriteColorOffsets[currentFrame][i].colorAdd.a;
							break;
					}
				}
			}

			prevFrameHasFillCol = currentFrameHasFillCol;
			prevFrameHasSpriteCol = currentFrameHasSpriteCol;
		}

		void UpdateMesh()
		{
			mesh.vertices = finalTransformedVertices[currentFrame];
			mesh.uv = UnimData.frameData[currentFrame].vertUVs;
			mesh.colors = finalFrameVertexRenderColorsSub; // Subtractive Color
			mesh.uv7 = finalFrameVertexRenderColorsAddRG; // Additive Color
			mesh.uv8 = finalFrameVertexRenderColorsAddBA; // Additive Color
		}

		#endregion

		#region CUSTOMIZE SPRITES

		public void AssignSpriteToSpriteSheet(string spriteName, Texture2D image)
		{
			if (!UnimData.spriteIdByName.ContainsKey(spriteName))
			{
				Debug.LogError($"Sprite name '{spriteName}' does not exist. ");
				return;
			}

			AssignSpriteToSpriteSheet(UnimData.spriteIdByName[spriteName], image);
		}

		public void AssignSpriteToSpriteSheet(int spriteId, Texture2D image)
		{
			if (spriteId >= UnimData.sprites.Length || spriteId < 0)
			{
				Debug.LogError($"Sprite ID {spriteId} is out of range.");
				return;
			}

			UnimData.Sprite s = UnimData.sprites[spriteId];

			if (s.rotated && (s.width != image.height || s.height != image.width) ||
			    !s.rotated && (s.width != image.width || s.height != image.height))
			{
				Debug.LogError(
					$"Image dimensions ({image.width} x {image.height}) does not match to sprite dimensions for {s.name} ({s.width} x {s.height}).");
				return;
			}

			if (!isCustomSheetCreated)
			{
				spriteSheet = CreateEditableTexture();
			}

			if (s.rotated)
			{
				for (int y = 0; y < image.height; y++)
				{
					for (int x = 0; x < image.width; x++)
					{
						spriteSheet.SetPixel(s.x + y, s.y - x, image.GetPixel(x, y));
					}
				}
			}
			else
			{
				for (int y = 0; y < image.height; y++)
				{
					for (int x = 0; x < image.width; x++)
					{
						spriteSheet.SetPixel(s.x + x, UnimData.sheetHeight - s.y - s.height + y, image.GetPixel(x, y));
					}
				}
			}

			spriteSheet.Apply();
		}

		public void ClearSpriteSheet()
		{
			if (!isCustomSheetCreated)
			{
				spriteSheet = CreateEditableTexture();
			}

			for (int x = 0; x < spriteSheet.width; x++)
			{
				for (int y = 0; y < spriteSheet.height; y++)
				{
					spriteSheet.SetPixel(x, y, Color.clear);
				}
			}

			spriteSheet.Apply();
		}

		private Texture2D CreateEditableTexture()
		{
			Texture t = rend.sharedMaterial.mainTexture;
			// Create a temporary RenderTexture of the same size as the texture
			RenderTexture tmp = RenderTexture.GetTemporary(
				t.width,
				t.height,
				0,
				RenderTextureFormat.Default,
				RenderTextureReadWrite.Linear);

			// Blit the pixels on texture to the RenderTexture
			Graphics.Blit(t, tmp);
			// Backup the currently set RenderTexture
			RenderTexture previous = RenderTexture.active;
			// Set the current RenderTexture to the temporary one we created
			RenderTexture.active = tmp;
			// Create a new readable Texture2D to copy the pixels to it
			Texture2D myTexture2D = new Texture2D(t.width, t.height);
			// Copy the pixels from the RenderTexture to the new Texture
			myTexture2D.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
			myTexture2D.Apply();
			// Reset the active RenderTexture
			RenderTexture.active = previous;
			// Release the temporary RenderTexture
			RenderTexture.ReleaseTemporary(tmp);

			rend.material.mainTexture = myTexture2D;

			isCustomSheetCreated = true;

			// "myTexture2D" now has the same pixels from "texture" and it's readable.
			return myTexture2D;
		}

		#endregion

		#region TRIGGER METHODS

		public void SubscribeToTrigger(string triggerName, OnTriggerEvent method)
		{
			triggerEventByTriggerName[triggerName].SubscribeToTrigger(method);
		}

		public void UnSubscribeFromTrigger(string triggerName, OnTriggerEvent method)
		{
			triggerEventByTriggerName[triggerName].UnSubscribeToTrigger(method);
		}

		#endregion

		#region OTHERS

		public UnimClip GetClipByClipIndex(int clipIndex)
		{
			return UnimData.clips[clipIndex];
		}

		public List<string> GetListOfClips()
		{
			List<string> c = new List<string>();
			foreach (UnimClip e in UnimData.clips)
			{
				c.Add(e.name);
			}

			return c;
		}

		public List<string> GetListOfSprites()
		{
			List<string> s = new List<string>();
			foreach (var e in UnimData.sprites)
			{
				s.Add(e.name);
			}

			return s;
		}

		public bool AllUnimInputFilesLinked()
		{
			return unimDataAsset != null && unimSheetAsset != null;
		}

		public bool IsLoaded()
		{
			bool result = UnimData != null && UnimData.clips != null;
			return result;
		}

		public float GetClipDurationByClipName(string clipName)
		{
			if (UnimData.clipIndexByClipName.ContainsKey(clipName))
			{
				return GetClipDuration(UnimData.clipIndexByClipName[clipName]);
			}
			else
			{
				Debug.LogError($"The clip name {clipName} does not exist.");
				return 0;
			}
		}

		public float GetClipDuration(int clipIndex)
		{
			float duration;
			if (clipIndex > 0 && clipIndex < UnimData.clips.Length)
			{
				duration = UnimData.clips[clipIndex].duration * Spf;
			}
			else
			{
				Debug.LogError($"The clip index is out of range.");
				duration = 0;
			}

			return duration;
		}

		// TODO need to support offset frames
		public float GetClipCompleteProgress()
		{
			float currentFrameProgress = elapsedFrameTime / spfAbsolute; // How far along till next frame.
			float elapsedClipFramesFloat =
				(CurrentFrame - currentAnim.Clip.startFrame) +
				currentFrameProgress; // Number of frames passed in animation
			return (elapsedClipFramesFloat / currentAnim.Clip.duration);
		}
		
		public bool ClipExists(string clip)
		{
			return UnimData.clipIndexByClipName.ContainsKey(clip);
		}

		#endregion
	}

	public class UnimData
	{
		// Serialized attributes
		[fsProperty] public int sceneWidth; // canvas width of main scene
		[fsProperty] public int sceneHeight; // canvas height of main scene
		[fsProperty] public int sheetWidth; // sprite sheet width
		[fsProperty] public int sheetHeight; // sprite sheet height
		[fsProperty] public int maxSprites; // max sprites used in any given frame on animation
		[fsProperty] public bool colorsExported;
		[fsProperty] public bool colorOperationsCombined;
		[fsProperty] public UnimClip[] clips;
		[fsProperty] public Trigger[] triggers;
		[fsProperty] public Sprite[] sprites; // sprite sheet image data
		[fsProperty] public AnimationData[][] animationData; // all frames in animation
		[fsProperty] public TrackObject[] trackObjects;

		// Version
		private string version = "2020.1";

		// Lookups
		public Dictionary<string, int> clipIndexByClipName;
		public Dictionary<string, List<int[]>> animSpriteUsagePositionsBySpriteName;
		public Dictionary<string, int> spriteIdByName;

		// Vertex Variables
		public FrameData[] frameData; // Contains vertex data for every frame of a Unim animation
		public int MaxFrames { get; private set; }
		public int TotalVertexCount => maxSprites * 4; // Total vertecies in entire Unim Mesh
		public bool trackObjectsExist;
		public bool triggersExist;

		// Serialized object types
		public class AnimationData
		{
			public int spriteID; // image ID used by sprite
			public Color color;
			public Color colorAdd;
			public float scaleX; // scale X
			public float scaleY; // scale Y
			public float skewX; // skew x
			public float skewY; // skew y
			public float positionX; // position x
			public float positionY; // position y
			public float positionZ; // depth
		};

		public class FrameData
		{
			// vertex manipulators
			public Vector3[] vertTransforms;
			public Color[] vertColors;
			public Color[] vertColorsAdd;
			public int[] vertSpriteIDs;
			public Vector2[] vertUVs;
		}


		public class Sprite
		{
			public int id; // image ID
			public string name; // name of sprite
			public int x; // image pixel distance from left of sheet
			public int y; // image pixel distance from bottom of sheet
			public int width; // image pixel width
			public int height; // image pixel height
			public bool rotated; // is image rotated on sprite sheet to perserve image space
			public Vector2 topLeft;
			public Vector2 topRight;
			public Vector2 bottomRight;
			public Vector2 bottomLeft;
		};

		public class TrackObject
		{
			public string name; // name
			public TrackTransform[] transform; // track
			public Dictionary<int, TrackTransform> transformByFrameNumber;

			public class TrackTransform
			{
				public int frame; // frame number
				public Vector3 position;
				public Vector2 scale;
				public float rotation; // rotation
			}
		}


		public class Trigger
		{
			public int frame;
			public string name;
			public int index;
		}

		public void ProcessBaseAnimData()
		{
			ProcessSpriteData();
			ProcessAnimData();
			ProcessClips();
		}

		public void ProcessExtendedAnimData()
		{
			ProcessTriggerEvents();
			ProcessTrackingObjects();
		}


		private void ProcessClips()
		{
			clipIndexByClipName = new Dictionary<string, int>();
			for (int i = 0; i < clips.Length; i++)
			{
				clipIndexByClipName.Add(clips[i].name, i);
			}
		}

		private void ProcessSpriteData()
		{
			spriteIdByName = new Dictionary<string, int>();
			MaxFrames = animationData.Length;

			for (int i = 0; i < sprites.Length; i++)
			{
				float left = (float) sprites[i].x / (float) sheetWidth;
				float top = ((float) sheetHeight - (float) sprites[i].y) / (float) sheetHeight;
				float right = ((float) sprites[i].x + (float) sprites[i].width) / (float) sheetWidth;
				float bottom = ((float) sheetHeight - ((float) sprites[i].y + (float) sprites[i].height)) /
				               (float) sheetHeight;
				bool isRot = sprites[i].rotated;

				// Top Left
				sprites[i].topLeft.x = isRot ? right : left;
				sprites[i].topLeft.y = top;

				// Top Right
				sprites[i].topRight.x = right;
				sprites[i].topRight.y = isRot ? bottom : top;

				// Bottom Left
				sprites[i].bottomLeft.x = left;
				sprites[i].bottomLeft.y = isRot ? top : bottom;

				// Bottom Right
				sprites[i].bottomRight.x = isRot ? left : right;
				sprites[i].bottomRight.y = bottom;

				// Add lookup
				spriteIdByName.Add(sprites[i].name, i);
			}
		}

		private void ProcessTriggerEvents()
		{
			if (triggers != null)
			{
				triggersExist = true;
				for (int i = 0; i < triggers.Length; i++)
				{
					triggers[i].index = i; // Used to link with triggerUnityEvents
				}
			}
		}

		private void ProcessTrackingObjects()
		{
			if (trackObjects != null)
			{
				trackObjectsExist = true;

				for (int i = 0; i < trackObjects.Length; i++)
				{
					var t = trackObjects[i];

					// Store all available track frames in track objects
					t.transformByFrameNumber = new Dictionary<int, UnimData.TrackObject.TrackTransform>();
					for (int j = 0; j < t.transform.Length; j++)
					{
						t.transformByFrameNumber.Add(t.transform[j].frame, t.transform[j]);
					}
				}
			}
		}

		private void ProcessAnimData()
		{
			Vector2 defaultPivot = new Vector2(0f, 0f);

			// Sets the base scale of each mesh
			Vector3[] baseVert = new Vector3[4];
			baseVert[0] = new Vector3(-0.5f, 0.5f, 0); // Top Left
			baseVert[1] = new Vector3(0.5f, 0.5f, 0); // Top Right
			baseVert[2] = new Vector3(0.5f, -0.5f, 0); // Bottom Right
			baseVert[3] = new Vector3(-0.5f, -0.5f, 0); // Bottom Left

			// Sets default coordinate offsets
			Vector3 pivotOffset = new Vector3(
				(-sceneWidth / 2f + (-sceneWidth / 2f * defaultPivot.x)),
				(sceneHeight / 2f + (-sceneHeight / 2f * defaultPivot.y)),
				0f);

			// Instantiate FrameData
			frameData = new FrameData[MaxFrames];
			for (int i = 0; i < animationData.Length; i++)
			{
				frameData[i] = new FrameData();
				frameData[i].vertTransforms = new Vector3[TotalVertexCount]; // 4 vertices per sprite
				frameData[i].vertSpriteIDs = new int[maxSprites];
				if (colorsExported)
				{
					if (!colorOperationsCombined)
					{
						frameData[i].vertColors = new Color[TotalVertexCount];
					}

					frameData[i].vertColorsAdd = new Color[TotalVertexCount];
				}

				frameData[i].vertUVs = new Vector2[TotalVertexCount];

				int spriteLength = animationData[i].Length;
				for (int j = 0; j < maxSprites; j++)
				{
					Vector3 tmpVert;
					Matrix4x4 tmpMtrx = new Matrix4x4();
					if (j < spriteLength)
					{
						// process sprites transform data
						tmpMtrx.m00 = animationData[i][j].scaleX;
						tmpMtrx.m01 = animationData[i][j].skewY;
						tmpMtrx.m02 = 0;
						tmpMtrx.m03 = animationData[i][j].positionX;

						tmpMtrx.m10 = animationData[i][j].skewX;
						tmpMtrx.m11 = animationData[i][j].scaleY;
						tmpMtrx.m12 = 0;
						tmpMtrx.m13 = animationData[i][j].positionY;

						tmpMtrx.m20 = 0;
						tmpMtrx.m21 = 0;
						tmpMtrx.m22 = 1;
						tmpMtrx.m23 = animationData[i][j].positionZ;

						tmpMtrx.m30 = 0;
						tmpMtrx.m31 = 0;
						tmpMtrx.m32 = 0;
						tmpMtrx.m33 = 1;

						// Top Left
						tmpVert = tmpMtrx.MultiplyPoint(baseVert[0]);
						tmpVert += pivotOffset;
						frameData[i].vertTransforms[j * 4 + 0] = tmpVert;
						// Top Right
						tmpVert = tmpMtrx.MultiplyPoint(baseVert[1]);
						tmpVert += pivotOffset;
						frameData[i].vertTransforms[j * 4 + 1] = tmpVert;
						// Bottom Right
						tmpVert = tmpMtrx.MultiplyPoint(baseVert[2]);
						tmpVert += pivotOffset;
						frameData[i].vertTransforms[j * 4 + 2] = tmpVert;
						// Bottom Left
						tmpVert = tmpMtrx.MultiplyPoint(baseVert[3]);
						tmpVert += pivotOffset;
						frameData[i].vertTransforms[j * 4 + 3] = tmpVert;

						// Set all vert corner to exported color
						if (colorsExported)
						{
							if (!colorOperationsCombined)
							{
								frameData[i].vertColors[j * 4 + 0] = animationData[i][j].color;
								frameData[i].vertColors[j * 4 + 1] = animationData[i][j].color;
								frameData[i].vertColors[j * 4 + 2] = animationData[i][j].color;
								frameData[i].vertColors[j * 4 + 3] = animationData[i][j].color;
							}

							frameData[i].vertColorsAdd[j * 4 + 0] = animationData[i][j].colorAdd;
							frameData[i].vertColorsAdd[j * 4 + 1] = animationData[i][j].colorAdd;
							frameData[i].vertColorsAdd[j * 4 + 2] = animationData[i][j].colorAdd;
							frameData[i].vertColorsAdd[j * 4 + 3] = animationData[i][j].colorAdd;
						}

						// sprite UV  |  process sprites image Id
						int spriteId = animationData[i][j].spriteID; // Get the sprite Id for the current anim sprite

						frameData[i].vertSpriteIDs[j] = spriteId; // Assign the sprite Id to the vert

						frameData[i].vertUVs[j * 4 + 0].x = sprites[spriteId].topLeft.x;
						frameData[i].vertUVs[j * 4 + 0].y = sprites[spriteId].topLeft.y;

						frameData[i].vertUVs[j * 4 + 1].x = sprites[spriteId].topRight.x;
						frameData[i].vertUVs[j * 4 + 1].y = sprites[spriteId].topRight.y;

						frameData[i].vertUVs[j * 4 + 2].x = sprites[spriteId].bottomRight.x;
						frameData[i].vertUVs[j * 4 + 2].y = sprites[spriteId].bottomRight.y;

						frameData[i].vertUVs[j * 4 + 3].x = sprites[spriteId].bottomLeft.x;
						frameData[i].vertUVs[j * 4 + 3].y = sprites[spriteId].bottomLeft.y;
					}
					else
					{
						// process sprites transform data
						tmpVert = Vector3.zero;
						frameData[i].vertTransforms[j * 4 + 0] = tmpVert;
						frameData[i].vertTransforms[j * 4 + 1] = tmpVert;
						frameData[i].vertTransforms[j * 4 + 2] = tmpVert;
						frameData[i].vertTransforms[j * 4 + 3] = tmpVert;

						// process sprites image Id
						frameData[i].vertSpriteIDs[j] = -1;

						// color Data
						if (colorsExported)
						{
							if (!colorOperationsCombined)
							{
								frameData[i].vertColors[j * 4 + 0] = Color.clear;
								frameData[i].vertColors[j * 4 + 1] = Color.clear;
								frameData[i].vertColors[j * 4 + 2] = Color.clear;
								frameData[i].vertColors[j * 4 + 3] = Color.clear;
							}

							frameData[i].vertColorsAdd[j * 4 + 0] = Color.clear;
							frameData[i].vertColorsAdd[j * 4 + 1] = Color.clear;
							frameData[i].vertColorsAdd[j * 4 + 2] = Color.clear;
							frameData[i].vertColorsAdd[j * 4 + 3] = Color.clear;
						}

						// sprite UV
						frameData[i].vertUVs[j * 4 + 0] = Vector2.zero;
						frameData[i].vertUVs[j * 4 + 1] = Vector2.zero;
						frameData[i].vertUVs[j * 4 + 2] = Vector2.zero;
						frameData[i].vertUVs[j * 4 + 3] = Vector2.zero;
					}
				}
			}
		}

		public int GetSpriteIdByName(string name)
		{
			if (!spriteIdByName.ContainsKey(name))
			{
				Debug.LogError($"Sprite name {name} does not exit.");
			}

			return spriteIdByName[name];
		}
	}

	public class UnimClip
	{
		public string name; // name
		public int startFrame; // frame start
		public int duration; // frame duration

		public UnimClip(string name, int startFrame, int duration)
		{
			this.name = name;
			this.startFrame = startFrame;
			this.duration = duration;
		}

		public int endFrame
		{
			get { return startFrame + duration; }
		}
	}

	public class UnimCache
	{
		private readonly fsSerializer serializer = new fsSerializer(); // Json serializer

		// Cache Dictionary ( where cache items are stored )
		private Dictionary<TextAsset, CacheItem<UnimData>> unimDataCache;
		private Dictionary<Texture2D, CacheItem<Material>> unimMaterialCache;

		// Singleton Implementation
		private static UnimCache instance;

		public static UnimCache Instance
		{
			get
			{
				if (instance == null)
				{
					instance = new UnimCache();
				}

				return instance;
			}
		}

		// Init
		private UnimCache()
		{
			unimDataCache = new Dictionary<TextAsset, CacheItem<UnimData>>();
			unimMaterialCache = new Dictionary<Texture2D, CacheItem<Material>>();
		}

		private class CacheItem<Type>
		{
			public int RefCount { get; private set; }
			public Type Entity { get; private set; }

			public int Ref()
			{
				return ++RefCount;
			}

			public int Unref()
			{
				return --RefCount;
			}

			public CacheItem(Type entity)
			{
				Entity = entity;
				RefCount = 1;
			}
		}

		public int GetCachedUnimDataRefCount(TextAsset file)
		{
			if (unimDataCache.TryGetValue(file, out CacheItem<UnimData> cacheItem))
			{
				return cacheItem.RefCount;
			}
			else
			{
				return 0;
			}
		}

		public int GetCachedMaterialRefCount(Texture2D tex)
		{
			if (unimMaterialCache.TryGetValue(tex, out CacheItem<Material> cacheItem))
			{
				return cacheItem.RefCount;
			}
			else
			{
				return 0;
			}
		}

		public UnimData LoadUnimData(TextAsset file, bool loadAsBytes, bool editorLoad = false)
		{
			UnimData unimData;

			// If cached unim based on filepath exists use that. Otherwise cache a new Unim
			if (!unimDataCache.TryGetValue(file, out CacheItem<UnimData> cacheItem) || editorLoad == true)
			{
				unimData = new UnimData();

				// Validate UnimData file
				if (file == null)
				{
					Debug.LogError($"UnimData not found: {file}");
					return null;
				}

				if (!loadAsBytes) // Load JSON file
				{
					// Stringify JSON
					string unimDataFileString = file.ToString();

					// Parse the JSON data
					fsData data = fsJsonParser.Parse(unimDataFileString);

					// Deserialize the data
					object obj = unimData;
					serializer.TryDeserialize(data, typeof(UnimData), ref obj).AssertSuccessWithoutWarnings();

					// Process Data
					unimData.ProcessBaseAnimData();
					if (editorLoad == false)
					{
						unimData.ProcessExtendedAnimData();

						// Cache Unim Item
						cacheItem = new CacheItem<UnimData>(unimData as UnimData);
						// Add cache item to Dictionary
						unimDataCache[file] = cacheItem;
					}
				}
				else
				{
					// TODO bytes implementation
					Debug.LogError("bytes file implementation not yet supported");
				}
			}
			else
			{
				unimData = cacheItem.Entity; // Assign existing casheItem to destination unim
				cacheItem.Ref(); // Increase the reference count
			}

			return unimData;
		}

		public void UnloadUnimData(TextAsset file)
		{
			CacheItem<UnimData> cacheItem;

			if (unimDataCache.TryGetValue(file, out cacheItem))
			{
				if (cacheItem.Unref() <= 0)
				{
					unimDataCache.Remove(file);
				}
			}
		}

		public Material LoadUnimMaterial(Texture2D textureRef, bool editorLoad = false)
		{
			Material mat;

			if (!unimMaterialCache.TryGetValue(textureRef, out CacheItem<Material> cacheItem) || editorLoad == true)
			{
				if (textureRef == null)
				{
					Debug.LogError($"Texture not found: {textureRef}");
					return null;
				}

				mat = new Material(Shader.Find("Unim/Standard"))
				{
					mainTexture = textureRef
				};

				if (editorLoad == false)
				{
					// Cache Unim Item
					cacheItem = new CacheItem<Material>(mat);
					// Add cache item to Dictionary
					unimMaterialCache[textureRef] = cacheItem;
				}
			}
			else
			{
				mat = cacheItem.Entity; // Assign existing casheItem to destination unim
				cacheItem.Ref(); // Increase the reference count
			}

			return mat;
		}

		public void UnloadUnimMaterial(Texture2D tex)
		{
			CacheItem<Material> cacheItem;

			if (unimMaterialCache.TryGetValue(tex, out cacheItem))
			{
				if (cacheItem.Unref() <= 0)
				{
					unimMaterialCache.Remove(tex);
				}
			}
		}

		// TODO Bytes implementation
		//        // ####################### bytes cache #####################
		//        public byte[] LoadBytes(string filePath)
		//        {
		//            CacheItem<byte[]> cacheItem = null;
		//            byte[] result = null;
		//
		//            if (!byteCache.TryGetValue(filePath, out cacheItem))
		//            {
		//                TextAsset bytesFile = Resources.Load<TextAsset>(filePath);
		//                if (bytesFile != null)
		//                {
		//                    result = bytesFile.bytes;
		//                    Resources.UnloadAsset(bytesFile);
		//                    cacheItem = new CacheItem<byte[]>(result);
		//                    byteCache[filePath] = cacheItem;
		//                }
		//                else
		//                {
		//                    Debug.LogError(string.Format("Bytes not found: {0}", filePath));
		//                }
		//            }
		//            else
		//            {
		//                result = cacheItem.Entity;
		//                cacheItem.Ref();
		//            }
		//
		//            return result;
		//        }
		//
		//        public void UnloadBytes(string filename)
		//        {
		//            CacheItem<byte[]> cacheItem;
		//
		//            if (byteCache.TryGetValue(filename, out cacheItem))
		//            {
		//                if (cacheItem.Unref() <= 0)
		//                {
		//                    byteCache.Remove(filename);
		//                }
		//            }
		//        }
		//        // #######################################
	}
}