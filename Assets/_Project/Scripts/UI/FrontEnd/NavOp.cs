namespace SlapMania.UI.FrontEnd
{
    /// What one navigation does to the screen stack (ScreenManager.ResolveNav).
    public enum NavOp : byte
    {
        /// Nothing (already there).
        Ignore,
        /// Add on top; Back returns to the current screen.
        Push,
        /// Swap the top; Back skips the current screen.
        Replace,
        /// Pop down to the target, which is already in the stack.
        PopTo,
        /// Clear the stack and show the target alone.
        ResetTo,
        /// Leave the front end and start a bout (raise MatchRequested).
        Match,
        /// The target lives in another scene's stack (raise LeaveRequested): the bout's
        /// overlay linking to Main Menu or, from Settings' back arrow, to Profile.
        Leave,
    }
}
