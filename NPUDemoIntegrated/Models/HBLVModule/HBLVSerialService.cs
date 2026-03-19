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
        public List<Rect> bboxs;
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

            int detected_cnt = pureData[0];
            pureData.RemoveAt(0);

            List<Rect> received_rects = new List<Rect>();

            int modelType = pureData[pureData.Count - 9];

            if (modelType != 2)
            {
                GlobalLogManager.Instance.ConsoleLog($"ERROR!! ModelTypeError!! receivedType :: {modelType}, currentType :: 2");
                SendModuleChangeNotice(EModuleType.HBLV);
                Thread.Sleep(10);
                connectionState = EConnectionState.Connected;
                return;
            }

            byte[] voltageByte = new byte[4];
            voltageByte[0] = pureData[pureData.Count - 4];
            voltageByte[1] = pureData[pureData.Count - 3];
            voltageByte[2] = pureData[pureData.Count - 2];
            voltageByte[3] = pureData[pureData.Count - 1];

            byte[] ampereByte = new byte[4];
            // Check Packet 
            // GlobalLogManager.Instance.ConsoleLog($"{pureData[pureData.Count - 4]}");
            // GlobalLogManager.Instance.ConsoleLog($"{pureData[pureData.Count - 3]}");
            // GlobalLogManager.Instance.ConsoleLog($"{pureData[pureData.Count - 2]}");
            // GlobalLogManager.Instance.ConsoleLog($"{pureData[pureData.Count - 1]}");
            ampereByte[0] = pureData[pureData.Count - 8];
            ampereByte[1] = pureData[pureData.Count - 7];
            ampereByte[2] = pureData[pureData.Count - 6];
            ampereByte[3] = pureData[pureData.Count - 5];

            pureData.RemoveRange(pureData.Count - 9, 9);

            byte[] probByte = new byte[4];
            probByte[0] = pureData[pureData.Count - 4];
            probByte[1] = pureData[pureData.Count - 3];
            probByte[2] = pureData[pureData.Count - 2];
            probByte[3] = pureData[pureData.Count - 1];

            pureData.RemoveRange(pureData.Count - 4, 4);

            float voltage = ConvertByteArray(voltageByte);
            float ampere = ConvertByteArray(ampereByte);
            float prediction = ConvertByteArray(probByte);

            received.prediction = prediction;
            received.voltage = voltage;
            received.ampere = ampere;
            received.errorCode = 0;

            // GlobalLogManager.Instance.ConsoleLog($"{voltage} {ampere}");

            for (int i = 0; i < detected_cnt; i++)
            {
                byte[] rectData = pureData.Take(9).ToArray();
                pureData.RemoveRange(0, 9);

                int prob = rectData[0];
                int x = BitConverter.ToInt16(rectData, 1); // lt x
                int y = BitConverter.ToInt16(rectData, 3); // lt y
                int w = BitConverter.ToInt16(rectData, 5);
                int h = BitConverter.ToInt16(rectData, 7);

                // GlobalLogManager.Instance.ConsoleLog($"Before Resize :: x={x}, y={y}, w={w}, h={h}");

                int x_new = x;
                int y_new = y;
                int w_new = w;
                int h_new = h;

                double ratio_x;
                double ratio_y;
                
                ratio_x = 320.0f / 320.0f;
                ratio_y = 320.0f / 320.0f;

                x_new = (int)(x * ratio_x);
                y_new = (int)(y * ratio_y);
                w_new = (int)(w * ratio_x);
                h_new = (int)(h * ratio_y);

                // GlobalLogManager.Instance.ConsoleLog($"After Resize :: x={x_new}, y={y_new}, w={w_new}, h={h_new}");

                GlobalLogManager.Instance.ConsoleLog($"Num {i + 1} | probability {prob} :: x={x}, y={y}, w={w}, h={h}");
                // GlobalLogManager.Instance.AddLogToFile("DEBUG", $"Num {i + 1} | probability {prob} :: x={x}, y={y}, w={w}, h={h}");

                if (prob >= _serialConfig.probThres)
                {
                    received_rects.Add(new Rect(x_new, y_new, w_new, h_new));
                }
            }
            received.bboxs = received_rects;

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
