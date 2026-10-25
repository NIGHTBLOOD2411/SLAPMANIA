using System.Diagnostics;

namespace SlapMania.Logging
{
    public static class SlapAssert
    {
        [Conditional("SLAPMANIA_DEBUG")]
        public static void That(bool condition, string message)
        {
            if (!condition) throw new SlapAssertException(message);
        }

        [Conditional("SLAPMANIA_DEBUG")]
        public static void NotNull(object obj, string name)
        {
            if (obj == null) throw new SlapAssertException(name + " must not be null");
        }
    }

    public sealed class SlapAssertException : System.Exception
    {
        public SlapAssertException(string msg) : base(msg) { }
    }
}
