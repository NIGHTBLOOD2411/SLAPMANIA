using System;

namespace SlapMania.Logging
{
    [Flags]
    public enum LogChannel
    {
        None         = 0,
        Boot         = 1 << 0,
        Match        = 1 << 1,
        Input        = 1 << 2,
        Power        = 1 << 3,
        Timing       = 1 << 4,
        Damage       = 1 << 5,
        Health       = 1 << 6,
        AI           = 1 << 7,
        Presentation = 1 << 8,
        Snapshot     = 1 << 9,
        UI           = 1 << 10,
        Audio        = 1 << 11,
        Save         = 1 << 12,
        Perf         = 1 << 13,
        All          = ~0
    }
}
