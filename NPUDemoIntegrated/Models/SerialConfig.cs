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

        private bool _is_spi_enable = false;
        private bool _is_send_all = true;
        private int _chunk_size = 1024;
        private int _prob_thres = 50;

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
        public bool is_spi_enable
        {
            get { return _is_spi_enable; }
            set { _is_spi_enable = value; OnPropertyChanged(); }
        }
        public int chunk_size
        {
            get { return _chunk_size; }
            set { _chunk_size = value; OnPropertyChanged(); }
        }
        public bool is_send_all
        {
            get { return _is_send_all; }
            set { _is_send_all = value; OnPropertyChanged(); }
        }

        public int prob_thres
        {
            get { return _prob_thres; }
            set { _prob_thres = value; OnPropertyChanged(); }
        }
    }
}
