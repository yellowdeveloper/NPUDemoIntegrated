using FTD2XX_NET;
using NPUDemoIntegrated.GlobalManagers;
using NPUDemoIntegrated.Models.IRModule;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NPUDemoIntegrated.Models
{
    abstract class BaseSerialService<TSerialConfig> where TSerialConfig : SerialConfig
    {
        protected TSerialConfig _serialConfig;
        protected SerialPort _spComm;
        protected FTDI _ftdi;
        protected CancellationTokenSource _cts;

        public readonly byte[] header = { 0x10, 0x01, 0x10, 0x01 };
        public readonly byte[] footer = { 0x0D, 0x0A, 0x0D, 0x0A };

        public BaseSerialService(TSerialConfig serialConfig, SerialPort sp, FTDI ftdi)
        {
            _serialConfig = serialConfig;
            _spComm = sp;
            _ftdi = ftdi;
        }

        protected abstract void OnSerialReceived(object sender, SerialDataReceivedEventArgs e);
        public abstract void SendModuleChangeNotice(EModuleType module);

        protected virtual int SerialConnect(SerialPort sp)
        {
            if (!sp.IsOpen)
            {
                try
                {
                    sp.PortName = _serialConfig.portName;
                    sp.BaudRate = _serialConfig.baudRate;
                    sp.Parity = _serialConfig.parity;
                    sp.DataBits = _serialConfig.dataBits;
                    sp.StopBits = _serialConfig.stopBits;

                    GlobalLogManager.Instance.ConsoleLog($"Connecting to Serial Port(Common:{sp.PortName})...");

                    sp.DataReceived += OnSerialReceived; // test with tx remove later --> No, We now use UART rx with SPI

                    sp.Open();

                    return 1;
                }
                catch (Exception ex)
                {
                    GlobalLogManager.Instance.ConsoleLog($"ERROR!! Error while opending Port(Common){ex}");
                    GlobalLogManager.Instance.AddLogToFile("ERROR", $"Error while opending Port(Common){ex}");

                    return 0;
                }
            }
            else
            {
                GlobalLogManager.Instance.ConsoleLog($"ERROR!! Error while opending Port(Common),Port Already Opened");
                GlobalLogManager.Instance.AddLogToFile("ERROR", $"Error while opending Port(Common), Port Already Opened");
                return 1;
            }
        }
        protected int SPIConnect(FTDI ftdi)
        {
            if (ftdi.IsOpen) return 1;

            uint devCount = 0;
            ftdi.GetNumberOfDevices(ref devCount);

            if (devCount == 0)
            {
                GlobalLogManager.Instance.ConsoleLog("No FTDI devices found.");
                return 0;
            }

            // Get FTDI Device List
            FTDI.FT_DEVICE_INFO_NODE[] deviceList = new FTDI.FT_DEVICE_INFO_NODE[devCount];
            ftdi.GetDeviceList(deviceList);

            int targetIndex = -1;

            // Find FT232H or RS232-HS
            for (int i = 0; i < devCount; i++)
            {
                GlobalLogManager.Instance.ConsoleLog($"Device [{i}]: {deviceList[i].Description} (Type: {deviceList[i].Type})");

                if (deviceList[i].Description.Contains("RS232-HS") || deviceList[i].Type == FTDI.FT_DEVICE.FT_DEVICE_232H)
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex == -1)
            {
                GlobalLogManager.Instance.ConsoleLog("ERROR: FT232H (High Speed) device not found!");
                return 0;
            }

            // Open Divice with Target Index
            FTDI.FT_STATUS status = ftdi.OpenByIndex((uint)targetIndex);
            if (status != FTDI.FT_STATUS.FT_OK)
            {
                GlobalLogManager.Instance.ConsoleLog($"Open Failed for Index {targetIndex}: {status}");
                return 0;
            }

            try
            {
                // Device Init
                ftdi.ResetDevice();
                ftdi.Purge(FTDI.FT_PURGE.FT_PURGE_RX | FTDI.FT_PURGE.FT_PURGE_TX);

                ftdi.SetCharacters(0, false, 0, false);
                ftdi.SetTimeouts(1000, 1000);
                ftdi.SetLatency(1);
                ftdi.SetFlowControl(FTDI.FT_FLOW_CONTROL.FT_FLOW_RTS_CTS, 0x00, 0x00);

                // Set to MPSSE Mode
                status = ftdi.SetBitMode(0x00, 0x02);
                if (status != FTDI.FT_STATUS.FT_OK)
                {
                    GlobalLogManager.Instance.ConsoleLog($"SetBitMode Failed: {status}");
                    ftdi.Close();
                    return 0;
                }

                Thread.Sleep(50);

                // MPSSE Setting
                if (!MPSSEConfig(ftdi))
                {
                    GlobalLogManager.Instance.ConsoleLog("MPSSE Configuration Failed.");
                    ftdi.Close();
                    return 0;
                }

                GlobalLogManager.Instance.ConsoleLog($"FTDI SPI Connected to {deviceList[targetIndex].Description}");
                return 1;
            }
            catch (Exception ex)
            {
                GlobalLogManager.Instance.ConsoleLog($"Error in Connect_SPI: {ex.Message}");
                ftdi.Close();
                return 0;
            }
        }

        private bool MPSSEConfig(FTDI _ftdi)
        {
            uint bytesWritten = 0;
            uint bytesRead = 0;
            byte[] buffer = new byte[1];

            _ftdi.Write(new byte[] { 0xAA }, 1, ref bytesWritten);
            Thread.Sleep(10);
            _ftdi.GetRxBytesAvailable(ref bytesRead);
            if (bytesRead > 0)
            {
                byte[] readData = new byte[bytesRead];
                _ftdi.Read(readData, bytesRead, ref bytesRead);
            }

            _ftdi.Write(new byte[] { 0xAB }, 1, ref bytesWritten);

            List<byte> cmd = new List<byte>();
            //uint bytesWritten = 0;

            // FT232H setting
            cmd.Add(0x8A); // Disable Divide by 5 (60MHz Master Clock) 
            cmd.Add(0x97); // Adaptive Clocking:: Enable = 0x96 || Disable = 0x97
            cmd.Add(0x8D); // Disable 3-Phase Data Clocking

            // Clock Setting
            // 30MHz = 60MHz / ((1 + Divisor) * 2) => Divisor = 0
            cmd.Add(0x86); // Set Clock Divisor 
            cmd.Add(0x00); // ValL
            cmd.Add(0x00); // ValH

            // Pin Direction Setting (Low Byte: ADBUS)
            cmd.Add(0x80); // 0x80
            cmd.Add(0x08); // Value: CS=1, SK=0
            cmd.Add(0xFB);  // Dir: SK/DO/CS=Out, DI=In

            // Send Setup Packet
            FTDI.FT_STATUS status = _ftdi.Write(cmd.ToArray(), cmd.Count, ref bytesWritten);
            if (status != FTDI.FT_STATUS.FT_OK) return false;

            Thread.Sleep(20);
            return true;
        }

        // Chip Select (CS) Control : Low
        protected void SetCS_Low(FTDI ftdi)
        {
            uint bytesWritten = 0;
            // 0x80: ADBUS Setup Command
            // 0x00: Value (All pins Low, ADBUS3(=CS) to Low)
            // 0xFB: Direction (SK, DO, CS = Output(1), DI = Input(0)) -> 1111 1011
            byte[] cmd = { 0x80, 0x00, 0xFB };
            ftdi.Write(cmd, cmd.Length, ref bytesWritten);
        }

        // Chip Select (CS) Control : High
        protected void SetCS_High(FTDI ftdi)
        {
            uint bytesWritten = 0;
            // 0x08: Value (ADBUS3(CS) High, 나머지 Low) -> 0000 1000
            byte[] cmd = { 0x80, 0x08, 0xFB };
            ftdi.Write(cmd, cmd.Length, ref bytesWritten);
        }

        protected void SPIDisconnect(FTDI ftdi)
        {
            if (ftdi != null && ftdi.IsOpen)
            {
                try
                {
                    ftdi.Purge(FTDI.FT_PURGE.FT_PURGE_RX | FTDI.FT_PURGE.FT_PURGE_TX);
                    ftdi.SetBitMode(0x00, 0x00);

                    ftdi.Close();

                    System.Threading.Thread.Sleep(100);

                    GlobalLogManager.Instance.ConsoleLog("FTDI (SPI) Device Closed & Reset.");
                }
                catch (Exception ex)
                {
                    GlobalLogManager.Instance.ConsoleLog($"ERROR closing FTDI: {ex.Message}");
                    GlobalLogManager.Instance.AddLogToFile("ERROR", ($"ERROR closing FTDI: {ex.Message}"));
                }
            }
        }

        protected virtual void SerialDisconnect(SerialPort sp)
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
                }
            }
        }

        protected float ConvertByteArray(byte[] val)
        {
            float result = BitConverter.ToUInt32(val);
            result = result / 1000.0f;
            return result;
        }
    }
}
