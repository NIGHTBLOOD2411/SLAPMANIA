using System;

namespace SlapMania.Core
{
    public interface ITapSource
    {
        event Action<TapEvent> OnTap;
        void Enable();
        void Disable();
    }
}
