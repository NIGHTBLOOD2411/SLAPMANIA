namespace SlapMania.Core
{
    public interface IClock
    {
        double Now { get; }
    }

    public sealed class ManualClock : IClock
    {
        public double Now { get; private set; }
        public ManualClock(double start = 0) { Now = start; }
        public void Advance(double dt) { Now += dt; }
        public void Set(double t) { Now = t; }
    }
}
