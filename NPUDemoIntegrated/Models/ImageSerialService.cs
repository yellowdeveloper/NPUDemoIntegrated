using FTD2XX_NET;
using NPUDemoIntegrated.GlobalManagers;
using NPUDemoIntegrated.Models.IRModule;
using NPUDemoIntegrated.Models.OBJModule;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NPUDemoIntegrated.Models
{
    internal class ImageSerialService<TConfig> : BaseSerialService<TConfig> where TConfig : SerialConfig
    {
        public ImageSerialService(TConfig config, SerialPort sp, FTDI ftdi) : base(config, sp, ftdi) { }

        protected List<byte> receivedBuffer = new List<byte>();
        protected List<byte> pureData = new List<byte>();

        protected int fragmentIndex;
        protected byte[] imageToSend;
        protected int footerTryCnt = 0;

        protected EConnectionState _connectionState = EConnectionState.Disconnected;

        public EConnectionState connectionState
        {
            get { return _connectionState; }
            set { _connectionState = value; }
        }
        // Connect Method For NPU Connection
        public virtual int Connect()
        {
            int spiStat = 0;
            int spCommStat = 0;

            if (_serialConfig.isSpiEnable)
            {
                spiStat = base.SPIConnect(_ftdi);
            }
            else
            {
                spiStat = 1;
            }

            spCommStat = base.SerialConnect(_spComm);

            if (spiStat == 1 && spCommStat == 1)
            {
                connectionState = EConnectionState.Connected;
                // StatusChanged?.Invoke("Connected");
                GlobalLogManager.Instance.ConsoleLog($"OK.. All stats good!, Connected To all the Ports");
            }
            else
            {
                GlobalLogManager.Instance.ConsoleLog($"ERROR!! Serial stat :: {spCommStat} & SPI stat :: {spiStat} \nDisconnect from all ...");
                Disconnect();
            }

            return spiStat & spCommStat;
        }

        protected void FindData()
        {
            ReadOnlySpan<byte> bufferSpan = CollectionsMarshal.AsSpan(receivedBuffer);

            int headerIndex = bufferSpan.IndexOf(header);
            int dataLength = 0;

            if (headerIndex != -1)
            {
                bufferSpan = bufferSpan.Slice(headerIndex + 4);
                int footerIndex = bufferSpan.IndexOf(footer);
                if (footerIndex != -1)
                {
                    dataLength = footerIndex;

                    if (dataLength >= 10 && dataLength % 10 == 0)
                    {
                        pureData = receivedBuffer.GetRange(headerIndex + 4, dataLength);
                        receivedBuffer.RemoveRange(0, headerIndex + footerIndex + 8);
                    }
                    else
                    {
                        GlobalLogManager.Instance.ConsoleLog("ERROR!! Data Length Not Available:: Clear Buffer");
                        receivedBuffer.RemoveRange(0, headerIndex + footerIndex + 8);
                    }
                }
                else
                {
                    if (footerTryCnt >= 5)
                    {
                        GlobalLogManager.Instance.ConsoleLog($"ERROR!! Wrong Footer:: Clear Buffer");
                        receivedBuffer.Clear();
                        footerTryCnt = 0;
                    }
                    footerTryCnt++;
                    GlobalLogManager.Instance.ConsoleLog($"WARN.. No Footer Found in Buffer Find Count:: {footerTryCnt}");
                    Thread.Sleep(1);
                }
            }
            else
            {
                GlobalLogManager.Instance.ConsoleLog("WARN.. No Header Found in Buffer... Find Again");
            }
        }

        protected void SendImageFragment()
        {
            if (connectionState == EConnectionState.SendingImage)
            {
                if (_serialConfig.isSendAll == true) _serialConfig.chunkSize = imageToSend.Length;
                int chunk_size = _serialConfig.chunkSize;

                while (connectionState == EConnectionState.SendingImage)
                {
                    int bytes_sent = fragmentIndex * chunk_size;
                    int remain_bytes = imageToSend.Length - bytes_sent;
                    byte[] chunk;
                    int bytes_to_send;

                    if (remain_bytes <= 0)
                    {
                        // image all sent
                        GlobalLogManager.Instance.ConsoleLog("No fragments to send.");
                        GlobalLogManager.Instance.AddLogToFile("DEBUG", "no fragments to send.");
                        return;
                    }
                    else if (remain_bytes <= chunk_size)
                    {
                        // last fragment :: Footer added
                        GlobalLogManager.Instance.ConsoleLog("Sending last Fragment");
                        GlobalLogManager.Instance.AddLogToFile("DEBUG", "\nSending last Fragment");
                        bytes_to_send = remain_bytes;

                        chunk = new byte[bytes_to_send];

                        Buffer.BlockCopy(imageToSend, bytes_sent, chunk, 0, bytes_to_send);
                        //Buffer.BlockCopy(new byte[4] { 0x0D, 0x0A, 0x0D, 0x0A }, 0, chunk, bytes_to_send, 4);

                        connectionState = EConnectionState.WaitingForInference;
                        // StatusChanged?.Invoke("WaitingForInference");

                        GlobalLogManager.Instance.ConsoleLog($"All image fragments have been sent :: Size={bytes_to_send} bytes");
                        GlobalLogManager.Instance.AddLogToFile("DEBUG", $"All image fragments have been sent :: Size={bytes_to_send} bytes");
                    }
                    else
                    {
                        bytes_to_send = chunk_size;

                        chunk = new byte[chunk_size];
                        Buffer.BlockCopy(imageToSend, bytes_sent, chunk, 0, bytes_to_send);
                    }

                    try
                    {
                        _spComm.Write(chunk, 0, chunk.Length);
                        // GlobalLogManager.Instance.ConsoleLog($"OK.. Sent Fragment {fragment_index + 1}:: Size={bytes_to_send} bytes");
                        // GlobalLogManager.Instance.AddLogToFile("DEBUG", $"Sent Fragment {fragment_index + 1}:: Size={bytes_to_send} bytes");
                        fragmentIndex++;
                    }
                    catch (Exception ex)
                    {
                        GlobalLogManager.Instance.ConsoleLog($"ERROR!! Error sending fragment: {ex.Message}");
                        GlobalLogManager.Instance.AddLogToFile("ERROR", $"Error sending fragment: {ex.Message}");
                        Disconnect();
                    }
                }
            }
        }

        protected void SendImageFragment_SPI()
        {
            if (connectionState != EConnectionState.SendingImage) return;

            if (_serialConfig.isSendAll == true)
                _serialConfig.chunkSize = imageToSend.Length;

            int chunkSize = _serialConfig.chunkSize;
            int chunkSendCount = imageToSend.Length / chunkSize;
            uint bytesWritten = 0;

            byte[] txBuffer = new byte[chunkSize + 3];

            txBuffer[0] = 0x11;                      // send cmd

            int len = chunkSize - 1;

            // Packet length
            txBuffer[1] = (byte)(len & 0xFF);        // Low Byte
            txBuffer[2] = (byte)((len >> 8) & 0xFF); // High Byte

            try
            {
                // [CS Low] Comm Start (ADBUS3 = 0)
                // 0x80(GPIO Setting) + 0x00(CS Low, 나머지 Low) + 0xFB(Direction)
                SetCS_Low(_ftdi);

                for (int i = 0; i < chunkSendCount; i++)
                {
                    int offset = i * chunkSize;

                    // Copy and send with SPI
                    Buffer.BlockCopy(imageToSend, offset, txBuffer, 3, chunkSize);
                    FTDI.FT_STATUS status = _ftdi.Write(txBuffer, txBuffer.Length, ref bytesWritten);

                    if (status != FTDI.FT_STATUS.FT_OK)
                    {
                        GlobalLogManager.Instance.ConsoleLog($"SPI Write Failed at offset {offset}: {status}");
                        break;
                    }
                }

                // [CS High] Comm Start (ADBUS3 = 1)
                // 0x80(GPIO Setting) + 0x08(CS High) + 0xFB(Direction)
                SetCS_High(_ftdi);

                connectionState = EConnectionState.WaitingForInference;
                // StatusChanged?.Invoke("WaitingForInference");

                GlobalLogManager.Instance.ConsoleLog($"SPI Image Transfer Complete. Total: {imageToSend.Length} bytes");
            }
            catch (Exception ex)
            {
                GlobalLogManager.Instance.ConsoleLog($"ERROR in SendImageFragment_SPI: {ex.Message}");
                SetCS_High(_ftdi);
                Disconnect();
            }
        }

        protected override void OnSerialReceived(object sender, SerialDataReceivedEventArgs e)
        {
            throw new NotImplementedException();
        }

        public override void SendModuleChangeNotice(EModuleType moduleType)
        {
            if (!_spComm.IsOpen)
            {
                base.SerialConnect(_spComm);
            }
            GlobalLogManager.Instance.ConsoleLog($"SendModuleChangeNotice Called in OBJService, TargetModule is ::{moduleType}");
            _spComm.Write(new byte[] { (byte)moduleType }, 0, 1);
        }

        public virtual void Disconnect()
        {
            base.SPIDisconnect(_ftdi);
            base.SerialDisconnect(_spComm);

            connectionState = EConnectionState.Disconnected;
            // StatusChanged?.Invoke("Disconnected");

            GlobalLogManager.Instance.ConsoleLog("Serial Disconnected");
            GlobalLogManager.Instance.AddLogToFile("DEBUG", "Serial Disconnected");
        }
    }
}
