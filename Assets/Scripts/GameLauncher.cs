using System.Collections;
using GameConsole;
using Sanmon.SurvivorDemo;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class GameLauncher : MonoBehaviour
{
    public string fontAsset = "Font/AliPuHui";
    public GameDevToolsController gdt;

    private IEnumerator Start()
    {
        SurvivorConfiguration configuration = null;
        yield return SurvivorConfiguration.LoadAsync(value => configuration = value);
        if (gdt == null) gdt = GameDevToolsController.Instance;
        if (gdt == null) gdt = FindObjectOfType<GameDevToolsController>();
        if (gdt == null) gdt = new GameObject("GameDevTools").AddComponent<GameDevToolsController>();

        var font = string.IsNullOrWhiteSpace(fontAsset) ? null : Resources.Load<Font>(fontAsset);
        if (font != null) gdt.RegisterFont(font.name, font);
        else font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var arena = GetComponent<SurvivorArena>();
        if (arena == null) arena = gameObject.AddComponent<SurvivorArena>();
        arena.Initialize(gdt, font, configuration);
    }
}
