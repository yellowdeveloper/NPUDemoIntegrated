using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NPUDemoIntegrated.Utils;

namespace NPUDemoIntegrated.Models
{
    class SerialConfig: Notifier
    {
        private string _portName = "COM5";
        private int _baudRate = 921600;
        private int _dataBits = 8;
        private Parity _parity = Parity.None;
        private StopBits _stopBits = StopBits.One;

        public string portName
        {
            get { return _portName; }
            set { _portName = value; OnPropertyChanged(); }
        }
        public int baudRate
        {
            get { return _baudRate; }
            set { _baudRate = value; OnPropertyChanged(); }
        }
        public int dataBits
        {
            get { return _dataBits; }
            set { _dataBits = value; OnPropertyChanged(); }
        }

        public Parity parity
        {
            get { return _parity; }
            set { _parity = value; OnPropertyChanged(); }
        }
        public StopBits stopBits
        {
            get { return _stopBits; }
            set { _stopBits = value; OnPropertyChanged(); }
        }
    }
}
