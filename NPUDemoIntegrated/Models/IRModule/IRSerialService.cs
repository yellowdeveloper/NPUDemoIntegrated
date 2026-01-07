using NPUDemoIntegrated.GlobalManagers;
using NPUDemoIntegrated.Models.IRModule;
using NPUDemoIntegrated.Models.OBJModule;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NPUDemoIntegrated.Models.IRModule
{
    class IRSerialService
    {
        private SerialPort sp = new SerialPort();
        private readonly IRConfig _config;
        public IRSerialData Data { get; } = new IRSerialData();

        public IRSerialService(IRConfig config)
        {
            _config = config;
        }
        public int Connect()
        {
            try
            {
                if (!sp.IsOpen)
                {
                    sp.PortName = _config.portName;
                    sp.BaudRate = _config.baudRate;
                    sp.DataBits = _config.dataBits;
                    sp.StopBits = _config.stopBits;
                    sp.Parity = _config.parity;

                    sp.DataReceived += OnSerialReceived;

                    sp.Open();

                    // ADD DEBUG LOG
                    Console.WriteLine($"Successfully Connected to: {_config.portName}");
                }
            }
            catch (Exception ex)
            {
                // ADD ERROR LOG
                Console.WriteLine($"Connect ERROR: {ex}");
            }
            return 1;
        }
        public int Disconnect()
        {
            if ((sp != null && sp.IsOpen))
            { // && (sp_debug != null && sp_debug.IsOpen)
                try
                {
                    sp.DataReceived -= OnSerialReceived; // test with tx change to rx later
                    //sp_debug.DataReceived -= OnSerialReceived_Debug;

                    System.Threading.Thread.Sleep(20);

                    sp.Close();
                    //sp_debug.Close();
                }
                catch (Exception ex)
                {
                    GlobalLogManager.Instance.ConsoleLog($"ERROR!! Error during Disconnect: {ex.Message}");
                    GlobalLogManager.Instance.AddLogToFile("ERROR", $"Error during Disconnect: {ex.Message}");
                    return -1;
                }
            }
            else
            {
                return -1;
            }

                GlobalLogManager.Instance.ConsoleLog("Serial Disconnected");
            GlobalLogManager.Instance.AddLogToFile("DEBUG", "Serial Disconnected");

            return 1;
        }

        private void OnSerialReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (!sp.IsOpen) return;

            try
            {
                int bytesToRead = sp.BytesToRead;
                byte[] buffer = new byte[bytesToRead];
                int actuallyRead = sp.Read(buffer, 0, bytesToRead);

                Data.AddToBuffer(buffer, actuallyRead);

                ParseReceivedData();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Serial Receive ERROR: {ex}");
            }
        }
        public void StartMeasure()
        {
            int numOfData = _config.numOfData;

            try
            {
                byte numOfData_Hi = (byte)((numOfData >> 8) & 0xFF);
                byte numOfData_Lo = (byte)(numOfData & 0xFF);

                byte[] cmdArray = { 0x11, 0x00, 0x00, numOfData_Hi, numOfData_Lo, 0x98 };

                sp.Write(cmdArray, 0, 6);
                Console.WriteLine($"Start Measure Command Sent: {cmdArray}");
                // ADD DEBUG LOG
            }
            catch (Exception ex)
            {
                // ADD ERROR LOG
                Console.WriteLine($"Start Measure Command Send ERROR: {ex}");
            }
        }

        private void ParseReceivedData()
        {
            int startIndex = Data.FindProtocolInBuffer(_config.numOfData * 2);
            if (startIndex < 0) return;
            int endIndex = startIndex + (_config.numOfData * 2) + 2;

            Data.ConvertReceivedBufferToArray(startIndex + 2, _config.numOfData * 2);

            Data.PostProcessData(_config.numOfData, _config.resolution);
            Data.ClearBufferRange(0, endIndex + 2);
        }
    }
}
