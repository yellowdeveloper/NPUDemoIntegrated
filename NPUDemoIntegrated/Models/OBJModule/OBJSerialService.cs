using FTD2XX_NET;
using NPUDemoIntegrated.GlobalManagers;
using OpenCvSharp;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text;

namespace NPUDemoIntegrated.Models.OBJModule
{
    class OBJSerialService: BaseSerialService<OBJConfig>
    {
        public OBJSerialService(OBJConfig config) : base(config) { }

        // UART rx and tx separate
        private SerialPort spComm = new SerialPort();
        private SerialPort spDebug = new SerialPort();

        private FTDI _ftdi = new FTDI();

        private List<byte> received_buffer = new List<byte>();
        private List<byte> pure_data = new List<byte>();

        public event Action<List<Rect>, List<OBJConfig.EClassArray>, List<int>> PointsReceived;
        public event Action<string> StatusChanged;

        private int fragment_index;
        private byte[] image_to_send;
        private int footerTryCnt = 0;

        private readonly object _rc_lock = new object();

        private EConnectionState connectionState = EConnectionState.Disconnected;

        public int Connect()
        {
            int spiStat = 0;
            int spCommStat = 0;

            if (_config.is_spi_enable)
            {
                spiStat = base.SPIConnect(_ftdi);
            }
            else
            {
                spiStat = 1;
            }
            
            spCommStat = base.SerialConnect(spComm);

            if (spiStat == 1 && spCommStat == 1)
            {
                connectionState = EConnectionState.Connected;
                StatusChanged?.Invoke("Connected");
                GlobalLogManager.Instance.ConsoleLog($"OK.. All stats good!, Connected To all the Ports");
            }
            else
            {
                GlobalLogManager.Instance.ConsoleLog($"ERROR!! Serial stat :: {spCommStat} & SPI stat :: {spiStat} \nDisconnect from all ...");
                Disconnect();
            }
            
            return spiStat & spCommStat;
        }

        public Task SerialCommunication(Mat frame)
        {
            return Task.Run(() => {
                lock (_rc_lock)
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
                int img_size = (_config.img_size == EImageSize.S384) ? 442368 : 307200;
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
                StatusChanged?.Invoke("SendingImage");

                if (_config.is_spi_enable == true) SendImageFragment_SPI();
                else SendImageFragment();
            });
        }

        protected override void OnSerialReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (!spComm.IsOpen) return;

            try
            {
                lock (_rc_lock)
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
            StatusChanged?.Invoke("ProcessingBuffer");

            int detected_cnt = pure_data[0];
            pure_data.RemoveAt(0); 

            List<Rect> received_rects = new List<Rect>();
            List<OBJConfig.EClassArray> received_cls = new List<OBJConfig.EClassArray>();
            List<int> received_probs = new List<int>();

            int modelType = pure_data[pure_data.Count - 9];

            if (modelType != 0)
            {
                GlobalLogManager.Instance.ConsoleLog($"ERROR!! ModelTypeError!! receivedType :: {modelType}, currentType :: 0");
                return;
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

                if (_config.img_mode == EImageMode.RESIZE)
                {
                    double ratio_x;
                    double ratio_y;

                    if (_config.img_size == EImageSize.S320)
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

                    if (_config.img_size == EImageSize.S320)
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
                    received_cls.Add((OBJConfig.EClassArray)cls);
                    received_probs.Add(prob);
                    received_rects.Add(new Rect(x_new, y_new, w_new, h_new));
                }
            }
            PointsReceived?.Invoke(received_rects, received_cls, received_probs);

            connectionState = EConnectionState.Connected;
            StatusChanged?.Invoke("Connected");
        }

        private void FindData()
        {
            ReadOnlySpan<byte> bufferSpan = CollectionsMarshal.AsSpan(received_buffer);

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
                SetCS_Low(_ftdi);

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
                SetCS_High(_ftdi);

                connectionState = EConnectionState.WaitingForInference;
                StatusChanged?.Invoke("WaitingForInference");

                GlobalLogManager.Instance.ConsoleLog($"SPI Image Transfer Complete. Total: {image_to_send.Length} bytes");
            }
            catch (Exception ex)
            {
                GlobalLogManager.Instance.ConsoleLog($"ERROR in SendImageFragment_SPI: {ex.Message}");
                SetCS_High(_ftdi);
                Disconnect();
            }
        }

        public override void SendModuleChangeNotice(ModuleType moduleType)
        {
            if (!spComm.IsOpen)
            {
                base.SerialConnect(spComm);
            }
            GlobalLogManager.Instance.ConsoleLog($"SendModuleChangeNotice Called in OBJService, TargetModule is ::{moduleType}");
            spComm.Write(new byte[] { (byte)moduleType }, 0, 1);
        }

        public void Disconnect()
        {
            base.SPIDisconnect(_ftdi);
            base.SerialDisconnect(spComm);

            connectionState = EConnectionState.Disconnected;
            StatusChanged?.Invoke("Disconnected");

            GlobalLogManager.Instance.ConsoleLog("Serial Disconnected");
            GlobalLogManager.Instance.AddLogToFile("DEBUG", "Serial Disconnected");
        }
    }
}
