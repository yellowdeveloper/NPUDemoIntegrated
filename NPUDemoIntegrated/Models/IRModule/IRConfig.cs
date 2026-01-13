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
        private int _numOfData = 1025;

        private float _minTemp = 20.0f;
        private float _maxTemp = 40.0f;

        private int _resolution = 128;

        public enum EClassArray { person, face }
        
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
