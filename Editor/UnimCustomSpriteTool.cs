using UnityEditor;
using UnityEngine;

public class PostprocessImages
{
    [MenuItem("Assets/Unim/Set Custom Sprite Image Settings")]
    public static void SetSpriteSettings()
    {
        string[] gUIDs = Selection.assetGUIDs;
        foreach (string g in gUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.isReadable = true;
                importer.mipmapEnabled = false;
            }
        }
    }

    [MenuItem("Assets/Unim/Set Custom Sprite Image Settings", true)]
    public static bool ValidateImageType()
    {
        return Selection.activeObject is Texture2D;
    }
}
