using NPUDemoIntegrated.GlobalManagers;
using NPUDemoIntegrated.Models;
using NPUDemoIntegrated.Models.HBLVModule;
using NPUDemoIntegrated.Models.IRModule;
using NPUDemoIntegrated.Models.OBJModule;
using NPUDemoIntegrated.Utils;
using OpenCvSharp;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace NPUDemoIntegrated.ViewModels
{
    class HBLVViewModel : BaseViewModel
    {
        private readonly HBLVSerialService serialService;
        private WebCamControl webCamControl;
        public HBLVConfig hblvConfig { get; }
        public SerialConfig serialConfig { get; }

        private readonly object frameLock = new object();
        private readonly object bboxLock = new object();
        private readonly object sendLock = new object();

        private bool isSending = false;
        private bool isRefBoxValid = false;
        private bool isImageValid = false;
        private bool isDetectionComplete = false;
        private bool _measureFlag = false;

        private int tryCount;

        private float _pred = 0.0f;
        private float _volt = 0.0f;
        private float _amp = 0.0f;

        private WriteableBitmap bitmapTmp;
        private WriteableBitmap bitmapSentTmp;
        private BitmapSource _bitmap;
        private BitmapSource _bitmapSent;
        private Mat frameToSend;
        private Mat frameToDraw;

        private List<OpenCvSharp.Rect> bbox = new List<OpenCvSharp.Rect>();

        public override ICommand ConnectCommand { get; }
        public override ICommand DisconnectCommand { get; }
        public ICommand MeasureCommand { get; }

        private string _topMessageLeft = "기준영역에 검지, 중지, 약지를 보여주세요";
        private string _topMessageRight = "진단 결과가 없습니다.";
        public override string title => "Doksan NPU Real-Time Vision AI Demonstration";
        public override string subTitle => "Real-time camera input and on-device hemoglobin level inference";

        public float pred
        {
            get => _pred;
            set { _pred = value; OnPropertyChanged(); }
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

        public bool measureFlag
        {
            get => _measureFlag;
            set { _measureFlag = value; OnPropertyChanged(); }
        }

        public string topMessageLeft
        {
            get => _topMessageLeft;
            set { _topMessageLeft = value; OnPropertyChanged(); }
        }

        public string topMessageRight
        {
            get => _topMessageRight;
            set { _topMessageRight = value; OnPropertyChanged(); }
        }

        public BitmapSource bitmapShow
        {
            get => _bitmap;
            set { _bitmap = value; OnPropertyChanged(); }
        }

        public BitmapSource bitmapSent
        {
            get => _bitmapSent;
            set { _bitmapSent = value; OnPropertyChanged(); }
        }

        public string cn_dn
        {
            get
            {
                if (serialService.connectionState == EConnectionState.Disconnected)
                {
                    return "White";    //Disonnected
                }
                else
                {
                    return "Red";      //Connected
                }
            }
        }

        public HBLVViewModel(SerialConfig _serialConfig, HBLVConfig _hblvConfig, HBLVSerialService service)
        {
            hblvConfig = _hblvConfig;
            serialConfig = _serialConfig;
            serialService = service;

            serialService._sharedStatus.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SharedStatus.connectionState))
                {
                    OnPropertyChanged(nameof(cn_dn));
                }
            };

            ConnectCommand = new RelayCommand(param => {
                if (serialService.Connect() == 1) ButtonCommand = DisconnectCommand;
                if (is_menu_open) is_menu_open = !is_menu_open;
            });

            DisconnectCommand = new RelayCommand(param => {
                Task.Run(() => serialService.Disconnect());
                ButtonCommand = ConnectCommand;
                if (is_menu_open) is_menu_open = !is_menu_open;
            });

            //MeasureCommand = new RelayCommand(param => {

            ButtonCommand = ConnectCommand;
            serialService.PacketReceived += OnPacketReceived;
        }

        private async void SendImage()
        {
            //if (!isSending && tryCount >= 10 && serialService.connectionState == EConnectionState.WaitingForInference && isRefBoxValid && !isImageValid)
            //{
            //    serialService.connectionState = EConnectionState.Connected;
            //    GlobalLogManager.Instance.ConsoleLog($"WARN.. SendFrame Re-Called: connection_status set to: {serialService.connectionState}");
            //    GlobalLogManager.Instance.AddLogToFile("DEBUG", $"SendFrame Re-Called: connection_status set to: {serialService.connectionState}");
            //}
            if (serialService.connectionState == EConnectionState.Connected && !isSending && isRefBoxValid && !isImageValid && measureFlag)
            {
                isSending = true;

                try
                {
                    lock (sendLock)
                    {
                        GlobalLogManager.Instance.ConsoleLog("\nSendLock Called");
                        if (frameToDraw != null && !frameToDraw.IsDisposed)
                        {
                            frameToSend.CopyTo(frameToDraw);
                        }
                        else
                        {
                            frameToDraw = frameToSend.Clone();
                        }
                    }

                    GlobalLogManager.Instance.ConsoleLog("\nSerialCommunication Called");
                    Mat converted = new Mat();
                    Cv2.CvtColor(frameToSend, converted, ColorConversionCodes.BGR2RGB);
                    // stopwatch.Restart();
                    await serialService.SerialCommunication(converted);
                    converted.Dispose();
                }
                finally
                {
                    isSending = false;
                    isRefBoxValid = false;
                }
            }
            else if (serialService.connectionState == EConnectionState.WaitingForInference)
            {
                GlobalLogManager.Instance.ConsoleLog($"SendFrame Failed ... is_sending: {isSending}  connection_status: {serialService.connectionState} trc :: {tryCount}");
                //tryCount++;
                //GlobalLogManager.Instance.AddLogToFile("ERROR", $"SendFrame Failed ... is_sending: {_is_sending}  connection_status: {_connection_status}  try_count: {_try_count}");
            }
        }

        private void OnFrameUpdate(Mat frame)
        {
            try
            {
                if (Application.Current == null) return;
                Application.Current.Dispatcher.Invoke(new Action(() => {
                    OpenCvSharp.Rect roi = new OpenCvSharp.Rect(110, 80, 420, 320);
                    if (bitmapTmp == null || bitmapTmp.PixelWidth != frame.Width || bitmapTmp.PixelHeight != frame.Height)
                    {
                        //OpenCvSharp.Rect white_ref = new OpenCvSharp.Rect(70, 205, 34, 34);
                        //Cv2.Rectangle(frame, white_ref, Scalar.Red, 1);

                        bitmapTmp = new WriteableBitmap(roi.Width, roi.Height, 96, 96, System.Windows.Media.PixelFormats.Bgr24, null);
                        bitmapShow = bitmapTmp;
                    }
                    using (Mat center_cropped = frame[roi])
                    {
                        UtilsForMatImage.WriteBufferDirectly(center_cropped, bitmapTmp);
                    }
                }));

                if (isSending || serialService.connectionState != EConnectionState.Connected || isDetectionComplete || !measureFlag) return;

                lock (frameLock)
                {
                    OpenCvSharp.Rect roi = new OpenCvSharp.Rect(160, 80, 320, 320);
                    using (Mat center_cropped = frame[roi])
                    {
                        if (frameToSend != null && !frameToSend.IsDisposed)
                        {

                            center_cropped.CopyTo(frameToSend);
                        }
                        else
                        {
                            frameToSend = center_cropped.Clone();
                        }
                    }
                }

                if (UtilsForMatImage.CheckIfRegionWhite(frame, new OpenCvSharp.Rect(180, 285, 34, 34)))
                {
                    isRefBoxValid = true;
                    topMessageLeft = "이미지 전송 후 결과를 기다리는 중입니다";
                    SendImage();
                }
                else
                {
                    isRefBoxValid = false;
                    topMessageLeft = "배경이 흰색이 아니거나, 너무 어둡습니다.";
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

        private void OnPacketReceived(predictionResultPacket received)
        {
            GlobalLogManager.Instance.ConsoleLog($"PacketReceived Called");
            if (received.errorCode == 0x01)
            {
                topMessageRight = "손가락이 충분히 탐지되지 않았습니다.";
                isImageValid = false;
                return;
            }

            if (received.errorCode == 0x02)
            {
                topMessageRight = "손이 너무 가깝거나 위치가 잘못됐습니다.";
                isImageValid = false;
                return;
            }

            isImageValid = true;
            isDetectionComplete = true;

            // string save_path = Path.Combine(GlobalConfigManager.Instance.GetImageFolderPath(), GlobalConfigManager.Instance.GetNowImageFileName());
            // GlobalLogManager.Instance.ConsoleLog($"OK.. Points Received ... Drawing Bbox");
            GlobalLogManager.Instance.AddLogToFile("DEBUG", "Points Received ... Drawing Bbox");

            int cnt = 0;

            lock (bboxLock)
            {
                bbox = received.bboxs;
            }

            Mat frame_to_draw;
            lock (sendLock)
            {
                if (frameToDraw == null || frameToDraw.Empty()) return;
                frame_to_draw = frameToDraw.Clone();
            }

            List<OpenCvSharp.Rect> textBoxs = new List<OpenCvSharp.Rect>();
            lock (bboxLock)
            {
                foreach (var box in bbox)
                {
                    Cv2.Rectangle(frame_to_draw, box, Scalar.Red, 1);
                    cnt++;
                }
                // Cv2.ImWrite(save_path, frame_to_draw);
            }

            // GlobalLogManager.Instance.ConsoleLog("OK.. Bbox drawing Completed ... Check Image\n");
            GlobalLogManager.Instance.AddLogToFile("DEBUG", "Bbox drawing Completed ... Check Image\n");

            // GlobalLogManager.Instance.ConsoleLog($"volt & amp: {amp}, {volt}");

            Application.Current.Dispatcher.Invoke(() => {
                if (bitmapSentTmp == null || bitmapSentTmp.PixelWidth != frame_to_draw.Width || bitmapSentTmp.PixelHeight != frame_to_draw.Height)
                {
                    bitmapSentTmp = new WriteableBitmap(frame_to_draw.Width, frame_to_draw.Height, 96, 96, System.Windows.Media.PixelFormats.Bgr24, null);
                    bitmapSent = bitmapSentTmp;
                }
                UtilsForMatImage.WriteBufferDirectly(frame_to_draw, bitmapSentTmp);

                this.volt = received.voltage;
                this.amp = received.ampere;

                frame_to_draw.Dispose();
            });

            this.pred = received.prediction * 100.0f;

            if (pred <= 30) {
                topMessageRight = $"높은 확률로 정상 입니다.";
            }
            else if (pred >= 70) {
                topMessageRight = $"높은 확률로 빈혈 입니다.";
            }
            else {
                topMessageRight = $"정확한 진료가 어려운 표본입니다. 병원에 방문해주세요.";
            }
            topMessageLeft = $"진단이 완료되었습니다.";

            measureFlag = false;
            isImageValid = false;
            isDetectionComplete = false;
        }

        public override void DeactivateModule(EModuleType targetModule)
        {
            while (serialService.connectionState == EConnectionState.SendingImage)
            {
                GlobalLogManager.Instance.ConsoleLog($"now connection state ::{serialService.connectionState} wait until sending is finished");
                Thread.Sleep(5);
            }
            GlobalLogManager.Instance.ConsoleLog($"now connection state ::{serialService.connectionState}");

            Thread.Sleep(10);

            serialService.SendModuleChangeNotice(targetModule);
            serialService.SerialReceiveEventDispose();

            Thread.Sleep(10);

            this.Dispose();
        }


        public override void ActivateModule()
        {
            serialService.SerialReceiveEventSubscribe();

            webCamControl = new WebCamControl();

            webCamControl.FrameUpdate += OnFrameUpdate;
            webCamControl.WebCamInitialize();
        }

        public override void Dispose()
        {
            if (webCamControl != null)
            {
                webCamControl.FrameUpdate -= OnFrameUpdate;
                webCamControl.Dispose();
                webCamControl = null;
            }

            serialService.PacketReceived -= OnPacketReceived;

            if (frameToSend != null) frameToSend.Dispose();
            if (frameToDraw != null) frameToDraw.Dispose();

        }
    }
}
