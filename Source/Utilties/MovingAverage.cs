using System;
using System.Collections.Generic;
using System.Text;

namespace DebugModPlus {
    internal sealed class MovingAverage {
        private readonly int _windowSize;

        private readonly Queue<long> _samples;

        private long _sampleAccumulator;

        public long GetAverage => _sampleAccumulator / _samples.Count;

        public float GetAverageFloat => _sampleAccumulator / _samples.Count;

        public MovingAverage(int windowSize = 30) {
            _windowSize = windowSize;
            _samples = new Queue<long>(_windowSize + 1);
        }

        public void Clear() {
            _sampleAccumulator = 0L;
            _samples.Clear();
        }

        public void Sample(long newSample) {
            _sampleAccumulator += newSample;
            _samples.Enqueue(newSample);
            if (_samples.Count > _windowSize) {
                _sampleAccumulator -= _samples.Dequeue();
            }
        }
    }
}