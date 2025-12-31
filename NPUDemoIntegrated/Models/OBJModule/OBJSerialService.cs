using FTD2XX_NET;
using NPUDemoIntegrated.GlobalManagers;
using OpenCvSharp;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text;

namespace NPUDemoIntegrated.Models.OBJModule
{
    class OBJSerialService
    {
        private readonly OBJConfig _config;
        public OBJSerialService(OBJConfig config)
        {
            _config = config;
        }

        // UART rx and tx separate
        private SerialPort sp_comm = new SerialPort();
        private SerialPort sp_debug = new SerialPort();

        private FTDI _ftdi = new FTDI();
        private bool _isConnected = false;

        private List<byte> received_buffer = new List<byte>();
        private List<byte> pure_data = new List<byte>();

        public event Action<List<Rect>, List<OBJConfig.ClassArray>, List<int>> PointsReceived;
        public event Action<string> StatusChanged;

        private int fragment_index;
        private byte[] image_to_send;

        private int data_length = 0;
        private int footer_try_cnt = 0;

        private readonly object _rc_lock = new object();

        static readonly byte[] header = { 0x10, 0x01, 0x10, 0x01 };
        static readonly byte[] footer = { 0x0D, 0x0A, 0x0D, 0x0A };

        private ConnectionState connectionState = ConnectionState.Disconnected;
        enum ConnectionState
        {
            Disconnected,
            Connecting,
            Connected,
            SendingImage,
            WaitingForInference,
            ProceesingBuffer
        }

        public int Connect()
        {
            try
            {
                if (_config.is_spi_enable == true) Connect_SPI();
            }
            catch (Exception ex)
            {
                GlobalLogManager.Instance.ConsoleLog($"ERROR!! Error while Connecting SPI{ex}");
                GlobalLogManager.Instance.AddLogToFile("ERROR", $"Error while Connecting SPI{ex}");

                return 0;
            }

            if (!sp_comm.IsOpen)
            {
                try
                {
                    GlobalLogManager.Instance.ConsoleLog("Connecting to Serial Port(Common)...");

                    sp_comm.PortName  = _config.portName;
                    sp_comm.BaudRate  = _config.baudRate;
                    sp_comm.Parity    = _config.parity;
                    sp_comm.DataBits  = _config.dataBits;
                    sp_comm.StopBits  = _config.stopBits;

                    sp_comm.DataReceived += OnSerialReceived; // test with tx remove later --> No, We now use UART rx with SPI

                    sp_comm.Open();

                    //sp_tx.Write(new byte[] { 0x10, 0x01, 0x10, 0x01 }, 0, 4);
                    //connectionState = ConnectionState.Connecting; // No need to check Status (Connecting)
                    //StatusChanged?.Invoke("Connecting");
                    connectionState = ConnectionState.Connected;
                    StatusChanged?.Invoke("Connected");

                    GlobalLogManager.Instance.ConsoleLog($"Port(Common) Opend. :: {connectionState}");
                    GlobalLogManager.Instance.AddLogToFile("DEBUG", $"Port(Common) Opend. :: {connectionState}");
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
            }
            return 1;
        }

        public int Connect_SPI()
        {
            if (_isConnected) return 1;

            uint devCount = 0;
            _ftdi.GetNumberOfDevices(ref devCount);

            if (devCount == 0)
            {
                GlobalLogManager.Instance.ConsoleLog("No FTDI devices found.");
                return 0;
            }

            // Get FTDI Device List
            FTDI.FT_DEVICE_INFO_NODE[] deviceList = new FTDI.FT_DEVICE_INFO_NODE[devCount];
            _ftdi.GetDeviceList(deviceList);

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
            FTDI.FT_STATUS status = _ftdi.OpenByIndex((uint)targetIndex);
            if (status != FTDI.FT_STATUS.FT_OK)
            {
                GlobalLogManager.Instance.ConsoleLog($"Open Failed for Index {targetIndex}: {status}");
                return 0;
            }

            try
            {
                // Device Init
                _ftdi.ResetDevice();
                _ftdi.Purge(FTDI.FT_PURGE.FT_PURGE_RX | FTDI.FT_PURGE.FT_PURGE_TX);

                _ftdi.SetCharacters(0, false, 0, false);
                _ftdi.SetTimeouts(1000, 1000);
                _ftdi.SetLatency(1);
                _ftdi.SetFlowControl(FTDI.FT_FLOW_CONTROL.FT_FLOW_RTS_CTS, 0x00, 0x00);

                // Set to MPSSE Mode
                status = _ftdi.SetBitMode(0x00, 0x02);
                if (status != FTDI.FT_STATUS.FT_OK)
                {
                    GlobalLogManager.Instance.ConsoleLog($"SetBitMode Failed: {status}");
                    _ftdi.Close();
                    return 0;
                }

                Thread.Sleep(50);

                // MPSSE Setting
                if (!MPSSEConfig())
                {
                    GlobalLogManager.Instance.ConsoleLog("MPSSE Configuration Failed.");
                    _ftdi.Close();
                    return 0;
                }

                _isConnected = true;
                GlobalLogManager.Instance.ConsoleLog($"FTDI SPI Connected to {deviceList[targetIndex].Description}");
                StatusChanged?.Invoke("Connected");
                return 1;
            }
            catch (Exception ex)
            {
                GlobalLogManager.Instance.ConsoleLog($"Error in Connect_SPI: {ex.Message}");
                _ftdi.Close();
                return 0;
            }
        }

        private bool MPSSEConfig()
        {
            List<byte> cmd = new List<byte>();
            uint bytesWritten = 0;

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

        public Task SerialCommunication(Mat frame)
        {
            return Task.Run(() => {
                lock (_rc_lock)
                {
                    if (sp_comm.IsOpen)
                    {
                        sp_comm.DiscardInBuffer();
                    }
                    received_buffer.Clear();
                    pure_data.Clear();
                }

                int frame_ch_num = frame.Channels();
                var frame_size = frame.Size();

                // Cv2.ImEncode(".jpg", frame, out image_to_send);
                // JPG = compressed foramt >> Encode to bmp. But, bmp contains header info >> Do not use ImEncode
                int img_size = (_config.img_size == ImageSize.S384) ? 442368 : 307200;
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
                connectionState = ConnectionState.SendingImage;
                StatusChanged?.Invoke("SendingImage");

                if (_config.is_spi_enable == true) SendImageFragment_SPI();
                else SendImageFragment();
            });
        }

        private void OnSerialReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (!sp_comm.IsOpen) return;

            try
            {
                lock (_rc_lock)
                {
                    int bytes_to_read = sp_comm.BytesToRead;
                    byte[] buffer = new byte[bytes_to_read];
                    int actually_read = sp_comm.Read(buffer, 0, bytes_to_read);

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
                        footer_try_cnt = 0;
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

        private void OnSerialReceived_Debug(object sender, SerialDataReceivedEventArgs e)
        {
            if (!sp_debug.IsOpen) return;

            try
            {
                int bytes_to_read = sp_debug.BytesToRead; // Test with tx change to rx later
                byte[] buffer = new byte[bytes_to_read];
                sp_debug.Read(buffer, 0, bytes_to_read);  // Test with tx change to rx later

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
            if (connectionState != ConnectionState.WaitingForInference)
            {
                GlobalLogManager.Instance.ConsoleLog($"ERROR!! connectionState Error State:: {connectionState}");
                return;
            }

            connectionState = ConnectionState.ProceesingBuffer;
            StatusChanged?.Invoke("ProcessingBuffer");

            int detected_cnt = pure_data[0];

            pure_data.RemoveAt(0); // remove command byte from buffer
            List<Rect> received_rects = new List<Rect>();
            List<OBJConfig.ClassArray> received_cls = new List<OBJConfig.ClassArray>();
            List<int> received_probs = new List<int>();

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

                if (_config.img_mode == ImageMode.RESIZE)
                {
                    double ratio_x;
                    double ratio_y;

                    if (_config.img_size == ImageSize.S320)
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

                    if (_config.img_size == ImageSize.S320)
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

                if (prob >= _config.prob_thres && cls == 0)
                {
                    received_cls.Add((OBJConfig.ClassArray)cls);
                    received_probs.Add(prob);
                    received_rects.Add(new Rect(x_new, y_new, w_new, h_new));
                }
            }
            PointsReceived?.Invoke(received_rects, received_cls, received_probs);

            connectionState = ConnectionState.Connected;
            StatusChanged?.Invoke("Connected");
        }

        private void FindData()
        {
            ReadOnlySpan<byte> bufferSpan = CollectionsMarshal.AsSpan(received_buffer);

            int header_index = bufferSpan.IndexOf(header);

            if (header_index != -1)
            {
                bufferSpan = bufferSpan.Slice(header_index + 4);
                int footer_index = bufferSpan.IndexOf(footer);
                if (footer_index != -1)
                {
                    data_length = footer_index;

                    if (data_length > 0 && data_length % 10 == 1)
                    {
                        pure_data = received_buffer.GetRange(header_index + 4, data_length);
                        received_buffer.RemoveRange(0, header_index + footer_index + 8);
                    }
                    else
                    {
                        GlobalLogManager.Instance.ConsoleLog("ERROR!! Data Length Not Available:: Clear Buffer");
                        received_buffer.RemoveRange(0, header_index + footer_index + 8);
                    }
                }
                else
                {
                    if (footer_try_cnt >= 5)
                    {
                        GlobalLogManager.Instance.ConsoleLog($"ERROR!! Wrong Footer:: Clear Buffer");
                        received_buffer.Clear();
                        footer_try_cnt = 0;
                    }
                    footer_try_cnt++;
                    GlobalLogManager.Instance.ConsoleLog($"WARN.. No Footer Found in Buffer Find Count:: {footer_try_cnt}");
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
            if (connectionState == ConnectionState.SendingImage)
            { //while (connectionState == ConnectionState.SendingImage) {
                if (_config.is_send_all == true) _config.chunk_size = image_to_send.Length;
                int chunk_size = _config.chunk_size;
                /*
                // Protocol Coomnad, Header, Footer Added & Image Packet Header Removed
                try
                {
                    sp_comm.Write(new byte[9] { 0x10, 0x01, 0x10, 0x01, 0x01, 0x0D, 0x0A, 0x0D, 0x0A }, 0, 9);
                }
                catch (Exception ex)
                {
                    GlobalLogManager.Instance.ConsoleLog($"ERROR!! Error sending start signal: {ex.Message}");
                    GlobalLogManager.Instance.AddLogToFile("ERROR", $"Error sending start signal: {ex.Message}");
                }

                Task.Delay(100);
                
                try
                {
                    sp_comm.Write(new byte[5] { 0x10, 0x01, 0x10, 0x01, 0x02 }, 0, 5);
                }
                catch (Exception ex)
                {
                    GlobalLogManager.Instance.ConsoleLog($"ERROR!! Error sending start signal: {ex.Message}");
                    GlobalLogManager.Instance.AddLogToFile("ERROR", $"Error sending start signal: {ex.Message}");
                }
                */
                while (connectionState == ConnectionState.SendingImage)
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

                        connectionState = ConnectionState.WaitingForInference;
                        StatusChanged?.Invoke("WaitingForInference");

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
                        sp_comm.Write(chunk, 0, chunk.Length);
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
                    /*
                    try
                    {
                        GlobalLogManager.Instance.ConsoleLog($"Waiting For Inference :: {connectionState}");
                        GlobalLogManager.Instance.AddLogToFile("DEBUG", $"Waiting For Inference :: {connectionState}");

                        Task.Delay(100);

                        sp_comm.Write(new byte[9] { 0x10, 0x01, 0x10, 0x01, 0x03, 0x0D, 0x0A, 0x0D, 0x0A }, 0, 9);
                    }
                    catch (Exception ex)
                    {
                        GlobalLogManager.Instance.ConsoleLog($"ERROR!! Error sending inference signal: {ex.Message}");
                        GlobalLogManager.Instance.AddLogToFile("ERROR", $"Error sending inference signal: {ex.Message}");
                    }
                    */
                }
            }
        }

        public void SendImageFragment_SPI()
        {
            if (connectionState != ConnectionState.SendingImage) return;

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
                SetCS_Low();

                for (int i = 0; i < chunk_send_count; i++)
                {
                    int offset = i * chunk_size;

                    // Copy and send with SPI
                    Buffer.BlockCopy(image_to_send, offset, txBuffer, 3, chunk_size);
                    FTDI.FT_STATUS status = _ftdi.Write(txBuffer, txBuffer.Length, ref bytesWritten);

                    if (status != FTDI.FT_STATUS.FT_OK)
                    {
                        GlobalLogManager.Instance.ConsoleLog($"SPI Write Failed at offset {offset}: {status}");
                        break;
                    }
                }

                // [CS High] Comm Start (ADBUS3 = 1)
                // 0x80(GPIO Setting) + 0x08(CS High) + 0xFB(Direction)
                SetCS_High();

                connectionState = ConnectionState.WaitingForInference;
                StatusChanged?.Invoke("WaitingForInference");

                GlobalLogManager.Instance.ConsoleLog($"SPI Image Transfer Complete. Total: {image_to_send.Length} bytes");
            }
            catch (Exception ex)
            {
                GlobalLogManager.Instance.ConsoleLog($"ERROR in SendImageFragment_SPI: {ex.Message}");
                SetCS_High();
                Disconnect();
            }
        }

        // Chip Select (CS) Control : Low
        private void SetCS_Low()
        {
            uint bytesWritten = 0;
            // 0x80: ADBUS Setup Command
            // 0x00: Value (All pins Low, ADBUS3(=CS) to Low)
            // 0xFB: Direction (SK, DO, CS = Output(1), DI = Input(0)) -> 1111 1011
            byte[] cmd = { 0x80, 0x00, 0xFB };
            _ftdi.Write(cmd, cmd.Length, ref bytesWritten);
        }

        // Chip Select (CS) Control : High
        private void SetCS_High()
        {
            uint bytesWritten = 0;
            // 0x08: Value (ADBUS3(CS) High, 나머지 Low) -> 0000 1000
            byte[] cmd = { 0x80, 0x08, 0xFB };
            _ftdi.Write(cmd, cmd.Length, ref bytesWritten);
        }

        public void Disconnect()
        {
            if (_ftdi != null && _ftdi.IsOpen)
            {
                try
                {
                    _ftdi.Purge(FTDI.FT_PURGE.FT_PURGE_RX | FTDI.FT_PURGE.FT_PURGE_TX);
                    _ftdi.SetBitMode(0x00, 0x00);

                    _ftdi.Close();

                    GlobalLogManager.Instance.ConsoleLog("FTDI (SPI) Device Closed & Reset.");
                }
                catch (Exception ex)
                {
                    GlobalLogManager.Instance.ConsoleLog($"ERROR closing FTDI: {ex.Message}");
                    GlobalLogManager.Instance.AddLogToFile("ERROR", ($"ERROR closing FTDI: {ex.Message}"));
                }
            }
            _isConnected = false;

            if ((sp_comm != null && sp_comm.IsOpen))
            { // && (sp_debug != null && sp_debug.IsOpen)
                try
                {
                    sp_comm.DataReceived -= OnSerialReceived; // test with tx change to rx later
                    //sp_debug.DataReceived -= OnSerialReceived_Debug;

                    System.Threading.Thread.Sleep(20);

                    sp_comm.Close();
                    //sp_debug.Close();
                }
                catch (Exception ex)
                {
                    GlobalLogManager.Instance.ConsoleLog($"ERROR!! Error during Disconnect: {ex.Message}");
                    GlobalLogManager.Instance.AddLogToFile("ERROR", $"Error during Disconnect: {ex.Message}");
                }
            }
            connectionState = ConnectionState.Disconnected;
            StatusChanged?.Invoke("Disconnected");

            GlobalLogManager.Instance.ConsoleLog("Serial Disconnected");
            GlobalLogManager.Instance.AddLogToFile("DEBUG", "Serial Disconnected");
        }
    }
}
