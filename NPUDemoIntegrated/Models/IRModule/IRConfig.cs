using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NPUDemoIntegrated.Models.IRModule
{
    class IRConfig : SerialConfig
    {
        private string _portName = "COM7";
        private int _baudRate = 115200;
        private int _dataBits = 8;
        private Parity _parity = Parity.None;
        private StopBits _stopBits = StopBits.One;

        private int _numOfData = 1025;

        private float _minTemp = 20.0f;
        private float _maxTemp = 40.0f;

        private int _resolution = 128;

        public enum EClassArray { person, face }

        public string IRPortName
        {
            get { return _portName; }
            set { _portName = value; OnPropertyChanged(); }
        }
        public int IRBaudRate
        {
            get { return _baudRate; }
            set { _baudRate = value; OnPropertyChanged(); }
        }
        public int IRDataBits
        {
            get { return _dataBits; }
            set { _dataBits = value; OnPropertyChanged(); }
        }

        public Parity IRParity
        {
            get { return _parity; }
            set { _parity = value; OnPropertyChanged(); }
        }
        public StopBits IRStopBits
        {
            get { return _stopBits; }
            set { _stopBits = value; OnPropertyChanged(); }
        }
        public int numOfData
        {
            get { return _numOfData; }
            set { _numOfData = value; OnPropertyChanged(); }
        }
        public float minTemp
        {
            get { return _minTemp; }
            set { _minTemp = value; OnPropertyChanged(); }
        }
        public float maxTemp
        {
            get { return _maxTemp; }
            set { _maxTemp = value; OnPropertyChanged(); }
        }
        public int resolution
        {
            get { return _resolution; }
            set { _resolution = value; OnPropertyChanged(); }
        }
    }
}
