using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Drawing;
using Unim;
using UnityEngine.UI;

public class TestUnim : MonoBehaviour
{
    private UnimPlayer unimPlayer;

    public string imagePath; 
    public string sprtNm;
    public RawImage bal;
    //public image


    [Header("Fill Color")] 
    public Color subColorFill = Color.white;
    private Color subColorFill_prev = Color.white;

    public Color addColorFill = Color.black;
    private Color addColorFill_prev = Color.black;
    
    public UnimPlayer.ColorOperation colorFillOperation;
    public bool resetFillColors;

    [Header("Sprite Color")] 
    public Color subColorSprite = Color.white;
    private Color subColorSprite_prev = Color.white;

    public Color addColorSprite = Color.black;
    private Color addColorSprite_prev = Color.black;

    public string spriteName;
    public UnimPlayer.ColorOperation colorSpriteOperation;
    public bool resetSpriteColors;
    
    
    [Header("Fade Fill Color")] 
    public Color subFillStart = Color.white;
    public Color addFillStart = Color.black;
    [Space]
    public Color subFillEnd = Color.white;
    public Color addFillEnd = Color.black;
    [Space]
    public float fadeFillDuration = 1;
    public UnimPlayer.ColorOperation fadeFillOperation;
    public bool runFadeFill;

    [Header("Fade Sprite Color")] 
    public Color subSpriteStart = Color.white;
    public Color addSpriteStart = Color.black;
    [Space]
    public Color subSpriteEnd = Color.white;
    public Color addSpriteEnd = Color.black;
    [Space]
    public string spriteFadeName;
    public float fadeSpriteDuration = 1;
    public UnimPlayer.ColorOperation fadeSpriteOperation;
    public bool runSpriteFill;

    // Start is called before the first frame update
    void Awake()
    {
        unimPlayer = GetComponent<UnimPlayer>();
    }

    void Start()
    {
        unimPlayer.PlayClip("Idle", UnimPlayer.PlayType.Loop);    
    }
    
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            unimPlayer.PlayClip("Spawn", UnimPlayer.PlayType.PlayOnce);
        }

        if (Input.GetKeyDown(KeyCode.W))
        {
            unimPlayer.QueueClip("Idle", UnimPlayer.PlayType.Loop);
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            unimPlayer.QueueClip("PostAttackLoop", UnimPlayer.PlayType.SingleFrame, maxCount: 1);
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            unimPlayer.QueueClip("Idle", UnimPlayer.PlayType.PlayOnce);
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            unimPlayer.QueueStop();
        }

        if (Input.GetKeyDown(KeyCode.Y))
        {
            unimPlayer.Stop();
        }

        if (Input.GetKeyDown(KeyCode.U))
        {
            unimPlayer.AssignSpriteToSpriteSheet(sprtNm, (Texture2D)Resources.Load(imagePath));
        }

        if (Input.GetKeyDown(KeyCode.I))
        {
            unimPlayer.ClearSpriteSheet();   
        }
        


        if (addColorFill_prev != addColorFill || subColorFill != subColorFill_prev)
        {
            unimPlayer.SetFillColor(subColorFill, addColorFill, colorFillOperation);
            addColorFill_prev = addColorFill;
            subColorFill_prev = subColorFill;
        }

        if (addColorSprite_prev != addColorSprite || subColorSprite_prev != subColorSprite)
        {
            unimPlayer.SetSpriteColor(spriteName, subColorSprite, addColorSprite, colorSpriteOperation);
            addColorSprite_prev = addColorSprite;
            subColorSprite_prev = subColorSprite;
        }

        if (resetSpriteColors == true)
        {
            resetSpriteColors = false;
            unimPlayer.ResetSpriteColorsOffset();
            subColorSprite = Color.white;
            addColorSprite = Color.black;
        }

        if (resetFillColors == true)
        {
            resetFillColors = false;
            unimPlayer.ResetFillColorOffset();
            subColorFill = Color.white;
            addColorFill = Color.black;
        }

        if (runFadeFill)
        {
            unimPlayer.FadeFillColor(subFillStart, subFillEnd, addFillStart, addFillEnd, fadeFillDuration, 0, fadeFillOperation, true  );
            runFadeFill = false;
        }
        
        if (runSpriteFill)
        {
            unimPlayer.FadeSpriteColor(spriteFadeName,subSpriteStart, subSpriteEnd, addSpriteStart, addSpriteEnd, fadeSpriteDuration, 0, fadeSpriteOperation, true  );
            runSpriteFill = false;
        }
    }
}