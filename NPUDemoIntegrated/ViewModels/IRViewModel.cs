using Iot.Device.Mcp25xxx;
using Iot.Device.Nmea0183;
using NPUDemoIntegrated.GlobalManagers;
using NPUDemoIntegrated.Models;
using NPUDemoIntegrated.Models.IRModule;
using NPUDemoIntegrated.Models.OBJModule;
using NPUDemoIntegrated.Utils;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace NPUDemoIntegrated.ViewModels
{
    class IRViewModel : BaseViewModel
    {
        public static readonly uint[] colormap = { 0, 0, 4, 1, 0, 5, 1, 1, 6, 1, 1, 8, 2, 1, 10, 2, 2, 12, 2, 2, 14, 3, 2, 16, 4, 3, 18, 4,
            3, 20, 5, 4, 23, 6, 4, 25, 7, 5, 27, 8, 5, 29, 9, 6, 31, 10, 7, 34, 11, 7, 36, 12, 8, 38, 13, 8, 41, 14, 9, 43, 16, 9, 45, 17,
            10, 48, 18, 10, 50, 20, 11, 52, 21, 11, 55, 22, 11, 57, 24, 12, 60, 25, 12, 62, 27, 12, 65, 28, 12, 67, 30, 12, 69, 31, 12, 72,
            33, 12, 74, 35, 12, 76, 36, 12, 79, 38, 12, 81, 40, 11, 83, 41, 11, 85, 43, 11, 87, 45, 11, 89, 47, 10, 91, 49, 10, 92, 50, 10,
            94, 52, 10, 95, 54, 9, 97, 56, 9, 98, 57, 9, 99, 59, 9, 100, 61, 9, 101, 62, 9, 102, 64, 10, 103, 66, 10, 104, 68, 10, 104, 69, 10,
            105, 71, 11, 106, 73, 11, 106, 74, 12, 107, 76, 12, 107, 77, 13, 108, 79, 13, 108, 81, 14, 108, 82, 14, 109, 84, 15, 109, 85, 15, 109,
            87, 16, 110, 89, 16, 110, 90, 17, 110, 92, 18, 110, 93, 18, 110, 95, 19, 110, 97, 19, 110, 98, 20, 110, 100, 21, 110, 101, 21, 110, 103, 22,
            110, 105, 22, 110, 106, 23, 110, 108, 24, 110, 109, 24, 110, 111, 25, 110, 113, 25, 110, 114, 26, 110, 116, 26, 110, 117, 27, 110, 119, 28, 109,
            120, 28, 109, 122, 29, 109, 124, 29, 109, 125, 30, 109, 127, 30, 108, 128, 31, 108, 130, 32, 108, 132, 32, 107, 133, 33, 107, 135, 33, 107, 136, 34,
            106, 138, 34, 106, 140, 35, 105, 141, 35, 105, 143, 36, 105, 144, 37, 104, 146, 37, 104, 147, 38, 103, 149, 38, 103, 151, 39, 102, 152, 39, 102, 154,
            40, 101, 155, 41, 100, 157, 41, 100, 159, 42, 99, 160, 42, 99, 162, 43, 98, 163, 44, 97, 165, 44, 96, 166, 45, 96, 168, 46, 95, 169, 46, 94, 171, 47,
            94, 173, 48, 93, 174, 48, 92, 176, 49, 91, 177, 50, 90, 179, 50, 90, 180, 51, 89, 182, 52, 88, 183, 53, 87, 185, 53, 86, 186, 54, 85, 188, 55, 84, 189,
            56, 83, 191, 57, 82, 192, 58, 81, 193, 58, 80, 195, 59, 79, 196, 60, 78, 198, 61, 77, 199, 62, 76, 200, 63, 75, 202, 64, 74, 203, 65, 73, 204, 66, 72,
            206, 67, 71, 207, 68, 70, 208, 69, 69, 210, 70, 68, 211, 71, 67, 212, 72, 66, 213, 74, 65, 215, 75, 63, 216, 76, 62, 217, 77, 61, 218, 78, 60, 219, 80,
            59, 221, 81, 58, 222, 82, 56, 223, 83, 55, 224, 85, 54, 225, 86, 53, 226, 87, 52, 227, 89, 51, 228, 90, 49, 229, 92, 48, 230, 93, 47, 231, 94, 46, 232, 96,
            45, 233, 97, 43, 234, 99, 42, 235, 100, 41, 235, 102, 40, 236, 103, 38, 237, 105, 37, 238, 106, 36, 239, 108, 35, 239, 110, 33, 240, 111, 32, 241, 113, 31,
            241, 115, 29, 242, 116, 28, 243, 118, 27, 243, 120, 25, 244, 121, 24, 245, 123, 23, 245, 125, 21, 246, 126, 20, 246, 128, 19, 247, 130, 18, 247, 132, 16, 248,
            133, 15, 248, 135, 14, 248, 137, 12, 249, 139, 11, 249, 140, 10, 249, 142, 9, 250, 144, 8, 250, 146, 7, 250, 148, 7, 251, 150, 6, 251, 151, 6, 251, 153, 6,
            251, 155, 6, 251, 157, 7, 252, 159, 7, 252, 161, 8, 252, 163, 9, 252, 165, 10, 252, 166, 12, 252, 168, 13, 252, 170, 15, 252, 172, 17, 252, 174, 18, 252, 176,
            20, 252, 178, 22, 252, 180, 24, 251, 182, 26, 251, 184, 29, 251, 186, 31, 251, 188, 33, 251, 190, 35, 250, 192, 38, 250, 194, 40, 250, 196, 42, 250, 198, 45,
            249, 199, 47, 249, 201, 50, 249, 203, 53, 248, 205, 55, 248, 207, 58, 247, 209, 61, 247, 211, 64, 246, 213, 67, 246, 215, 70, 245, 217, 73, 245, 219, 76, 244, 221,
            79, 244, 223, 83, 244, 225, 86, 243, 227, 90, 243, 229, 93, 242, 230, 97, 242, 232, 101, 242, 234, 105, 241, 236, 109, 241, 237, 113, 241, 239, 117, 241, 241, 121,
            242, 242, 125, 242, 244, 130, 243, 245, 134, 243, 246, 138, 244, 248, 142, 245, 249, 146, 246, 250, 150, 248, 251, 154, 249, 252, 157, 250, 253, 161, 252, 255, 164
        };

        private readonly IRSerialService _serialService;
        private LeptonService leptonService;

        public byte[] lepton_render = new byte[57600];
        public IRConfig irConfig { get; }
        public SerialConfig serialConfig { get; }

        private readonly Timer _timer;
        private DispatcherTimer _measureTimer;
        private readonly Stopwatch _stopwatch = new Stopwatch();

        private readonly object _frame_lock = new object();
        private readonly object _bbox_lock = new object();
        private readonly object _send_lock = new object();

        public override ICommand ConnectCommand { get; }
        public override ICommand DisconnectCommand { get; }

        public ICommand ModuleConnectCommand { get; }
        public ICommand ModuleDisconnectCommand { get; }
        public ICommand AutoMeasureToggleCommand { get; }
        public ICommand SendCommand { get; }
        public ICommand ToggleModuleMenu { get; }
        private ICommand _moduleCommand;


        private List<OpenCvSharp.Rect> _bbox = new List<OpenCvSharp.Rect>();
        public float[] pixelArray => _serialService.Data.pixelTempArray;
        public float sensorTemp => _serialService.Data.sensorTemp;
        private float[] _processedBuffer;
        
        public WriteableBitmap colorBitmap { get; private set; }
        public BitmapSource _bitmapShow;
        public Mat colorMat;
        public Mat colorMatShow;
        
        private double _rtFps = 0.0;
        private double _fps = 0.0;
        private float _volt = 0.0f;
        private float _amp = 0.0f;
        private float _pixelMax;
        private string _modColor;
        private bool _isSendAuto = false;
        private bool _isInterpolate = false;
        private bool _isModuleConfigOpen = false;

        private bool _isSending = false;
        private int _tryCount = 0;
        private bool isUpdated = false;

        private CancellationTokenSource dummyToken;
        private int dummyCount = 0;

        public override string title => "Doksan NPU Real-Time Vision AI Demonstration";
        public override string subTitle => "Real-time Infrared Image input and on-device object detection inference";

        public string cn_dn
        {
            get
            {
                if (_serialService.connectionState == EConnectionState.Disconnected)
                {
                    return "White"; // Disonnected
                }
                else
                {
                    return "Red";   // Connected
                }
            }
        }
        public bool isSendAuto
        {
            get => _isSendAuto;
            set
            {
                _isSendAuto = value;
                OnPropertyChanged();

                if (_isSendAuto)
                {
                    _timer.Change(0, 10);
                    GlobalLogManager.Instance.ConsoleLog("Auto Send Enabled");
                    GlobalLogManager.Instance.AddLogToFile("DEBUG", "Auto Send Enabled");
                }
                else
                {
                    _timer.Change(Timeout.Infinite, Timeout.Infinite);
                }
            }
        }

        public bool isInterpolate
        {
            get { return _isInterpolate; }
            set
            {
                _isInterpolate = value;
                OnPropertyChanged();
            }
        }

        public bool isModuleConfigOpen
        {
            get => _isModuleConfigOpen;
            set { _isModuleConfigOpen = value; OnPropertyChanged(); }
        }

        public double fps
        {
            get { return _fps; }
            set
            {
                _fps = value;
                OnPropertyChanged();
            }
        }
        public double rtFps
        {
            get { return _rtFps; }
            set
            {
                _rtFps = value;
                OnPropertyChanged();
            }
        }

        public float volt
        {
            get => _volt;
            set { _volt = value; OnPropertyChanged(); }
        }

        public float amp
        {
            get => _amp;
            set { _amp = value; OnPropertyChanged(); }
        }

        public BitmapSource bitmapShow
        {
            get => _bitmapShow;
            set { _bitmapShow = value; OnPropertyChanged(); }
        }
        public ICommand ModuleCommand
        {
            get => _moduleCommand;
            set { _moduleCommand = value; OnPropertyChanged(); }
        }
        public float pixelMax
        {
            get { return _pixelMax; }
            set { _pixelMax = value; OnPropertyChanged(); }
        }

        public string modColor
        {
            get { return _modColor; }
            set { _modColor = value; OnPropertyChanged(); }
        }

        public IRViewModel(SerialConfig _serialConfig, IRConfig _irConfig, IRSerialService service)
        {
            _serialService = service;
            irConfig = _irConfig;
            serialConfig = _serialConfig;

            _serialService.PointsReceived += OnPointsReceived;

            _measureTimer = new DispatcherTimer();
            _measureTimer.Interval = TimeSpan.FromMilliseconds(500);
            _measureTimer.Tick += (s, e) =>
            {
                _serialService.StartMeasure();
            };

            colorBitmap = new WriteableBitmap(irConfig.resolution, irConfig.resolution, 96, 96, System.Windows.Media.PixelFormats.Bgr32, null); 

            _processedBuffer = new float[irConfig.resolution * irConfig.resolution];

            _serialService._sharedStatus.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SharedStatus.connectionState))
                {
                    OnPropertyChanged(nameof(cn_dn));
                }
            };

            irConfig.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(IRConfig.resolution))
                {
                    int resolution = irConfig.resolution;
                    colorBitmap = new WriteableBitmap(resolution, resolution, 96, 96, System.Windows.Media.PixelFormats.Bgr32, null);
                    _processedBuffer = new float[irConfig.resolution * irConfig.resolution];

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        OnPropertyChanged(nameof(colorBitmap));
                    });
                }
            };

            _serialService.Data.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(IRModuleData.pixelTempArray))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        UpdateColorBitmap();
                    });
                }
                
                if (e.PropertyName == nameof(IRModuleData.sensorTemp))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        OnPropertyChanged(nameof(sensorTemp));
                    });
                }

                if (e.PropertyName == nameof(IRModuleData.fps))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        rtFps = _serialService.Data.fps;
                    });
                }
            };

            ToggleModuleMenu = new RelayCommand(param => {
                isModuleConfigOpen = !isModuleConfigOpen;
            });

            ConnectCommand = new RelayCommand(param => {
                if (_serialService.Connect() == 1) ButtonCommand = DisconnectCommand;
                if (is_menu_open) is_menu_open = !is_menu_open;
                //Debug.Write("\nConnect button clicked");
            });

            DisconnectCommand = new RelayCommand(param => {
                Task.Run(() => _serialService.Disconnect());
                ButtonCommand = ConnectCommand;
                if (is_menu_open) is_menu_open = !is_menu_open;
                //Debug.Write("\nDisconnect button clicked");
            });

            ButtonCommand = ConnectCommand;

            ModuleConnectCommand = new RelayCommand(param =>
            {
                if (_serialService.ModuleConnect() == 1)
                {
                    ModuleCommand = ModuleDisconnectCommand;
                    modColor = "Red";
                }
                else
                {
                    WindowPopUp.ErrorWindowPopUp("No Connection With Module. Check Your Connection.");
                }

                if (is_menu_open) is_menu_open = !is_menu_open;
            });

            ModuleDisconnectCommand = new RelayCommand(param =>
            {
                Task.Run(() => _serialService.ModuleDisconnect());
                ModuleCommand = ModuleConnectCommand;
                modColor = "White";
                if (is_menu_open) is_menu_open = !is_menu_open;
            });

            ModuleCommand = ModuleConnectCommand;

            AutoMeasureToggleCommand = new RelayCommand(param =>
            {
                if (param is bool isChecked)
                {
                    if (isChecked)
                        _measureTimer.Start();
                    else
                        _measureTimer.Stop();
                }
            });

            SendCommand = new RelayCommand(async param => {
                _tryCount = 20;
                isSendAuto = false;
                await SendFramePeriodically();
                if (is_menu_open) is_menu_open = !is_menu_open;
            });

            _timer = new Timer(async (_) => await SendFramePeriodically(), null, Timeout.Infinite, Timeout.Infinite);
        }

        private async Task SendFramePeriodically()
        {
            GlobalLogManager.Instance.ConsoleLog($"In SendFramePeriodically Key Status :: sending? {_isSending}, state? {_serialService.connectionState}, auto?{isSendAuto}");
            if (!_isSending && _tryCount >= 10 && _serialService.connectionState == EConnectionState.WaitingForInference && isUpdated)
            {
                _serialService.connectionState = EConnectionState.Connected;
                GlobalLogManager.Instance.ConsoleLog($"WARN.. SendFrame Re-Called: connection_status set to: {_serialService.connectionState}");
                GlobalLogManager.Instance.AddLogToFile("DEBUG", $"SendFrame Re-Called: connection_status set to: {_serialService.connectionState}");
            }

            if (!_isSending && _serialService.connectionState == EConnectionState.Connected && isUpdated)
            {
                _isSending = true;

                GlobalLogManager.Instance.ConsoleLog($"OK.. Image Sending Method Called ... ");
                GlobalLogManager.Instance.AddLogToFile("DEBUG", $"Image Sending Method Called ... ");

                _tryCount = 0;
                Mat mat_tmp = new Mat();

                lock (_frame_lock)
                {
                    //Debug.Write("\nFrameLock Called");
                    if (colorMat == null)
                    {
                        GlobalLogManager.Instance.ConsoleLog($"ERROR!! frame empty, return");
                        _isSending = false;
                        return;
                    }
                    else { mat_tmp = colorMat.Clone(); }
                }

                Mat converted = new Mat();
                Cv2.Resize(mat_tmp, converted, new OpenCvSharp.Size(160, 160), 0, 0, InterpolationFlags.Linear);
                Cv2.CvtColor(converted, converted, ColorConversionCodes.BGRA2RGB);
                //colorMatShow = resized; // test

                lock (_send_lock)
                {
                    //Debug.Write("\nSendLock Called");
                    colorMatShow?.Dispose();
                    colorMatShow = mat_tmp.Clone();
                }
                mat_tmp.Dispose();

                try
                {
                    Debug.Write("\nSerialCommunication Called");
                    //Cv2.ImShow("Debug - Sent Image", converted);
                    //Cv2.WaitKey(0);
                    await _serialService.SerialCommunication(converted);
                }
                finally
                {
                    isUpdated = false;
                    _isSending = false;
                    converted.Dispose();
                }
            }
            else if (_serialService.connectionState == EConnectionState.WaitingForInference && _isSendAuto)
            {
                //GlobalLogManager.Instance.ConsoleLog($"SendFrame Failed ... is_sending: {_is_sending}  connection_status: {_connection_status}  try_count: {_try_count}"  );
                //GlobalLogManager.Instance.AddLogToFile("ERROR", $"SendFrame Failed ... is_sending: {_is_sending}  connection_status: {_connection_status}  try_count: {_try_count}");
                _tryCount++;
            }
        }

        private void OnPointsReceived(float watt, float ampere, List<OpenCvSharp.Rect> b_box, List<IRConfig.EClassArray> cls, List<int> prob)
        {
            string save_path = Path.Combine(GlobalConfigManager.Instance.GetImageFolderPath(), GlobalConfigManager.Instance.GetNowImageFileName());
            GlobalLogManager.Instance.ConsoleLog($"OK.. Points Received ... Drawing Bbox");
            GlobalLogManager.Instance.AddLogToFile("DEBUG", "Points Received ... Drawing Bbox");
            int cnt = 0;

            lock (_bbox_lock)
            {
                _bbox = b_box;
            }

            Mat frame_to_draw;
            lock (_send_lock)
            {
                if (colorMatShow == null || colorMatShow.Empty()) return;
                frame_to_draw = colorMatShow.Clone();
            }


            List<OpenCvSharp.Rect> text_boxs = new List<OpenCvSharp.Rect>();
            Mat resized = new Mat();
            lock (_bbox_lock)
            {
                if (irConfig.useLepton) Cv2.Resize(frame_to_draw, resized, new OpenCvSharp.Size(640, 480), 0, 0, InterpolationFlags.Linear);
                else resized = UtilsForMatImage.Resize(frame_to_draw, 512);

                foreach (var box in _bbox)
                {
                    FindMaxTempInFace(cls[cnt], box);

                    Scalar rectColor = new Scalar(68, 156, 74, 255);  // Dark Green
                    Scalar textColor = new Scalar(255, 255, 255, 255);  // White
                    Cv2.Rectangle(resized, box, rectColor, 2);

                    text_boxs.Add(UtilsForMatImage.DrawTextWithBox(resized, rectColor, textColor, cls[cnt], prob[cnt], box, text_boxs));
                    cnt++;
                }

                frame_to_draw.Dispose();
                // Cv2.ImWrite(save_path, frame_to_draw);
            }

            GlobalLogManager.Instance.ConsoleLog("OK.. Bbox drawing Completed ... Check Image\n");
            GlobalLogManager.Instance.AddLogToFile("DEBUG", "Bbox drawing Completed ... Check Image\n");

            // BitmapSource bitmap_tmp = frame_to_draw.ToBitmapSource();
            // bitmap_tmp.Freeze();

            _stopwatch.Stop();
            var elapsed = _stopwatch.Elapsed.TotalSeconds;
            _stopwatch.Restart();

            Application.Current.Dispatcher.Invoke(() => {
                BitmapSource bitmap_tmp = resized.ToBitmapSource();
                bitmap_tmp.Freeze();

                bitmapShow = bitmap_tmp;
                fps = 1 / elapsed;

                this.volt = watt;
                this.amp = ampere;

                resized.Dispose();
            });

            GlobalLogManager.Instance.ConsoleLog($"Frame Rate:: {fps}");

            // frame_to_draw.Dispose();
        }

        unsafe private void UpdateColorBitmap()
        {
            // Console.WriteLine($"[Update] Hash: {this.GetHashCode()}, IsInterp: {isInterpolate}");
            if (irConfig.useLepton)
            {
                colorBitmap.WritePixels(new Int32Rect(0, 0, 160, 120), lepton_render, 160 * 3, 0);
                OnPropertyChanged(nameof(colorBitmap));

                lock (_frame_lock)
                {
                    if (colorMat == null || colorMat.Rows != 160 || colorMat.Channels() != 4)
                    {
                        colorMat?.Dispose();
                        colorMat = new Mat(160, 160, MatType.CV_8UC4);
                    }

                    using (Mat rawMat = new Mat(120, 160, MatType.CV_8UC3))
                    {
                        System.Runtime.InteropServices.Marshal.Copy(lepton_render, 0, rawMat.Data, lepton_render.Length);
                        Cv2.CvtColor(rawMat, colorMat, ColorConversionCodes.BGR2BGRA);
                    }
                }

                isUpdated = true;
                return;
            }

            float[] data = pixelArray;

            int resolution = irConfig.resolution;

            if (_isInterpolate) Interpolate(data, resolution);
            else ResizeBitmap(data, resolution);

            bool communicationState = !_isSending && _serialService.connectionState == EConnectionState.Connected;

            colorBitmap.Lock();

            uint* pUIBackBuffer = (uint*)colorBitmap.BackBuffer;

            uint* pNPUData = null;

            lock (_frame_lock)
            {
                if (communicationState)
                {
                    if (colorMat == null || colorMat.Rows != resolution)
                    {
                        colorMat?.Dispose();
                        colorMat = new Mat(resolution, resolution, MatType.CV_8UC4);
                    }
                    pNPUData = (uint*)colorMat.DataPointer;
                }

                for (int i = 0; i < _processedBuffer.Length; i++)
                {
                    pUIBackBuffer[i] = TempDataToColor(_processedBuffer[i]);
                    if (pNPUData != null)
                    {
                        pNPUData[i] = TempDataToColor(_processedBuffer[i]);
                    }
                }
            }

            colorBitmap.AddDirtyRect(new Int32Rect(0, 0, resolution, resolution));
            colorBitmap.Unlock();

            isUpdated = true;

            OnPropertyChanged(nameof(colorBitmap));
        }

        public uint TempDataToColor(float pixelTemp)
        {
            float minTemp = irConfig.minTemp;
            float maxTemp = irConfig.maxTemp;

            float alpha = (pixelTemp - minTemp) / (maxTemp - minTemp);
            alpha = Math.Clamp(alpha, 0.0f, 1.0f);

            Color cMin = Color.FromArgb(0, 0, 80);
            Color cMid = Color.FromArgb(255, 60, 0);
            Color cMax = Color.FromArgb(255, 255, 0);

            byte r;
            byte g;
            byte b;

            if (alpha < 0.5)
            {
                double t = alpha / 0.5;
                r = (byte)(255 * t);
                g = 0;
                b = (byte)(160 * (1 - t) + 80);
            }
            else
            {
                double t = (alpha - 0.5) / 0.5; // 0 → 1
                r = (byte)(cMid.R + (cMax.R - cMid.R) * t);
                g = (byte)(cMid.G + (cMax.G - cMid.G) * t);
                b = (byte)(cMid.B + (cMax.B - cMid.B) * t);
            }

            return (uint)((255 << 24) | (r << 16) | (g << 8) | b);
        }

        private void ResizeBitmap(float[] array, int resolution)
        {
            int arrSize = resolution * resolution;
            int multiplied = resolution / 32;
            //float[] tmpArray = new float[arrSize];

            for (int i = 0; i < 32; i++)
            {
                for (int j = 0; j < 32; j++)
                {
                    float val = array[(i * 32) + j];

                    for (int k = 0; k < multiplied; k++)
                    {
                        for (int l = 0; l < multiplied; l++)
                        {
                            int currentX = j * multiplied + l;
                            int currentY = i * multiplied + k;
                            _processedBuffer[currentX + currentY * resolution] = val;
                        }
                    }
                }
            }

            //return tmpArray;
        }

        private void Interpolate(float[] array, int resolution)
        {
            int arrSize = resolution * resolution;
            int multiplied = resolution / 32;
            // float[] tmpArray = new float[arrSize];

            for (int i = 0; i < 32; i++)
            {
                for (int j = 0; j < 32; j++)
                {
                    int currentCoord = (i * 32) + j;

                    float X1Val = array[currentCoord]; // current val
                    float X2Val = (j < 31) ? array[currentCoord + 1] : X1Val;
                    float Y1Val = (i < 31) ? array[currentCoord + 32] : X1Val;
                    float Y2Val = (j < 31 && i < 31) ? array[currentCoord + 33] : X2Val;

                    for (int k = 0; k < multiplied; k++)
                    {
                        float leftVerticalInterp = X1Val + (Y1Val - X1Val) * ((float)k / multiplied);
                        float rightVerticalInterp = X2Val + (Y2Val - X2Val) * ((float)k / multiplied);

                        for (int l = 0; l < multiplied; l++)
                        {
                            int currentX = j * multiplied + l;
                            int currentY = i * multiplied + k;

                            float val = leftVerticalInterp + ((rightVerticalInterp - leftVerticalInterp) * (float)(l) / multiplied);

                            _processedBuffer[currentX + currentY * resolution] = val;
                        }
                    }
                }
            }

            //return tmpArray;
        }

        private void FindMaxTempInFace(IRConfig.EClassArray cls, OpenCvSharp.Rect box)
        {
            if (irConfig.useLepton)
            {
                double downsizeRatioX = 160.0f / 640.0f;
                double downsizeRatioY = 120.0f / 480.0f;

                if (cls == IRConfig.EClassArray.face)
                {
                    int orgTLX = box.X;
                    int orgTLY = box.Y;
                    int orgBRX = box.X + box.Width - 1;
                    int orgBRY = box.Y + box.Height - 1;

                    int newTLX = (int)Math.Round(orgTLX * downsizeRatioX);
                    int newTLY = (int)Math.Round(orgTLY * downsizeRatioY);
                    int newBRX = (int)Math.Round(orgBRX * downsizeRatioX);
                    int newBRY = (int)Math.Round(orgBRY * downsizeRatioY);

                    newTLX = Math.Clamp(newTLX, 0, 159);
                    newTLY = Math.Clamp(newTLY, 0, 119);
                    newBRX = Math.Clamp(newBRX, 0, 159);
                    newBRY = Math.Clamp(newBRY, 0, 119);

                    float maxTemp = 0.0f;

                    for (int y = newTLY; y <= newBRY; y++)
                    {
                        for (int x = newTLX; x <= newBRX; x++)
                        {
                            int pixelIdx = (y * 160 + x) * 3;

                            byte b = lepton_render[pixelIdx];
                            byte g = lepton_render[pixelIdx + 1];
                            byte r = lepton_render[pixelIdx + 2];

                            float temp = DecalcTemp(r, g, b);

                            if (temp > maxTemp)
                            {
                                maxTemp = temp;
                            }
                        }
                    }
                    pixelMax = maxTemp;
                }
                return;
            }

            if (_serialService.Data.pixelTempArray == null)
            {
                pixelMax = 0.0f;
                return;
            }

            double downsizeRatio = 32.0f / irConfig.resolution;
            
            if (cls == IRConfig.EClassArray.face)
            {
                int orgTLX = box.X;
                int orgTLY = box.Y;
                int orgBRX = box.X + box.Width - 1;
                int orgBRY = box.Y + box.Height - 1;

                int newTLX = (int)Math.Round(orgTLX * downsizeRatio);
                int newTLY = (int)Math.Round(orgTLY * downsizeRatio);
                int newBRX = (int)Math.Round(orgBRX * downsizeRatio);
                int newBRY = (int)Math.Round(orgBRY * downsizeRatio);

                newTLX = Math.Clamp(newTLX, 0, 31);
                newTLY = Math.Clamp(newTLY, 0, 31);
                newBRX = Math.Clamp(newBRX, 0, 31);
                newBRY = Math.Clamp(newBRY, 0, 31);

                int startIndex = newTLY * 32 + newTLX;
                int endIndex = newBRY * 32 + newBRX;

                float maxTemp = 0.0f;

                for (int i = startIndex; i < endIndex; i++)
                {
                    if (maxTemp < _serialService.Data.pixelTempArray[i])
                    {
                        maxTemp = _serialService.Data.pixelTempArray[i];
                    }
                }
                pixelMax = maxTemp;
            }
            // else pixelMax = 0.0f;
        }

        public override void DeactivateModule(EModuleType targetModule)
        {
            while (_serialService.connectionState == EConnectionState.SendingImage)
            {
                GlobalLogManager.Instance.ConsoleLog($"now connection state ::{_serialService.connectionState} wait until sending is finished");
                Thread.Sleep(5);
            }
            GlobalLogManager.Instance.ConsoleLog($"now connection state ::{_serialService.connectionState}");

            isSendAuto = false;
            _measureTimer.Stop();

            Thread.Sleep(10);

            _serialService.SendModuleChangeNotice(targetModule);
            _serialService.ModuleDisconnect();

            ModuleCommand = ModuleConnectCommand;

            _serialService.SerialReceiveEventDispose();

            dummyToken.Cancel();
            dummyToken.Dispose();
            dummyToken = null;

            Thread.Sleep(10);
        }


        public override void ActivateModule()
        {
            if (irConfig.useLepton)
            {
                // Real
                //leptonService = new LeptonService();

                //leptonService.FrameUpdate += OnFrameUpdate;

                // leptonService.LeptonInitialize();
                // Test
                dummyToken = new CancellationTokenSource();
                Task.Run(() => DummyLoopAsync(dummyToken.Token));
            }

            _serialService.SerialReceiveEventSubscribe();
        }

        public override void Dispose()
        {
            if (irConfig.useLepton)
            {
                // Real
                //leptonService.FrameUpdate -= OnFrameUpdate;
                //leptonService = null

                // Test
                dummyToken.Cancel();
            }

            _timer.Dispose();
            _serialService.PointsReceived -= OnPointsReceived;

            if (colorMat != null) colorMat.Dispose();
            if (colorMatShow != null) colorMatShow.Dispose();
        }

        // Lepton Using Method
        private void OnFrameUpdate(byte[] evt)
        {
            try
            {
                MapInferno(evt);

                if (Application.Current == null) return;
                Application.Current.Dispatcher.Invoke(new Action(() => {
                    if (colorBitmap == null || colorBitmap.PixelWidth != 160 || colorBitmap.PixelHeight != 120)
                    {
                        colorBitmap = new WriteableBitmap(160, 120, 96, 96, System.Windows.Media.PixelFormats.Bgr24, null);
                    }
                    UpdateColorBitmap();
                }));
            }
            catch (Exception ex)
            {
                GlobalLogManager.Instance.ConsoleLog($"ERROR!! Error While Updating Frame :: {ex}");
                GlobalLogManager.Instance.AddLogToFile("ERROR", $"Error While Updating Frame :: {ex}");
            }
        }

        private void MapInferno(byte[] frame)
        {
            float min = irConfig.minTemp * 100.0f + 27000.0f;
            float max = irConfig.maxTemp * 100.0f + 27000.0f;
            float scale = 255.0f / (max - min);

            int imageBufferIndex = 0;

            for (int i = 0; i < 19680; i++)
            {
                // Jump every headet in packet (4 bytes)
                if ((i % 82) < 2) continue;

                UInt16 rawValue = (UInt16)((frame[i * 2] << 8) + frame[i * 2 + 1]);

                //Console.Write($"{rawValue} ");
                //if ((i % 82) == 0) Console.WriteLine("");

                if (rawValue < min) rawValue = (UInt16)min;
                if (rawValue > max) rawValue = (UInt16)max;

                int value = (int)((rawValue - min) * scale);

                int ofs_r = 3 * value + 0; if (colormap.Length <= ofs_r) ofs_r = colormap.Length - 1;
                int ofs_g = 3 * value + 1; if (colormap.Length <= ofs_g) ofs_g = colormap.Length - 1;
                int ofs_b = 3 * value + 2; if (colormap.Length <= ofs_b) ofs_b = colormap.Length - 1;

                lepton_render[imageBufferIndex + 0] = (byte)colormap[ofs_b];
                lepton_render[imageBufferIndex + 1] = (byte)colormap[ofs_g];
                lepton_render[imageBufferIndex + 2] = (byte)colormap[ofs_r];

                imageBufferIndex += 3;
            }

            //Console.WriteLine("");
        }

        private float DecalcTemp(int r, int g, int b)
        {
            int index = 0;
            int minDistance = 0;

            for (int i = 0; i < 256; i++)
            {
                int MapR = (int)colormap[3 * i + 0];
                int MapG = (int)colormap[3 * i + 1];
                int MapB = (int)colormap[3 * i + 2];

                int distanceR = r - MapR;
                int distanceG = g - MapG;
                int distanceB = b - MapB;

                int totalDistance = distanceR*distanceR + distanceG*distanceG + distanceB*distanceB;

                if (totalDistance == 0)
                {
                    index = i;
                    break;
                }

                if (totalDistance < minDistance)
                {
                    minDistance = totalDistance;
                    index = i;
                }
            }

            float min = irConfig.minTemp * 100.0f + 27000.0f;
            float max = irConfig.maxTemp * 100.0f + 27000.0f;

            float rawValue = index * ((max - min) / 255.0f) + min;

            return (rawValue - 27000.0f) / 100.0f;
        }

        private void LoadDummyImageToRenderBuffer(string imagePath)
        {
            if (!System.IO.File.Exists(imagePath))
            {
                GlobalLogManager.Instance.ConsoleLog($"Test image not found: {imagePath}");
                return;
            }

            using (Mat img = Cv2.ImRead(imagePath, ImreadModes.Color))
            {
                if (img.Empty()) return;

                using (Mat resized = new Mat())
                {
                    Cv2.Resize(img, resized, new OpenCvSharp.Size(160, 120));

                    System.Runtime.InteropServices.Marshal.Copy(resized.Data, lepton_render, 0, 57600);
                }
            }
        }

        private async Task DummyLoopAsync(CancellationToken cts)
        {
            string dummyImagePath;

            while (!cts.IsCancellationRequested)
            {
                if (dummyCount % 4 == 0)
                    dummyImagePath = @"C:\Users\user\Downloads\doksan_dev\lepton_test_0.png";
                else
                    dummyImagePath = @$"C:\Users\user\Downloads\doksan_dev\lepton_t{dummyCount % 4}.png";

                Console.WriteLine($"[DummyLoop] Loading dummy image to render buffer...");
                try
                {
                    LoadDummyImageToRenderBuffer(dummyImagePath);

                    if (Application.Current != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (colorBitmap == null || colorBitmap.PixelWidth != 160 || colorBitmap.PixelHeight != 120)
                            {
                                colorBitmap = new WriteableBitmap(160, 120, 96, 96, System.Windows.Media.PixelFormats.Bgr24, null);
                            }
                            UpdateColorBitmap();
                        });
                    }
                    dummyCount++;
                }
                catch (Exception ex)
                {
                    GlobalLogManager.Instance.ConsoleLog($"Dummy Loop Error :: {ex}");
                }

                await Task.Delay(500);
            }
        }
    }
}
