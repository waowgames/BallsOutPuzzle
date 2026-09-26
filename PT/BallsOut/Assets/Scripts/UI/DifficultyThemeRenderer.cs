using UnityEngine;

/// <summary>
/// Recolours the renderers under this object (e.g. the desk behind the board) on
/// Hard / Very Hard levels through a property block, so the shared material stays untouched.
/// </summary>
public sealed class DifficultyThemeRenderer : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField] private Color hardColor = new Color(0.62f, 0.08f, 0.11f);
    [SerializeField] private Color veryHardColor = new Color(0.27f, 0.02f, 0.05f);

    private Renderer[] renderers;
    private MaterialPropertyBlock block;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        block = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        GameEvents.OnLevelLoaded += HandleLevelLoaded;

        if (LevelManager.Instance != null)
            Apply(LevelManager.Instance.CurrentDifficulty);
    }

    private void OnDisable()
    {
        GameEvents.OnLevelLoaded -= HandleLevelLoaded;
    }

    private void HandleLevelLoaded(int _)
    {
        Apply(LevelManager.Instance != null
            ? LevelManager.Instance.CurrentDifficulty
            : LevelDifficulty.Normal);
    }

    private void Apply(LevelDifficulty difficulty)
    {
        foreach (Renderer target in renderers)
        {
            if (target == null)
                continue;

            if (difficulty == LevelDifficulty.Normal)
            {
                // Clearing the block restores the material's own colour.
                target.SetPropertyBlock(null);
                continue;
            }

            target.GetPropertyBlock(block);
            block.SetColor(BaseColorId, difficulty == LevelDifficulty.Hard ? hardColor : veryHardColor);
            target.SetPropertyBlock(block);
        }
    }
}
