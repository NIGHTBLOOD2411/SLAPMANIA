namespace SlapMania.UI.FrontEnd
{
    /// Maps a screen to its behaviour. Screens without special behaviour use ScreenView,
    /// which already gives them navigation, meters, rays, LIVE dots and enter motion.
    internal static class ScreenViews
    {
        public static ScreenView Create(ScreenId id)
        {
            switch (id)
            {
                case ScreenId.Title:      return new Screens.TitleView();
                case ScreenId.Loader:     return new Screens.LoaderView();
                case ScreenId.MainMenu:   return new Screens.MainMenuView();
                case ScreenId.Roster:     return new Screens.RosterView();
                case ScreenId.Search:     return new Screens.SearchView();
                case ScreenId.Walkout:    return new Screens.WalkoutView();
                case ScreenId.TaleOfTape: return new Screens.TaleOfTapeView();
                case ScreenId.PackOpen:   return new Screens.PackOpenView();
                case ScreenId.Settings:   return new Screens.SettingsView();
                case ScreenId.Replay:     return new Screens.ReplayView();
                // V1 Loading's only link is the sponsor strip; it advances on its own like A2.
                case ScreenId.V1Loading:  return new Screens.LoaderView(ScreenId.V1Home);
                default:                  return new ScreenView();
            }
        }
    }
}
