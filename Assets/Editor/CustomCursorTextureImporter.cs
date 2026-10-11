using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps the generated cursor texture in a cursor-compatible import format.
/// </summary>
public sealed class CustomCursorTextureImporter : AssetPostprocessor
{
    private const string CursorAssetPath = "Assets/Resources/UI/MouseCursorOffWhite.png";

    private void OnPreprocessTexture()
    {
        if (assetPath != CursorAssetPath)
        {
            return;
        }

        Configure((TextureImporter)assetImporter);
    }

    [InitializeOnLoadMethod]
    private static void ConfigureExistingAsset()
    {
        EditorApplication.delayCall += () =>
        {
            TextureImporter importer = AssetImporter.GetAtPath(CursorAssetPath) as TextureImporter;
            if (importer == null || IsConfigured(importer))
            {
                return;
            }

            Configure(importer);
            importer.SaveAndReimport();
        };
    }

    private static void Configure(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Cursor;
        importer.alphaIsTransparency = true;
        importer.isReadable = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 64;
    }

    private static bool IsConfigured(TextureImporter importer)
    {
        return importer.textureType == TextureImporterType.Cursor
            && importer.alphaIsTransparency
            && importer.isReadable
            && !importer.mipmapEnabled
            && importer.wrapMode == TextureWrapMode.Clamp
            && importer.filterMode == FilterMode.Bilinear
            && importer.textureCompression == TextureImporterCompression.Uncompressed
            && importer.maxTextureSize == 64;
    }
}
