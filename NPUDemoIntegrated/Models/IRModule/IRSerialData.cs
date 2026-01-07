using NPUDemoIntegrated.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NPUDemoIntegrated.Models.IRModule
{
    class IRSerialData: Notifier
    {
        private List<byte> _receivedBuffer = new List<byte>();
        private readonly object _lock = new object();

        private const byte START_HI = 0x16;
        private const byte START_LO = 0x98;
        private const byte END_HI = 0x1A;
        private const byte END_LO = 0x9C;

        private int data_length = 0;

        private byte[] _receivedBufferArray;
        private float _sensorTemp = 0;
        private float[] _pixelTempArray;

        private readonly Stopwatch _stopwatch = new Stopwatch();

        public float sensorTemp
        {
            get { return _sensorTemp; }
            set { _sensorTemp = value; OnPropertyChanged(); }
        }
        public float[] pixelTempArray
        {
            get => _pixelTempArray;
            set { _pixelTempArray = value; OnPropertyChanged(); }
        }

        public void GetIsReceivedBufferEmpty()
        {
            lock (_lock)
            {
                if (_receivedBuffer.Count == 0)
                {
                    Console.WriteLine("Received Buffer is Empty");
                }
                else
                {
                    Console.WriteLine($"Received Buffer has {_receivedBuffer.Count} bytes");
                }
            }
        }

        public void ConvertReceivedBufferToArray(int index, int count)
        {
            lock (_lock)
            {
                _receivedBufferArray = _receivedBuffer.GetRange(index, count).ToArray();
            }
        }

        public void AddToBuffer(byte[] buffer, int size)
        {
            if (size <= 0) return;

            try
            {
                lock (_lock)
                {
                    _receivedBuffer.AddRange(buffer.Take(size));
                }
                // ADD DEBUG LOG
            }
            catch (Exception ex)
            {
                // ADD ERROR LOG
            }
        }

        /// <summary>
        /// Clear Received Buffer About count From index 
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        public void ClearBufferRange(int index, int count)
        {
            lock (_lock)
            {
                _receivedBuffer.RemoveRange(index, count);
            }
        }

        /// <summary>
        /// Remove One Content From Buffer At index
        /// </summary>
        /// <param name="index"></param>
        public void ClearBufferAt(int index)
        {
            lock (_lock)
            {
                _receivedBuffer.RemoveAt(index);
            }
        }

        /// <summary>
        /// Clear All Buffer
        /// </summary>
        public void ClearBuffer()
        {
            lock (_lock)
            {
                _receivedBuffer.Clear();
            }
        }

        public int FindProtocolInBuffer(int numOfData)
        {
            _stopwatch.Restart();

            byte[] start = { START_HI, START_LO };
            byte[] end = { END_HI, END_LO };

            ReadOnlySpan<byte> bufferSpan = CollectionsMarshal.AsSpan(_receivedBuffer);

            int startIndex = bufferSpan.IndexOf(start);

            if (startIndex == -1)
            {
                // ADD WARN LOG: No Start Byte Found
                // Console.WriteLine("No Start Byte Found");
                return -1;
            }

            bufferSpan = bufferSpan.Slice(startIndex + 2);
            int endIndex = bufferSpan.IndexOf(end);

            if (endIndex == -1)
            {
                // ADD WARN LOG: No End Byte Found
                // Console.WriteLine("No End Byte Found");
                return -2;
            }

            data_length = endIndex;
            if (data_length != numOfData)
            {
                // ADD ERROR LOG: Data Length Mismatch
                Console.WriteLine("Data Length Mismatch");
                return -3;
            }
            Console.WriteLine("Protocol Successfully Found!");

            _stopwatch.Stop();
            var elapsed = _stopwatch.Elapsed.TotalMilliseconds;
            Console.Write($"FindProtocolInBuffer elapsed:: {elapsed}\n");

            return startIndex;
        }

        public void PostProcessData(int numOfData, int resolution)
        {
            _stopwatch.Restart();

            lock (_lock)
            {
                short tmpSenseVal = (short)((_receivedBufferArray[0] << 8) | _receivedBufferArray[1]);
                sensorTemp = tmpSenseVal / 10.0f;

                float[] tempArray = new float[numOfData - 1];

                for (int i = 2; i < _receivedBufferArray.Length; i += 2)
                {
                    short tmpArrVal = (short)((_receivedBufferArray[i] << 8) | _receivedBufferArray[i + 1]);
                    tempArray[(i - 2) / 2] = tmpArrVal / 10.0f;
                }

                _stopwatch.Stop();
                var elapsed = _stopwatch.Elapsed.TotalMilliseconds;
                Console.Write($"PostProcessData Before Resize elapsed:: {elapsed}\n");

                pixelTempArray = tempArray;

                //for (int i = 0; i < _pixelTempArray.Length; i++)
                //{
                //    Console.WriteLine($"Pixel[{i}] Temperature: {_pixelTempArray[i]} °C");
                //}
            }
        }
    }
}
