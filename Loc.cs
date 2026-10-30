// Simple two-language string table (English / Hebrew).
// NOTE: TextMeshPro's default font has no Hebrew glyphs and no RTL support.
// Keep Hebrew OFF until you add a Hebrew font asset + an RTL fix (see README).
public static class Loc
{
    public static bool Hebrew;

    private static string T(string en, string he) { return Hebrew ? he : en; }

    // Status messages
    public static string MissionReady => T("Ready. Press HUNT to begin!", "מוכנים. לחץ על ציד כדי להתחיל!");
    public static string HuntBegins => T("The hunt begins!", "הציד התחיל!");
    public static string MonsterSpotted => T("Monster spotted! Aim and fire.", "מפלצת אותרה! כוון ולחץ ירי.");
    public static string NoAmmo => T("Out of ammo. Reload!", "אין תחמושת. טען מחדש.");
    public static string Reloading => T("Reloading...", "טוען...");
    public static string Reloaded => T("Reloaded.", "התחמושת נטענה.");
    public static string Defeated(int pts) => T("Monster down! +" + pts, "המפלצת הובסה! +" + pts + " נקודות.");
    public static string WaveStart(int w) => T("Wave " + w + " incoming!", "גל " + w + " מתקרב!");
    public static string WaveCleared(int w) => T("Wave " + w + " cleared!", "גל " + w + " נוקה!");
    public static string GameOver(int score, int wave) =>
        T("GAME OVER\nScore: " + score + "\nReached wave " + wave,
          "המשחק נגמר\nניקוד: " + score + "\nהגעת לגל " + wave);

    // HUD labels
    public static string ScoreLabel(int s) => T("Score: " + s, "ניקוד: " + s);
    public static string AmmoLabel(int a, int max) => T("Ammo: " + a + "/" + max, "תחמושת: " + a + "/" + max);
    public static string HealthLabel(int hp) => T("HP " + hp, "חיים " + hp);
    public static string WaveLabel(int w) => T("Wave: " + w, "גל: " + w);

    // Buttons
    public static string Fire => T("FIRE", "ירה");
    public static string Reload => T("RELOAD", "טען");
    public static string Hunt => T("HUNT", "ציד");
    public static string Restart => T("RESTART", "התחל מחדש");

    // Environment
    public static string Phase(EnvironmentController.DayPhase p)
    {
        switch (p)
        {
            case EnvironmentController.DayPhase.Dawn: return T("Dawn", "זריחה");
            case EnvironmentController.DayPhase.Day: return T("Day", "יום");
            case EnvironmentController.DayPhase.Dusk: return T("Dusk", "שקיעה");
            default: return T("Night", "לילה");
        }
    }

    public static string Weather(EnvironmentController.Weather w)
    {
        switch (w)
        {
            case EnvironmentController.Weather.Cloudy: return T("Cloudy", "מעונן");
            case EnvironmentController.Weather.Rain: return T("Rain", "גשם");
            case EnvironmentController.Weather.Fog: return T("Fog", "ערפל");
            case EnvironmentController.Weather.Storm: return T("Storm", "סערה");
            default: return T("Clear", "בהיר");
        }
    }
}
