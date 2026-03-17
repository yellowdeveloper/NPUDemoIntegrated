using FTD2XX_NET;
using NPUDemoIntegrated.GlobalManagers;
using NPUDemoIntegrated.Models.IRModule;
using NPUDemoIntegrated.Models.OBJModule;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NPUDemoIntegrated.Models.HBLVModule
{
    struct predictionResultPacket
    {
        public float ampere;
        public float voltage;
        public float prediction;
        public byte errorCode;
    }
    internal class HBLVSerialService: ImageSerialService<HBLVConfig>
    {
        HBLVConfig _hblvConfig;
        public HBLVSerialService(SerialConfig serialConfig, HBLVConfig hblvConfig, SerialPort sp, FTDI ftdi, SharedStatus stat) : base(hblvConfig, sp, ftdi, stat)
        {
            _hblvConfig = hblvConfig;
        }

        public event Action<predictionResultPacket> PacketReceived;

        private readonly object _rcLock = new object();
        private predictionResultPacket received;

        public Task SerialCommunication(Mat frame)
        {
            return Task.Run(() => {
                lock (_rcLock)
                {
                    if (_spComm.IsOpen)
                    {
                        _spComm.DiscardInBuffer();
                    }
                    receivedBuffer.Clear();
                    pureData.Clear();
                }

                int frame_ch_num = frame.Channels();
                var frame_size = frame.Size();

                // Cv2.ImEncode(".jpg", frame, out image_to_send);
                // JPG = compressed foramt >> Encode to bmp. But, bmp contains header info >> Do not use ImEncode
                int img_size = (320 * 320 * 3);
                if (imageToSend == null || imageToSend.Length != img_size)
                {
                    imageToSend = new byte[img_size];
                }

                if (frame.Empty())
                {
                    for (int i = 0; i < imageToSend.Length; i++)
                    {
                        imageToSend[i] = (byte)(i % 256);
                    }
                    GlobalLogManager.Instance.ConsoleLog($"WARN.. sending test array");
                }
                else Marshal.Copy(frame.Data, imageToSend, 0, imageToSend.Length); // fix 

                fragmentIndex = 0;
                connectionState = EConnectionState.SendingImage;

                if (_serialConfig.isSpiEnable == true) SendImageFragment_SPI();
                else SendImageFragment();
            });
        }

        protected override void OnSerialReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (!_spComm.IsOpen) return;

            try
            {
                lock (_rcLock)
                {
                    int bytes_to_read = _spComm.BytesToRead;
                    byte[] buffer = new byte[bytes_to_read];
                    int actually_read = _spComm.Read(buffer, 0, bytes_to_read);

                    if (actually_read > 0) receivedBuffer.AddRange(buffer.Take(actually_read));

                    Console.WriteLine("");
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        Console.Write($"{buffer[i]:X2}");
                    }
                    Console.WriteLine("");

                    FindData();

                    if (pureData.Count >= 1)
                    {
                        footerTryCnt = 0;
                        ProcessReceivedBuffer();
                        pureData.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                GlobalLogManager.Instance.ConsoleLog($"ERROR!! Error occured while receiving {ex}");
                GlobalLogManager.Instance.AddLogToFile("ERROR", $"Error occured while receiving {ex}");
            }
        }

        private void ProcessReceivedBuffer()
        {
            if (connectionState != EConnectionState.WaitingForInference)
            {
                GlobalLogManager.Instance.ConsoleLog($"ERROR!! connectionState Error State:: {connectionState}");
                return;
            }

            connectionState = EConnectionState.ProceesingBuffer;

            if (pureData.Count < 12 && pureData[0] == 0xFF)
            {
                ProcessError(pureData[1]);
                connectionState = EConnectionState.Connected;
                return;
            }

            byte[] probByte = new byte[4];
            probByte[0] = pureData[0];
            probByte[1] = pureData[1];
            probByte[2] = pureData[2];
            probByte[3] = pureData[3];
            pureData.RemoveRange(0, 4);

            int modelType = pureData[0];

            if (modelType != 2)
            {
                GlobalLogManager.Instance.ConsoleLog($"ERROR!! ModelTypeError!! receivedType :: {modelType}, currentType :: 2");
                SendModuleChangeNotice(EModuleType.HBLV);
                Thread.Sleep(10);
                connectionState = EConnectionState.Connected;
                return;
            }

            byte[] voltageByte = new byte[4];
            voltageByte[0] = pureData[1];
            voltageByte[1] = pureData[2];
            voltageByte[2] = pureData[3];
            voltageByte[3] = pureData[4];

            byte[] ampereByte = new byte[4];
            ampereByte[0] = pureData[5];
            ampereByte[1] = pureData[6];
            ampereByte[2] = pureData[7];
            ampereByte[3] = pureData[8];

            pureData.RemoveRange(pureData.Count - 9, 9);

            float prediction = ConvertByteArray(probByte);
            float voltage = ConvertByteArray(voltageByte);
            float ampere = ConvertByteArray(ampereByte);

            received.prediction = prediction;
            received.voltage = voltage;
            received.ampere = ampere;
            received.errorCode = 0;

            PacketReceived?.Invoke(received);

            connectionState = EConnectionState.Connected;
        }

        private void ProcessError(byte errorCode)
        {
            switch (errorCode)
            {
                case 0x01:
                    received.errorCode = 0x01;
                    PacketReceived?.Invoke(received);
                    break;
                case 0x02:
                    received.errorCode = 0x02;
                    PacketReceived?.Invoke(received);
                    break;
                default:
                    break;

            }
        }
    }
}
