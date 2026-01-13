using FTD2XX_NET;
using NPUDemoIntegrated.GlobalManagers;
using NPUDemoIntegrated.ViewModels;
using OpenCvSharp;
using System.Data;
using System.IO.Ports;
using System.Runtime.InteropServices;


namespace NPUDemoIntegrated.Models.IRModule
{
    class IRSerialService: BaseSerialService<IRConfig>
    {
        private SerialPort spModule = new SerialPort();
        private SerialPort spComm = new SerialPort();
        private FTDI ftdi = new FTDI();

        public IRModuleData Data { get; } = new IRModuleData();
        public IRSerialService(IRConfig config) : base(config) { }
        public event Action<List<Rect>, List<IRConfig.EClassArray>, List<int>> PointsReceived;

        private List<byte> received_buffer = new List<byte>();
        private List<byte> pure_data = new List<byte>();

        private int fragment_index;
        private byte[] image_to_send;
        private int footerTryCnt = 0;

        private EConnectionState _connectionState = EConnectionState.Disconnected;

        private readonly object _rcLock = new object();

        public EConnectionState connectionState
        {
            get { return _connectionState; }
            set { _connectionState = value; }
        }

        public int Connect()
        {
            int spiStat = 0;
            int spCommStat = 0;
            int spModuleStat = 0;

            if (_config.is_spi_enable)
            {
                spiStat = base.SPIConnect(ftdi);
            }
            else
            {
                spiStat = 1;
            }

            spModuleStat = SerialConnect(spModule);
            spCommStat = base.SerialConnect(spComm);

            if (spiStat == 1 && spCommStat == 1 && spModuleStat == 1)
            {
                connectionState = EConnectionState.Connected;
                GlobalLogManager.Instance.ConsoleLog($"OK.. All stats good!, Connected To all the Ports");
            }
            else
            {
                GlobalLogManager.Instance.ConsoleLog($"ERROR!! Serial stat :: {spModuleStat}, {spCommStat} & SPI stat :: {spiStat} \nDisconnect from all ...");
                Disconnect(); 
            }

            return spiStat & spCommStat & spModuleStat;
            //return 1;
        }

        protected override int SerialConnect(SerialPort sp)
        {
            if (!sp.IsOpen)
            {
                try
                {
                    GlobalLogManager.Instance.ConsoleLog("Connecting to Serial Port(Module)...");

                    sp.PortName = _config.IRPortName;
                    sp.BaudRate = _config.IRBaudRate;
                    sp.Parity = _config.IRParity;
                    sp.DataBits = _config.IRDataBits;
                    sp.StopBits = _config.IRStopBits;

                    sp.DataReceived += OnSerialReceivedModule; // test with tx remove later --> No, We now use UART rx with SPI

                    sp.Open();

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

                ParseReceivedData(Data, _config);
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
                    if (spComm.IsOpen)
                    {
                        spComm.DiscardInBuffer();
                    }
                    received_buffer.Clear();
                    pure_data.Clear();
                }

                int frame_ch_num = frame.Channels();
                var frame_size = frame.Size();

                // Cv2.ImEncode(".jpg", frame, out image_to_send);
                // JPG = compressed foramt >> Encode to bmp. But, bmp contains header info >> Do not use ImEncode
                int img_size = (_config.resolution * _config.resolution * 3);
                if (image_to_send == null || image_to_send.Length != img_size)
                {
                    image_to_send = new byte[img_size];
                }

                if (frame.Empty())
                {
                    for (int i = 0; i < image_to_send.Length; i++)
                    {
                        image_to_send[i] = (byte)(i % 256);
                    }
                    GlobalLogManager.Instance.ConsoleLog($"WARN.. sending test array");
                }
                else Marshal.Copy(frame.Data, image_to_send, 0, image_to_send.Length); // fix 

                fragment_index = 0;
                connectionState = EConnectionState.SendingImage;

                if (_config.is_spi_enable == true) SendImageFragment_SPI();
                else SendImageFragment();
            });
        }

        protected override void OnSerialReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (!spComm.IsOpen) return;

            try
            {
                lock (_rcLock)
                {
                    int bytes_to_read = spComm.BytesToRead;
                    byte[] buffer = new byte[bytes_to_read];
                    int actually_read = spComm.Read(buffer, 0, bytes_to_read);

                    if (actually_read > 0) received_buffer.AddRange(buffer.Take(actually_read));

                    Console.WriteLine("");
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        Console.Write($"{buffer[i]:X2}");
                    }
                    Console.WriteLine("");

                    FindData();
                    if (pure_data.Count >= 1)
                    {
                        footerTryCnt = 0;
                        ProcessReceivedBuffer();
                        pure_data.Clear();
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

            int detected_cnt = pure_data[0];
            pure_data.RemoveAt(0);

            List<Rect> received_rects = new List<Rect>();
            List<IRConfig.EClassArray> received_cls = new List<IRConfig.EClassArray>();
            List<int> received_probs = new List<int>();

            int modelType = pure_data[pure_data.Count - 9];

            if (modelType != 1)
            {
                GlobalLogManager.Instance.ConsoleLog($"ERROR!! ModelTypeError!! receivedType :: {modelType}, currentType :: 1");
                SendModuleChangeNotice(ModuleType.IR);
                ProcessReceivedBuffer();
            }

            byte[] voltageByte = new byte[4];
            voltageByte[0] = pure_data[pure_data.Count - 8];
            voltageByte[1] = pure_data[pure_data.Count - 7];
            voltageByte[2] = pure_data[pure_data.Count - 6];
            voltageByte[3] = pure_data[pure_data.Count - 5];

            byte[] ampereByte = new byte[4];
            ampereByte[0] = pure_data[pure_data.Count - 4];
            ampereByte[1] = pure_data[pure_data.Count - 3];
            ampereByte[2] = pure_data[pure_data.Count - 2];
            ampereByte[3] = pure_data[pure_data.Count - 1];

            pure_data.RemoveRange(pure_data.Count - 9, 9);

            float voltage = ConvertByteArray(voltageByte);
            float ampere = ConvertByteArray(ampereByte);

            for (int i = 0; i < detected_cnt; i++)
            {
                byte[] rectData = pure_data.Take(10).ToArray();
                pure_data.RemoveRange(0, 10);

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

                ratio_x = 512.0f / _config.resolution;
                ratio_y = 512.0f / _config.resolution;

                x_new = (int)(x * ratio_x);
                y_new = (int)(y * ratio_y);
                w_new = (int)(w * ratio_x);
                h_new = (int)(h * ratio_y);

                GlobalLogManager.Instance.ConsoleLog($"Num {i + 1} | class {cls} | probability {prob} :: x={x}, y={y}, w={w}, h={h}");
                GlobalLogManager.Instance.AddLogToFile("DEBUG", $"Num {i + 1} | class {cls} | probability {prob} :: x={x}, y={y}, w={w}, h={h}");

                if (prob >= _config.prob_thres && (cls == 0 || cls == 1))
                {
                    received_cls.Add((IRConfig.EClassArray)cls);
                    received_probs.Add(prob);
                    received_rects.Add(new Rect(x_new, y_new, w_new, h_new));
                }
            }
            PointsReceived?.Invoke(received_rects, received_cls, received_probs);

            connectionState = EConnectionState.Connected;
        }

        private void FindData()
        {
            ReadOnlySpan<byte> bufferSpan = CollectionsMarshal.AsSpan(received_buffer);
            int dataLength;
            int headerIndex = bufferSpan.IndexOf(header);

            if (headerIndex != -1)
            {
                bufferSpan = bufferSpan.Slice(headerIndex + 4);
                int footerIndex = bufferSpan.IndexOf(footer);
                if (footerIndex != -1)
                {
                    dataLength = footerIndex;

                    if (dataLength >= 10 && dataLength % 10 == 0)
                    {
                        pure_data = received_buffer.GetRange(headerIndex + 4, dataLength);
                        received_buffer.RemoveRange(0, headerIndex + footerIndex + 8);
                    }
                    else
                    {
                        GlobalLogManager.Instance.ConsoleLog("ERROR!! Data Length Not Available:: Clear Buffer");
                        received_buffer.RemoveRange(0, headerIndex + footerIndex + 8);
                    }
                }
                else
                {
                    if (footerTryCnt >= 5)
                    {
                        GlobalLogManager.Instance.ConsoleLog($"ERROR!! Wrong Footer:: Clear Buffer");
                        received_buffer.Clear();
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
        public void SendImageFragment()
        {
            if (connectionState == EConnectionState.SendingImage)
            {
                if (_config.is_send_all == true) _config.chunk_size = image_to_send.Length;
                int chunk_size = _config.chunk_size;

                while (connectionState == EConnectionState.SendingImage)
                {
                    int bytes_sent = fragment_index * chunk_size;
                    int remain_bytes = image_to_send.Length - bytes_sent;
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

                        Buffer.BlockCopy(image_to_send, bytes_sent, chunk, 0, bytes_to_send);
                        //Buffer.BlockCopy(new byte[4] { 0x0D, 0x0A, 0x0D, 0x0A }, 0, chunk, bytes_to_send, 4);

                        connectionState = EConnectionState.WaitingForInference;

                        GlobalLogManager.Instance.ConsoleLog($"All image fragments have been sent :: Size={bytes_to_send} bytes");
                        GlobalLogManager.Instance.AddLogToFile("DEBUG", $"All image fragments have been sent :: Size={bytes_to_send} bytes");
                    }
                    else
                    {
                        bytes_to_send = chunk_size;

                        chunk = new byte[chunk_size];
                        Buffer.BlockCopy(image_to_send, bytes_sent, chunk, 0, bytes_to_send);
                    }

                    try
                    {
                        spComm.Write(chunk, 0, chunk.Length);
                        // GlobalLogManager.Instance.ConsoleLog($"OK.. Sent Fragment {fragment_index + 1}:: Size={bytes_to_send} bytes");
                        // GlobalLogManager.Instance.AddLogToFile("DEBUG", $"Sent Fragment {fragment_index + 1}:: Size={bytes_to_send} bytes");
                        fragment_index++;
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

        public void SendImageFragment_SPI()
        {
            if (connectionState != EConnectionState.SendingImage) return;

            if (_config.is_send_all == true)
                _config.chunk_size = image_to_send.Length;

            int chunk_size = _config.chunk_size;
            int chunk_send_count = image_to_send.Length / chunk_size;
            uint bytesWritten = 0;

            byte[] txBuffer = new byte[chunk_size + 3];

            txBuffer[0] = 0x11;                      // send cmd

            int len = chunk_size - 1;

            // Packet length
            txBuffer[1] = (byte)(len & 0xFF);        // Low Byte
            txBuffer[2] = (byte)((len >> 8) & 0xFF); // High Byte

            try
            {
                // [CS Low] Comm Start (ADBUS3 = 0)
                // 0x80(GPIO Setting) + 0x00(CS Low, 나머지 Low) + 0xFB(Direction)
                SetCS_Low(ftdi);

                for (int i = 0; i < chunk_send_count; i++)
                {
                    int offset = i * chunk_size;

                    // Copy and send with SPI
                    Buffer.BlockCopy(image_to_send, offset, txBuffer, 3, chunk_size);
                    FTDI.FT_STATUS status = ftdi.Write(txBuffer, txBuffer.Length, ref bytesWritten);

                    if (status != FTDI.FT_STATUS.FT_OK)
                    {
                        GlobalLogManager.Instance.ConsoleLog($"SPI Write Failed at offset {offset}: {status}");
                        break;
                    }
                }

                // [CS High] Comm Start (ADBUS3 = 1)
                // 0x80(GPIO Setting) + 0x08(CS High) + 0xFB(Direction)
                SetCS_High(ftdi);

                connectionState = EConnectionState.WaitingForInference;

                GlobalLogManager.Instance.ConsoleLog($"SPI Image Transfer Complete. Total: {image_to_send.Length} bytes");
            }
            catch (Exception ex)
            {
                GlobalLogManager.Instance.ConsoleLog($"ERROR in SendImageFragment_SPI: {ex.Message}");
                SetCS_High(ftdi);
                Disconnect();
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

                spModule.Write(cmdArray, 0, 6);
                Console.WriteLine($"Start Measure Command Sent: {cmdArray}");
                // ADD DEBUG LOG
            }
            catch (Exception ex)
            {
                // ADD ERROR LOG
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

        public override void SendModuleChangeNotice(ModuleType moduleType)
        {
            if (!spComm.IsOpen)
            {
                base.SerialConnect(spComm);
            }
            GlobalLogManager.Instance.ConsoleLog($"SendModuleChangeNotice Called in IRService, TargetModule is :: {moduleType}");
            spComm.Write(new byte[] { (byte)moduleType }, 0, 1);
        }

        public void Disconnect()
        {
            base.SPIDisconnect(ftdi);
            SerialDisconnect(spModule);
            base.SerialDisconnect(spComm);

            connectionState = EConnectionState.Disconnected;

            GlobalLogManager.Instance.ConsoleLog("Serial Disconnected");
            GlobalLogManager.Instance.AddLogToFile("DEBUG", "Serial Disconnected");
        }

        protected override void SerialDisconnect(SerialPort sp)
        {
            if ((sp != null && sp.IsOpen))
            {
                try
                {
                    sp.DataReceived -= OnSerialReceivedModule;
                    System.Threading.Thread.Sleep(20);
                    sp.Close();
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
