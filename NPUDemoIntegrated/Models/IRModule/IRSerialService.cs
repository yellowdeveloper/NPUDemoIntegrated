using FTD2XX_NET;
using NPUDemoIntegrated.GlobalManagers;
using NPUDemoIntegrated.Models.OBJModule;
using NPUDemoIntegrated.ViewModels;
using OpenCvSharp;
using System.Data;
using System.Diagnostics;
using System.IO.Ports;
using System.Runtime.InteropServices;


namespace NPUDemoIntegrated.Models.IRModule
{
    class IRSerialService: ImageSerialService<SerialConfig>
    {
        private readonly IRConfig _irConfig;
        public IRSerialService(SerialConfig serialConfig, IRConfig irConfig, SerialPort sp, FTDI ftdi, SharedStatus stat) : base(serialConfig, sp, ftdi, stat)
        {
            _irConfig = irConfig;
        }

        private SerialPort spModule = new SerialPort();

        public IRModuleData Data { get; } = new IRModuleData();
        
        public event Action<float, float, List<Rect>, List<IRConfig.EClassArray>, List<int>> PointsReceived;

        private readonly Stopwatch _stopwatch = new Stopwatch();

        private readonly object _rcLock = new object();

        private bool isSendingCmd = false;

        public int ModuleConnect()
        {
            if (!spModule.IsOpen)
            {
                try
                {
                    spModule.PortName = _irConfig.portName;
                    spModule.BaudRate = _irConfig.baudRate;
                    spModule.Parity = _irConfig.parity;
                    spModule.DataBits = _irConfig.dataBits;
                    spModule.StopBits = _irConfig.stopBits;

                    GlobalLogManager.Instance.ConsoleLog($"Connecting to Serial Port(Module:{spModule.PortName})...");

                    spModule.DataReceived += OnSerialReceivedModule;

                    spModule.Open();

                    return 1;
                }
                catch (Exception ex)
                {
                    GlobalLogManager.Instance.ConsoleLog($"ERROR!! Error while opending Port(Module){ex}");
                    GlobalLogManager.Instance.AddLogToFile("ERROR", $"Error while opending Port(Module){ex}");

                    return 0;
                }
            }
            else
            {
                GlobalLogManager.Instance.ConsoleLog($"ERROR!! Error while opending Port(Module),Port Already Opened");
                GlobalLogManager.Instance.AddLogToFile("ERROR", $"Error while opending Port(Module), Port Already Opened");
                return 1;
            }
        }

        private void OnSerialReceivedModule(object sender, SerialDataReceivedEventArgs e)
        {
            if (!spModule.IsOpen) return;

            try
            {
                int bytesToRead = spModule.BytesToRead;
                byte[] buffer = new byte[bytesToRead];
                int actuallyRead = spModule.Read(buffer, 0, bytesToRead);

                Data.AddToBuffer(buffer, actuallyRead);

                ParseReceivedData(Data, _irConfig);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Serial Receive ERROR: {ex}");
            }
        }

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
                int img_size = (_irConfig.resolution * _irConfig.resolution * 3);
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
                        if (!(pureData.Count >= 10 && pureData.Count % 10 == 0))
                        {
                            GlobalLogManager.Instance.ConsoleLog("ERROR!! Data Length Not Available:: Clear Buffer");
                            return;
                        }
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

            int detected_cnt = pureData[0];
            pureData.RemoveAt(0);

            List<Rect> received_rects = new List<Rect>();
            List<IRConfig.EClassArray> received_cls = new List<IRConfig.EClassArray>();
            List<int> received_probs = new List<int>();

            int modelType = pureData[pureData.Count - 9];

            if (modelType != 1)
            {
                GlobalLogManager.Instance.ConsoleLog($"ERROR!! ModelTypeError!! receivedType :: {modelType}, currentType :: 1");
                SendModuleChangeNotice(EModuleType.IR);
                Thread.Sleep(10);
                connectionState = EConnectionState.Connected;
                return;
            }

            byte[] voltageByte = new byte[4];
            voltageByte[0] = pureData[pureData.Count - 8];
            voltageByte[1] = pureData[pureData.Count - 7];
            voltageByte[2] = pureData[pureData.Count - 6];
            voltageByte[3] = pureData[pureData.Count - 5];

            byte[] ampereByte = new byte[4];
            ampereByte[0] = pureData[pureData.Count - 4];
            ampereByte[1] = pureData[pureData.Count - 3];
            ampereByte[2] = pureData[pureData.Count - 2];
            ampereByte[3] = pureData[pureData.Count - 1];

            pureData.RemoveRange(pureData.Count - 9, 9);

            float voltage = ConvertByteArray(voltageByte);
            float ampere = ConvertByteArray(ampereByte);

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

                double ratio_x;
                double ratio_y;

                ratio_x = 512.0f / _irConfig.resolution;
                ratio_y = 512.0f / _irConfig.resolution;

                x_new = (int)(x * ratio_x);
                y_new = (int)(y * ratio_y);
                w_new = (int)(w * ratio_x);
                h_new = (int)(h * ratio_y);

                GlobalLogManager.Instance.ConsoleLog($"Num {i + 1} | class {cls} | probability {prob} :: x={x}, y={y}, w={w}, h={h}");
                GlobalLogManager.Instance.AddLogToFile("DEBUG", $"Num {i + 1} | class {cls} | probability {prob} :: x={x}, y={y}, w={w}, h={h}");

                if (prob >= _serialConfig.probThres && (cls == 0 || cls == 1))
                {
                    received_cls.Add((IRConfig.EClassArray)cls);
                    received_probs.Add(prob);
                    received_rects.Add(new Rect(x_new, y_new, w_new, h_new));
                }
            }
            PointsReceived?.Invoke(ampere, voltage, received_rects, received_cls, received_probs);

            connectionState = EConnectionState.Connected;
        }

        public void StartMeasure()
        {
            isSendingCmd = true;
            int numOfData = _irConfig.numOfData;

            try
            {
                byte numOfData_Hi = (byte)((numOfData >> 8) & 0xFF);
                byte numOfData_Lo = (byte)(numOfData & 0xFF);

                byte[] cmdArray = { 0x11, 0x00, 0x00, numOfData_Hi, numOfData_Lo, 0x98 };

                spModule.Write(cmdArray, 0, 6);
                Console.WriteLine($"Start Measure Command Sent: {cmdArray}");

                _stopwatch.Stop();
                 Data.fps = 1.0f / _stopwatch.Elapsed.TotalSeconds;
                // Console.WriteLine($"RT-FPS: {Data.fps}");
                isSendingCmd = false;
                _stopwatch.Restart();
                // ADD DEBUG LOG
            }
            catch (Exception ex)
            {
                // ADD ERROR LOG
                isSendingCmd = false;
                Console.WriteLine($"Start Measure Command Send ERROR: {ex}");
            }
        }

        public void ParseReceivedData(IRModuleData data, IRConfig config)
        {
            int startIndex = data.FindProtocolInBuffer(config.numOfData * 2);
            if (startIndex < 0) return;
            int endIndex = startIndex + (config.numOfData * 2) + 2;

            data.ConvertReceivedBufferToArray(startIndex + 2, config.numOfData * 2);

            data.PostProcessData(config.numOfData, config.resolution);
            data.ClearBufferRange(0, endIndex + 2);
        }

        public void ModuleDisconnect()
        {
            if ((spModule != null && spModule.IsOpen))
            {
                while (isSendingCmd)
                {
                    Thread.Sleep(10);
                }
                try
                {
                    spModule.DataReceived -= OnSerialReceivedModule;
                    Thread.Sleep(20);
                    spModule.Close();

                    GlobalLogManager.Instance.ConsoleLog($"IR Module Disconnected");
                }
                catch (Exception ex)
                {
                    GlobalLogManager.Instance.ConsoleLog($"ERROR!! Error during Disconnect: {ex.Message}");
                    GlobalLogManager.Instance.AddLogToFile("ERROR", $"Error during Disconnect: {ex.Message}");
                }
            }
        }
    }
}
