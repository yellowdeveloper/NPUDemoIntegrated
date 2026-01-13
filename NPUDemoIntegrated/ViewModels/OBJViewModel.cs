using NPUDemoIntegrated.GlobalManagers;
using NPUDemoIntegrated.Models;
using NPUDemoIntegrated.Models.OBJModule;
using NPUDemoIntegrated.Utils;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace NPUDemoIntegrated.ViewModels
{
    class OBJViewModel: BaseViewModel
    {
        private readonly WebCamControl _web_cam_control;
        private readonly OBJSerialService _serialService;
        private readonly Timer _timer;
        private readonly Stopwatch _stopwatch = new Stopwatch();

        public OBJConfig objConfig { get; }
        public SerialConfig serialConfig { get; }

        private BitmapSource _bitmap;
        private BitmapSource _bitmap_sent;
        private Mat _frame_to_send;
        private Mat _frame_to_draw;
        private List<OpenCvSharp.Rect> _bbox = new List<OpenCvSharp.Rect>();
        private List<OpenCvSharp.Rect> text_boxs = new List<OpenCvSharp.Rect>();

        private bool _is_sent_auto = false;
        private bool _is_sending = false;
        private int _try_count = 0;
        private double _fps = 0.0;

        //for test
        private bool _test_flag = false;

        private readonly object _frame_lock = new object();
        private readonly object _bbox_lock = new object();
        private readonly object _send_lock = new object();

        public override ICommand ConnectCommand { get; }
        public override ICommand DisconnectCommand { get; }
        public ICommand SendCommand { get; }


        public event PropertyChangedEventHandler PropertyChanged;
        public override string title => "Doksan NPU Real-Time Vision AI Demonstration";
        public override string subTitle => "Real-time camera input and on-device object detection inference";

        public OBJViewModel(SerialConfig _serialConfig, OBJConfig _objConfig, OBJSerialService service)
        {
            _serialService = service;
            objConfig = _objConfig;
            serialConfig = _serialConfig;
            //_viewModelId = DateTime.Now.ToString("\nInstance Creadted Time == HH:mm:ss.fff\n");
            //Debug.Write($"{_viewModelId}");

            _web_cam_control = new WebCamControl();

            _web_cam_control.FrameUpdate += OnFrameUpdate;
            _web_cam_control.WebCamInitialize();

            _serialService.PointsReceived += OnPointsReceived;

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

            SendCommand = new RelayCommand(async param => {
                is_send_auto = false;
                await SendFramePeriodically();
                if (is_menu_open) is_menu_open = !is_menu_open;
                // GlobalLogManager.Instance.ConsoleLog("Manual Send Completed");
            });

            _timer = new Timer(async (_) => await SendFramePeriodically(), null, Timeout.Infinite, Timeout.Infinite);
        }

        private async Task SendFramePeriodically()
        {
            if (!_is_sending && _try_count >= 20 && _serialService.connectionState == EConnectionState.WaitingForInference)
            {
                _serialService.connectionState = EConnectionState.Connected;
                GlobalLogManager.Instance.ConsoleLog($"WARN.. SendFrame Re-Called: connection_status set to: {_serialService.connectionState}");
                GlobalLogManager.Instance.AddLogToFile("DEBUG", $"SendFrame Re-Called: connection_status set to: {_serialService.connectionState}");
            }
            if (!_is_sending && _serialService.connectionState == EConnectionState.Connected)
            {
                _is_sending = true;

                GlobalLogManager.Instance.ConsoleLog($"OK.. Image Sending Method Called ... ");
                GlobalLogManager.Instance.AddLogToFile("DEBUG", $"Image Sending Method Called ... ");

                _try_count = 0;
                Mat mat_tmp = new Mat();
                //Debug.Write("\nMat Variable set");
                lock (_frame_lock)
                {
                    //Debug.Write("\nFrameLock Called");
                    if (_frame_to_send == null) _test_flag = true;
                    else { mat_tmp = _frame_to_send.Clone(); }
                }

                if (_test_flag)
                {
                    try
                    {
                        GlobalLogManager.Instance.ConsoleLog($"WARN.. frame empty, sending 0 ~ 255");
                        await _serialService.SerialCommunication(new Mat());
                        return;
                    }
                    finally
                    {
                        _is_sending = false;
                    }
                }

                Mat resized;
                if (serialConfig.imgMode == EImageMode.RESIZE)
                {
                    resized = Resize(mat_tmp);
                }
                else
                {
                    resized = Pad(mat_tmp);
                }

                lock (_send_lock)
                {
                    //Debug.Write("\nSendLock Called");
                    _frame_to_draw?.Dispose();
                    _frame_to_draw = mat_tmp.Clone();
                }
                mat_tmp.Dispose();

                try
                {
                    Debug.Write("\nSerialCommunication Called");
                    _stopwatch.Restart();
                    await _serialService.SerialCommunication(resized);
                }
                finally
                {
                    _is_sending = false;
                    resized.Dispose();
                }
            }
            else if (_serialService.connectionState == EConnectionState.WaitingForInference && _is_sent_auto)
            {
                //GlobalLogManager.Instance.ConsoleLog($"SendFrame Failed ... is_sending: {_is_sending}  connection_status: {_connection_status}  try_count: {_try_count}"  );
                //GlobalLogManager.Instance.AddLogToFile("ERROR", $"SendFrame Failed ... is_sending: {_is_sending}  connection_status: {_connection_status}  try_count: {_try_count}");
                _try_count++;
            }
        }

        private void OnFrameUpdate(Mat frame)
        {
            try
            {
                //BitmapSource bitmap_tmp = frame.ToBitmapSource();
                //bitmap_tmp.Freeze();
                Application.Current.Dispatcher.Invoke(new Action(() => {
                    BitmapSource bitmap_tmp = frame.ToBitmapSource();
                    bitmap_tmp.Freeze();
                    bitmap_show = bitmap_tmp;
                }));

                lock (_frame_lock)
                {
                    _frame_to_send?.Dispose();
                    _frame_to_send = frame.Clone();
                }
            }
            catch (Exception ex)
            {
                GlobalLogManager.Instance.ConsoleLog($"ERROR!! Error While Updating Frame :: {ex}");
                GlobalLogManager.Instance.AddLogToFile("ERROR", $"Error While Updating Frame :: {ex}");
            }
            finally
            {
                frame.Dispose();
            }
        }

        private void OnPointsReceived(List<OpenCvSharp.Rect> b_box, List<OBJConfig.EClassArray> cls, List<int> prob)
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
                if (_frame_to_draw == null || _frame_to_draw.Empty()) return;
                frame_to_draw = _frame_to_draw.Clone();
            }

            text_boxs.Clear();
            lock (_bbox_lock)
            {
                foreach (var box in _bbox)
                {
                    Cv2.Rectangle(frame_to_draw, box, Scalar.Red, 2);

                    text_boxs.Add(DrawTextWithBox(frame_to_draw, cls[cnt], prob[cnt], box));
                    cnt++;
                }
                // Cv2.ImWrite(save_path, frame_to_draw);
            }

            GlobalLogManager.Instance.ConsoleLog("OK.. Bbox drawing Completed ... Check Image\n");
            GlobalLogManager.Instance.AddLogToFile("DEBUG", "Bbox drawing Completed ... Check Image\n");

            // BitmapSource bitmap_tmp = frame_to_draw.ToBitmapSource();
            // bitmap_tmp.Freeze();

            _stopwatch.Stop();
            var elapsed = _stopwatch.Elapsed.TotalSeconds;

            Application.Current.Dispatcher.Invoke(() => {
                BitmapSource bitmap_tmp = frame_to_draw.ToBitmapSource();
                bitmap_tmp.Freeze();

                bitmap_sent = bitmap_tmp;
                fps = 1 / elapsed;

                frame_to_draw.Dispose();
            });

            GlobalLogManager.Instance.ConsoleLog($"Frame Rate:: {fps}");

            // frame_to_draw.Dispose();
        }

        private OpenCvSharp.Rect DrawTextWithBox(Mat frame, OBJConfig.EClassArray cls, int prob, OpenCvSharp.Rect box)
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
            if (box.X + text_size.Width > 640)
            {
                GlobalLogManager.Instance.ConsoleLog("Text Box Out of Bound Found! Adjusting ...");
                GlobalLogManager.Instance.AddLogToFile("DEBUG", "Text Box Out of Bound Found! Adjusting ...");
                coord.X = box.X - ((box.X + text_size.Width) - 640);
            }

            OpenCvSharp.Rect background_rect = new OpenCvSharp.Rect(
                coord.X,
                coord.Y - text_size.Height - baseline,
                text_size.Width,
                text_size.Height + 1 * baseline
                );

            background_rect = AvoidTextBoxIntersection(background_rect);
            coord.X = background_rect.X;
            coord.Y = background_rect.Y + text_size.Height;

            Cv2.Rectangle(frame, background_rect, Scalar.Red, -1);
            Cv2.PutText(frame, text, coord, font, font_scale, Scalar.Yellow, thickness);

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

        // resize function added
        public Mat Resize(Mat src)
        {
            GlobalLogManager.Instance.ConsoleLog("Resizing bbox Image ...");
            GlobalLogManager.Instance.AddLogToFile("DEBUG", "Resizing Image ...");
            int size = objConfig.imgSize == EImageSize.S320 ? 320 : 384;
            OpenCvSharp.Size newSize = new OpenCvSharp.Size(size, size);

            Mat resizedImage = new Mat();
            Cv2.Resize(src, resizedImage, newSize, 0, 0, InterpolationFlags.Linear);

            return resizedImage;
        }
        // padding function added
        public Mat Pad(Mat src)
        {
            GlobalLogManager.Instance.ConsoleLog("Padding bbox Image ...");
            GlobalLogManager.Instance.AddLogToFile("DEBUG", "Padding bbox Image ...");
            int size = objConfig.imgSize == EImageSize.S320 ? 320 : 384;
            Scalar color = new Scalar(0, 0, 0); // Black padding

            double w = src.Width;
            double h = src.Height;

            double ratio = w > h ? size / w : size / h;

            int w_resized = (int)(w * ratio);
            int h_resized = (int)(h * ratio);

            using (Mat resizedImage = new Mat())
            {
                Cv2.Resize(src, resizedImage, new OpenCvSharp.Size(w_resized, h_resized), 0, 0, InterpolationFlags.Area);

                Mat canvas = new Mat(size, size, src.Type(), color);

                int top = (size - h_resized) / 2;
                int left = (size - w_resized) / 2;

                OpenCvSharp.Rect roi = new OpenCvSharp.Rect(left, top, w_resized, h_resized);
                resizedImage.CopyTo(canvas[roi]);

                return canvas;
            }
        }

        public BitmapSource bitmap_show
        {
            get => _bitmap;
            set { _bitmap = value; OnPropertyChanged(); }
        }

        public BitmapSource bitmap_sent
        {
            get => _bitmap_sent;
            set { _bitmap_sent = value; OnPropertyChanged(); }
        }

        public string cn_dn
        {
            get
            {
                if (_serialService.connectionState == EConnectionState.Disconnected)
                {
                    return "White";    //Disonnected
                }
                else
                {
                    return "Red"; //Connected
                }
            }
        }
        public double fps
        {
            get => _fps;
            set { _fps = value; OnPropertyChanged(); }
        }

        public bool is_send_auto
        {
            get => _is_sent_auto;
            set
            {
                _is_sent_auto = value;
                OnPropertyChanged();

                if (_is_sent_auto)
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

        public override void DeactivateModule(EModuleType targetModule)
        {
            _serialService.SendModuleChangeNotice(targetModule);
            _serialService?.Disconnect();
        }

        public override void Dispose()
        {
            _timer.Dispose();
            _web_cam_control.FrameUpdate -= OnFrameUpdate;
            _serialService.PointsReceived -= OnPointsReceived;
            
            _web_cam_control.Dispose();
            if (_frame_to_draw != null) _frame_to_draw.Dispose();
            if (_frame_to_send != null) _frame_to_send.Dispose();
        }
    }
}
