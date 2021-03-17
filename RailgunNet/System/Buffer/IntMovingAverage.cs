namespace RailgunNet.System.Buffer
{
    public class IntMovingAverage
    {
        private CircularBuffer<int> _samples;

        private double _accumulator;
        public double Average { get; private set; }

        public bool IsFull => _samples.IsFull;

        public IntMovingAverage(int samples)
        {
            _samples = new CircularBuffer<int>(samples);
        }

        /// <summary>
        /// Computes a new windowed average each time a new sample arrives
        /// </summary>
        /// <param name="newSample"></param>
        public void ComputeAverage(int newSample)
        {
            if (_samples.IsFull)
                _accumulator -= _samples.PopFront();

            _accumulator += newSample;
            _samples.PushBack(newSample);
            Average = _accumulator / _samples.Size;
        }

        public void Clear()
        {
            _accumulator = 0;
            _samples.Clear();
        }
    }

    public class DoubleMovingAverage
    {
        private CircularBuffer<double> _samples;

        private double _accumulator;
        public double Average { get; private set; }

        public bool IsFull => _samples.IsFull;

        public DoubleMovingAverage(int samples)
        {
            _samples = new CircularBuffer<double>(samples);
        }

        /// <summary>
        /// Computes a new windowed average each time a new sample arrives
        /// </summary>
        /// <param name="newSample"></param>
        public void ComputeAverage(double newSample)
        {
            if (_samples.IsFull)
                _accumulator -= _samples.PopFront();

            _accumulator += newSample;
            _samples.PushBack(newSample);
            Average = _accumulator / _samples.Size;
        }

        public void Clear()
        {
            _accumulator = 0;
            _samples.Clear();
        }
    }
}