using NPUDemoIntegrated.Utils;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NPUDemoIntegrated.Models
{
    class SerialConfig: Notifier
    {
        // Serial Class for all NPU Connection
        private string _portName = "COM6";
        private int _baudRate = 115200;
        private int _dataBits = 8;
        private Parity _parity = Parity.None;
        private StopBits _stopBits = StopBits.One;

        private bool _isSpiEnable = false;
        private bool _isSendAll = true;
        private int _chunkSize = 1024;
        private int _probThres = 50;
        private EImageMode _imgMode = EImageMode.RESIZE;

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
        public bool isSpiEnable
        {
            get { return _isSpiEnable; }
            set { _isSpiEnable = value; OnPropertyChanged(); }
        }
        public int chunkSize
        {
            get { return _chunkSize; }
            set { _chunkSize = value; OnPropertyChanged(); }
        }
        public bool isSendAll
        {
            get { return _isSendAll; }
            set { _isSendAll = value; OnPropertyChanged(); }
        }

        public int probThres
        {
            get { return _probThres; }
            set { _probThres = value; OnPropertyChanged(); }
        }
        public EImageMode imgMode
        {
            get { return _imgMode; }
            set { _imgMode = value; OnPropertyChanged(); }
        }
    }
}
