using UnityEngine;

public class CharacterDef
{
    public string name, blurb;
    public float maxHealth, moveMult, damageMult;
    public int planterBonus;   // extra food per crop harvest
    public string modelKey;    // the character's body model in Resources/Models (see PlayerBody)
}

/// <summary>The three playable characters. Chosen on the start menu and saved.</summary>
public static class Characters
{
    public static readonly CharacterDef[] All =
    {
        new CharacterDef
        {
            name = "Scavenger",
            blurb = "Quick on their feet and good at finding supplies. Fragile in a fight.",
            maxHealth = 80f, moveMult = 1.15f, damageMult = 1f, planterBonus = 0,
            modelKey = "player-gb-walker"
        },
        new CharacterDef
        {
            name = "Medic",
            blurb = "Tough and patient. Every crop harvest gives one extra food.",
            maxHealth = 120f, moveMult = 0.95f, damageMult = 0.9f, planterBonus = 1,
            modelKey = "player-handpaint-girl"
        },
        new CharacterDef
        {
            name = "Brawler",
            blurb = "Hits hard with a club but is slow to get moving.",
            maxHealth = 110f, moveMult = 0.9f, damageMult = 1.3f, planterBonus = 0,
            modelKey = "player-warrok"
        },
    };

    public static CharacterDef Current => All[Mathf.Clamp(GameSettings.CharacterIndex, 0, All.Length - 1)];
}
