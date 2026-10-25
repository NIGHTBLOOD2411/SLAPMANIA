using System.Diagnostics;
using UnityEngine;

namespace SlapMania.Logging
{
    public static class SlapLog
    {
        public static LogChannel Enabled = LogChannel.All;

        [Conditional("SLAPMANIA_DEBUG")]
        public static void Trace(LogChannel ch, string msg)
        {
            if ((Enabled & ch) == 0) return;
            UnityEngine.Debug.Log(Prefix(ch) + msg);
        }

        [Conditional("SLAPMANIA_DEBUG")]
        public static void Info(LogChannel ch, string msg)
        {
            if ((Enabled & ch) == 0) return;
            UnityEngine.Debug.Log(Prefix(ch) + msg);
        }

        [Conditional("SLAPMANIA_DEBUG")]
        public static void Warn(LogChannel ch, string msg)
        {
            if ((Enabled & ch) == 0) return;
            UnityEngine.Debug.LogWarning(Prefix(ch) + msg);
        }

        public static void Error(LogChannel ch, string msg)
        {
            UnityEngine.Debug.LogError(Prefix(ch) + msg);
        }

        [Conditional("SLAPMANIA_VERBOSE")]
        public static void Verbose(LogChannel ch, string msg)
        {
            if ((Enabled & ch) == 0) return;
            UnityEngine.Debug.Log(Prefix(ch) + msg);
        }

        private static string Prefix(LogChannel ch) => "[" + ch + "] ";
    }
}
