namespace SlapMania.UI.FrontEnd
{
    /// Every front-end screen (M10, ADR-0013), named after its mockup file in
    /// Docs/Reference/Mockups/V2 (V1 screens carry a `V1` prefix) so a UXML link
    /// `nav-<Name>` parses straight into this enum.
    /// Values are explicit and grouped by the mockup rows (A..E) because they are serialized
    /// into ScreenManager's table; never renumber, only append.
    public enum ScreenId
    {
        None = 0,

        // A - front end, menus, fighters
        Title = 1, Loader = 2, MainMenu = 3, Career = 4, Roster = 5, FighterCard = 6, SeasonPass = 7,

        // B - store, progression, profile
        Store = 10, PackOpen = 11, Challenges = 12, Rankings = 13, Profile = 14, Settings = 15,

        // C - match intro and your turn (Ready onwards is the bout scene's HUD)
        Search = 20, Walkout = 21, TaleOfTape = 22, Ready = 23, Aim = 24, Charge = 25,

        // D - impact, replay, pause
        Impact = 30, Brace = 31, Knockout = 32, Replay = 33, PauseMenu = 34,

        // E - results
        RoundEnd = 40, Win = 41, Loss = 42, RankUp = 43,

        // V1 page (Docs/Reference/Mockups/V1, canvas boards 01-16 in order). Its UXML lives in
        // Screens/V1 and its links are `nav-V1<Name>`; GameConfig.frontEndPage picks the page.
        V1Main = 100, V1Loading = 101, V1Home = 102, V1FighterSelect = 103, V1Shop = 104,
        V1Leaderboard = 105, V1Matchmaking = 106, V1VsIntro = 107,
        V1Countdown = 108, V1SlapTurn = 109, V1SmashHit = 110, V1Defend = 111, V1Pause = 112,
        V1RoundKO = 113, V1Victory = 114, V1Defeat = 115,
    }

    public static class ScreenIds
    {
        /// Screens drawn by the bout scene's uGUI HUD (V2Hud). Navigating to one from the
        /// front end means "start a bout", not "show a UXML screen". V1's eight bout screens
        /// (Countdown onwards) are the same: their UXML is a design reference, and the bout
        /// itself is the V2 HUD.
        public static bool IsMatch(ScreenId id)
        {
            switch (id)
            {
                case ScreenId.Ready: case ScreenId.Aim: case ScreenId.Charge:
                case ScreenId.Impact: case ScreenId.Brace: case ScreenId.Knockout:
                case ScreenId.Win: case ScreenId.Loss:
                case ScreenId.V1Countdown: case ScreenId.V1SlapTurn: case ScreenId.V1SmashHit:
                case ScreenId.V1Defend: case ScreenId.V1Pause: case ScreenId.V1RoundKO:
                case ScreenId.V1Victory: case ScreenId.V1Defeat:
                    return true;
                default:
                    return false;
            }
        }

        /// The six screens behind V2's top tab bar, and the four behind V1's bottom nav.
        /// Tabs are siblings, not a stack.
        public static bool IsTab(ScreenId id)
        {
            switch (id)
            {
                case ScreenId.MainMenu: case ScreenId.Career: case ScreenId.Roster:
                case ScreenId.Store: case ScreenId.SeasonPass: case ScreenId.Rankings:
                case ScreenId.V1Home: case ScreenId.V1FighterSelect: case ScreenId.V1Shop:
                case ScreenId.V1Leaderboard:
                    return true;
                default:
                    return false;
            }
        }

        /// Where a request for `id` lands on `page`. V1 has no Title or Main Menu; its splash
        /// and Home stand in (boot opens Title, a bout's MAIN MENU returns to MainMenu).
        public static ScreenId ForPage(ScreenId id, Data.FrontEndPage page)
        {
            if (page != Data.FrontEndPage.V1) return id;
            switch (id)
            {
                case ScreenId.Title:    return ScreenId.V1Main;
                case ScreenId.MainMenu: return ScreenId.V1Home;
                default:                return id;
            }
        }

        public static bool TryParse(string name, out ScreenId id)
        {
            return System.Enum.TryParse(name, false, out id) && System.Enum.IsDefined(typeof(ScreenId), id) && id != ScreenId.None;
        }
    }
}
