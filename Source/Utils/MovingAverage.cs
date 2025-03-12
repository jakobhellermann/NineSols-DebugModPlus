using System.Collections.Generic;

namespace DebugModPlus;

internal sealed class MovingAverage {
    private readonly int windowSize;

    private readonly Queue<long> samples;

    private long sampleAccumulator;

    public long GetAverage => sampleAccumulator / samples.Count;

    public float GetAverageFloat => (float)sampleAccumulator / samples.Count;

    public MovingAverage(int windowSize = 30) {
        this.windowSize = windowSize;
        samples = new Queue<long>(this.windowSize + 1);
    }

    public void Clear() {
        sampleAccumulator = 0L;
        samples.Clear();
    }

    public void Sample(long newSample) {
        sampleAccumulator += newSample;
        samples.Enqueue(newSample);
        if (samples.Count > windowSize) {
            sampleAccumulator -= samples.Dequeue();
        }
    }
}