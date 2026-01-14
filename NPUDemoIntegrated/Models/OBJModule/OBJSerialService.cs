using FTD2XX_NET;
using NPUDemoIntegrated.GlobalManagers;
using OpenCvSharp;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text;

namespace NPUDemoIntegrated.Models.OBJModule
{
    class OBJSerialService: ImageSerialService<SerialConfig>
    {
        private readonly OBJConfig _objConfig;
        public OBJSerialService(SerialConfig serialConfig, OBJConfig objConfig, SerialPort sp, FTDI ftdi, SharedStatus stat) : base(serialConfig, sp, ftdi, stat)
        {
            _objConfig = objConfig;
        }

        // UART rx and tx separate
        private SerialPort spDebug = new SerialPort();

        public event Action<float, float, List<Rect>, List<OBJConfig.EClassArray>, List<int>> PointsReceived;

        private readonly object _rcLock = new object();

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
                int img_size = (_objConfig.imgSize == EImageSize.S384) ? 442368 : 307200;
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

        private void OnSerialReceived_Debug(object sender, SerialDataReceivedEventArgs e)
        {
            if (!spDebug.IsOpen) return;

            try
            {
                int bytes_to_read = spDebug.BytesToRead; // Test with tx change to rx later
                byte[] buffer = new byte[bytes_to_read];
                spDebug.Read(buffer, 0, bytes_to_read);  // Test with tx change to rx later

                GlobalLogManager.Instance.ConsoleLog($"DEBUG PORT: Received Bytes Length: {buffer.Length}\n");

                GlobalLogManager.Instance.ConsoleLog($"DEBUG PORT: Received Bytes: ");
                for (int i = 0; i < buffer.Length; i++)
                {
                    Console.Write($"{buffer[i]:X2}  ");
                }
                Console.Write(": ");

                Console.Write($"{Encoding.UTF8.GetString(buffer)}\n");
            }
            catch (Exception ex)
            {
                GlobalLogManager.Instance.ConsoleLog($"ERROR!! Error occured while receiving {ex}");
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

            int detected_cnt = pureData[0];
            pureData.RemoveAt(0); 

            List<Rect> received_rects = new List<Rect>();
            List<OBJConfig.EClassArray> received_cls = new List<OBJConfig.EClassArray>();
            List<int> received_probs = new List<int>();

            int modelType = pureData[pureData.Count - 9];

            if (modelType != 0)
            {
                GlobalLogManager.Instance.ConsoleLog($"ERROR!! ModelTypeError!! receivedType :: {modelType}, currentType :: 0");
                SendModuleChangeNotice(EModuleType.OBJ);
                Thread.Sleep(10);
                return;
            }

            byte[] voltageByte = new byte[4];
            voltageByte[0] = pureData[pureData.Count - 8];
            voltageByte[1] = pureData[pureData.Count - 7];
            voltageByte[2] = pureData[pureData.Count - 6];
            voltageByte[3] = pureData[pureData.Count - 5];
            
            byte[] ampereByte = new byte[4];
            GlobalLogManager.Instance.ConsoleLog($"{pureData[pureData.Count - 4]}");
            GlobalLogManager.Instance.ConsoleLog($"{pureData[pureData.Count - 3]}");
            GlobalLogManager.Instance.ConsoleLog($"{pureData[pureData.Count - 2]}");
            GlobalLogManager.Instance.ConsoleLog($"{pureData[pureData.Count - 1]}");
            ampereByte[0] = pureData[pureData.Count - 4];
            ampereByte[1] = pureData[pureData.Count - 3];
            ampereByte[2] = pureData[pureData.Count - 2];
            ampereByte[3] = pureData[pureData.Count - 1];

            pureData.RemoveRange(pureData.Count - 9, 9);

            float voltage = ConvertByteArray(voltageByte);
            float ampere = ConvertByteArray(ampereByte);

            GlobalLogManager.Instance.ConsoleLog($"{voltage} {ampere}");

            for (int i = 0; i < detected_cnt; i++)
            {
                byte[] rectData = pureData.Take(10).ToArray();
                pureData.RemoveRange(0, 10);

                int cls = rectData[0];
                int prob = rectData[1];
                int x = BitConverter.ToInt16(rectData, 2); // lt x
                int y = BitConverter.ToInt16(rectData, 4); // lt y
                int w = BitConverter.ToInt16(rectData, 6);
                int h = BitConverter.ToInt16(rectData, 8);

                GlobalLogManager.Instance.ConsoleLog($"Before Resize :: x={x}, y={y}, w={w}, h={h}");

                int x_new = x;
                int y_new = y;
                int w_new = w;
                int h_new = h;

                if (_serialConfig.imgMode == EImageMode.RESIZE)
                {
                    double ratio_x;
                    double ratio_y;

                    if (_objConfig.imgSize == EImageSize.S320)
                    {
                        ratio_x = 640.0f / 320.0f;
                        ratio_y = 480.0f / 320.0f;
                    }
                    else
                    {
                        ratio_x = 640.0f / 384.0f;
                        ratio_y = 480.0f / 384.0f;
                    }

                    x_new = (int)(x * ratio_x);
                    y_new = (int)(y * ratio_y);
                    w_new = (int)(w * ratio_x);
                    h_new = (int)(h * ratio_y);

                    GlobalLogManager.Instance.ConsoleLog($"After Resize :: x={x_new}, y={y_new}, w={w_new}, h={h_new}");
                }
                else
                {
                    // process in PAD mode
                    double ratio;
                    int y_pad;

                    if (_objConfig.imgSize == EImageSize.S320)
                    {
                        ratio = 640.0f / 320.0f;
                        y_pad = (int)((320 - 480 * (320.0f / 640.0f)) / 2); // == 40
                    }
                    else
                    {
                        ratio = 640.0f / 384.0f;
                        y_pad = (int)((384 - 480 * (384.0f / 640.0f)) / 2); // == 48
                    }

                    x_new = (int)(x * ratio);
                    y_new = (int)((y - y_pad) * ratio);
                    w_new = (int)(w * ratio);
                    h_new = (int)(h * ratio);

                    GlobalLogManager.Instance.ConsoleLog($"After Resize :: x={x_new}, y={y_new}, w={w_new}, h={h_new}");
                }

                GlobalLogManager.Instance.ConsoleLog($"Num {i + 1} | class {cls} | probability {prob} :: x={x}, y={y}, w={w}, h={h}");
                GlobalLogManager.Instance.AddLogToFile("DEBUG", $"Num {i + 1} | class {cls} | probability {prob} :: x={x}, y={y}, w={w}, h={h}");

                if (prob >= _serialConfig.probThres && cls == 0)
                {
                    received_cls.Add((OBJConfig.EClassArray)cls);
                    received_probs.Add(prob);
                    received_rects.Add(new Rect(x_new, y_new, w_new, h_new));
                }
            }
            GlobalLogManager.Instance.ConsoleLog($"amp, volt before invoke :: {ampere}, {voltage}");
            PointsReceived?.Invoke(ampere, voltage, received_rects, received_cls, received_probs);

            connectionState = EConnectionState.Connected;
        }
    }
}
