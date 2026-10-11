using UnityEngine;

/// <summary>
/// Applies the project's custom cursor before the first scene starts.
/// No scene component is required.
/// </summary>
public static class CustomGameCursor
{
    private const string CursorResourcePath = "UI/MouseCursorOffWhite";
    private static readonly Vector2 Hotspot = new Vector2(5f, 2f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        Texture2D cursorTexture = Resources.Load<Texture2D>(CursorResourcePath);

        if (cursorTexture == null)
        {
            Debug.LogWarning($"Custom cursor was not found at Resources/{CursorResourcePath}.");
            return;
        }

        Cursor.SetCursor(cursorTexture, Hotspot, CursorMode.Auto);
    }
}
