using UnityEngine;

namespace SlapMania.Core
{
    public readonly struct TapEvent
    {
        public readonly double EventTime;
        public readonly Vector2 ScreenPosition;
        public readonly float Pressure;
        public readonly byte SourceId;

        public TapEvent(double eventTime, Vector2 screenPosition, float pressure, byte sourceId)
        {
            EventTime = eventTime;
            ScreenPosition = screenPosition;
            Pressure = pressure;
            SourceId = sourceId;
        }

        public const byte SourcePlayerTouch = 0;
        public const byte SourcePlayerMouse = 1;
        public const byte SourceAI          = 2;
        public const byte SourceReplay      = 3;
    }
}
