using NPUDemoIntegrated.GlobalManagers;
using NPUDemoIntegrated.Models;
using NPUDemoIntegrated.Models.IRModule;
using NPUDemoIntegrated.Utils;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace NPUDemoIntegrated.ViewModels
{
    class IRViewModel : BaseViewModel
    {
        public override string title => "Doksan NPU Real-Time Vision AI Demonstration";
        public override string subTitle => "Real-time Infrared Image input and on-device object detection inference";

        private readonly IRSerialService _serialService;
        public IRConfig irConfig { get; }
        public SerialConfig serialConfig { get; }

        private readonly Timer _timer;
        private DispatcherTimer _measureTimer;
        private readonly Stopwatch _stopwatch = new Stopwatch();

        public override ICommand ConnectCommand { get; }
        public override ICommand DisconnectCommand { get; }

        public ICommand ModuleConnectCommand { get; }
        public ICommand ModuleDisconnectCommand { get; }
        public ICommand AutoMeasureToggleCommand { get; }
        public ICommand SendCommand { get; }
        private ICommand _moduleCommand;

        private List<OpenCvSharp.Rect> _bbox = new List<OpenCvSharp.Rect>();
        private List<OpenCvSharp.Rect> text_boxs = new List<OpenCvSharp.Rect>();
        public float[] pixelArray => _serialService.Data.pixelTempArray;
        private float[] _processedBuffer;
        public float sensorTemp => _serialService.Data.sensorTemp;
        public WriteableBitmap colorBitmap { get; private set; }
        public Mat colorMat;
        public Mat colorMatShow;
        public BitmapSource _bitmapShow;

        private double _fps = 0.0;
        private float _volt = 0.0f;
        private float _amp = 0.0f;

        private bool _isSendAuto = false;
        private bool _isInterpolate = false;
        private bool _isSending = false;
        private int _tryCount = 0;

        private readonly object _frame_lock = new object();
        private readonly object _bbox_lock = new object();
        private readonly object _send_lock = new object();

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

            colorBitmap = new WriteableBitmap(irConfig.resolution, irConfig.resolution,
                96, 96, System.Windows.Media.PixelFormats.Bgr32, null);
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
            };

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
                if (_serialService.ModuleConnect() == 1) ModuleCommand = ModuleDisconnectCommand;
                if (is_menu_open) is_menu_open = !is_menu_open;
            });

            ModuleDisconnectCommand = new RelayCommand(param =>
            {
                Task.Run(() => _serialService.ModuleDisconnect());
                ModuleCommand = ModuleConnectCommand;
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
            if (!_isSending && _tryCount >= 20 && _serialService.connectionState == EConnectionState.WaitingForInference)
            {
                _serialService.connectionState = EConnectionState.Connected;
                GlobalLogManager.Instance.ConsoleLog($"WARN.. SendFrame Re-Called: connection_status set to: {_serialService.connectionState}");
                GlobalLogManager.Instance.AddLogToFile("DEBUG", $"SendFrame Re-Called: connection_status set to: {_serialService.connectionState}");
            }

            if (!_isSending && _serialService.connectionState == EConnectionState.Connected)
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
                Cv2.CvtColor(mat_tmp, converted, ColorConversionCodes.BGRA2RGB);
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
                    await _serialService.SerialCommunication(converted);
                }
                finally
                {
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

            text_boxs.Clear();

            Mat resized = new Mat();
            lock (_bbox_lock)
            {
                resized = Resize(frame_to_draw, 512);

                foreach (var box in _bbox)
                {
                    Scalar rectColor = new Scalar(0, 0, 255, 255);  //red
                    Cv2.Rectangle(resized, box, rectColor, 2);

                    text_boxs.Add(DrawTextWithBox(resized, cls[cnt], prob[cnt], box));
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

        private OpenCvSharp.Rect DrawTextWithBox(Mat frame, IRConfig.EClassArray cls, int prob, OpenCvSharp.Rect box)
        {
            string text = $"class: {cls.ToString()}  prob: {prob}";
            var font = HersheyFonts.HersheyTriplex;
            double font_scale = 0.6;
            int thickness = 1;

            OpenCvSharp.Size text_size = Cv2.GetTextSize(text, font, font_scale, thickness, out int baseline);
            var coord = new OpenCvSharp.Point(box.X - 1, box.Y - 1);

            if (box.Y - text_size.Height < 0)
            {
                GlobalLogManager.Instance.ConsoleLog("Text Box Out of Bound Found! Adjusting ...");
                GlobalLogManager.Instance.AddLogToFile("DEBUG", "Text Box Out of Bound Found! Adjusting ...");
                coord.Y = box.Y + text_size.Height + 1;
            }
            if (box.X + text_size.Width > 512)
            {
                GlobalLogManager.Instance.ConsoleLog("Text Box Out of Bound Found! Adjusting ...");
                GlobalLogManager.Instance.AddLogToFile("DEBUG", "Text Box Out of Bound Found! Adjusting ...");
                coord.X = box.X - ((box.X + text_size.Width) - 512);
            }

            Scalar rectColor = new Scalar(0, 0, 255, 255);  //red
            Scalar textColor = new Scalar(0, 255, 255, 255);//yellow

            OpenCvSharp.Rect background_rect = new OpenCvSharp.Rect(
                coord.X,
                coord.Y - text_size.Height - baseline,
                text_size.Width,
                text_size.Height + 1 * baseline
                );

            background_rect = AvoidTextBoxIntersection(background_rect);
            coord.X = background_rect.X;
            coord.Y = background_rect.Y + text_size.Height;

            Cv2.Rectangle(frame, background_rect, rectColor, -1);
            Cv2.PutText(frame, text, coord, font, font_scale, textColor, thickness);

            GlobalLogManager.Instance.ConsoleLog("Text Box Drawing Completed");
            GlobalLogManager.Instance.AddLogToFile("DEBUG", "Text Box Drawing Completed");

            return background_rect;
        }

        private OpenCvSharp.Rect AvoidTextBoxIntersection(OpenCvSharp.Rect text_box)
        {
            if (text_boxs.Count == 0) return text_box;

            bool is_intersect = false;

            do
            {
                is_intersect = false;
                foreach (var box in text_boxs)
                {
                    if (text_box.IntersectsWith(box))
                    {
                        GlobalLogManager.Instance.ConsoleLog("Text Box Intersection Found! Avoiding ...");
                        GlobalLogManager.Instance.AddLogToFile("DEBUG", "Text Box Intersection Found! Avoiding ...");
                        text_box.Y = box.Bottom + 3;
                        is_intersect = true;
                        break;
                    }
                }
            } while (is_intersect);
            return text_box;
        }

        unsafe private void UpdateColorBitmap()
        {
            // Console.WriteLine($"[Update] Hash: {this.GetHashCode()}, IsInterp: {isInterpolate}");

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

        public Mat Resize(Mat src, int size)
        {
            GlobalLogManager.Instance.ConsoleLog("Resizing bbox Image ...");
            GlobalLogManager.Instance.AddLogToFile("DEBUG", "Resizing Image ...");

            OpenCvSharp.Size newSize = new OpenCvSharp.Size(size, size);

            Mat resizedImage = new Mat();
            Cv2.Resize(src, resizedImage, newSize, 0, 0, InterpolationFlags.Linear);

            return resizedImage;
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
                    _timer.Change(0, 500);
                    GlobalLogManager.Instance.ConsoleLog("Auto Send Enabled");
                    GlobalLogManager.Instance.AddLogToFile("DEBUG", "Auto Send Enabled");
                }
                else
                {
                    _timer.Change(Timeout.Infinite, Timeout.Infinite);
                }
            }
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

        public bool isInterpolate
        {
            get { return _isInterpolate; }
            set
            {
                _isInterpolate = value;
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

        public override void DeactivateModule(EModuleType targetModule)
        {
            while (_serialService.connectionState == EConnectionState.SendingImage)
            {
                GlobalLogManager.Instance.ConsoleLog($"now connection state ::{_serialService.connectionState} wait until sending is finished");
                Thread.Sleep(20);
            }
            GlobalLogManager.Instance.ConsoleLog($"now connection state ::{_serialService.connectionState}");

            isSendAuto = false;
            _measureTimer.Stop();

            Thread.Sleep(10);

            _serialService.SendModuleChangeNotice(targetModule);
            _serialService.ModuleDisconnect();
            _serialService.SerialReceiveEventDispose();

            Thread.Sleep(20);
        }
        public override void ActivateModule()
        {
            _serialService.SerialReceiveEventSubscribe();
        }

        public override void Dispose()
        {
            _timer.Dispose();
            _serialService.PointsReceived -= OnPointsReceived;

            if (colorMat != null) colorMat.Dispose();
            if (colorMatShow != null) colorMatShow.Dispose();
        }
    }
}
