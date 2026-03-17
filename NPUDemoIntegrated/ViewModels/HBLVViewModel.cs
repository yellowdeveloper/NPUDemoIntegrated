using NPUDemoIntegrated.GlobalManagers;
using NPUDemoIntegrated.Models;
using NPUDemoIntegrated.Models.HBLVModule;
using NPUDemoIntegrated.Models.IRModule;
using NPUDemoIntegrated.Models.OBJModule;
using NPUDemoIntegrated.Utils;
using OpenCvSharp;
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
        private bool isSending = false;
        private bool isRefBoxValid = false;
        private bool isImageValid = false;
        private bool isDetectionComplete = false;
        private int tryCount;

        private float _pred = 0.0f;
        private float _volt = 0.0f;
        private float _amp = 0.0f;

        private WriteableBitmap bitmapTmp;
        private BitmapSource _bitmap;
        private Mat frameToSend;

        public override ICommand ConnectCommand { get; }
        public override ICommand DisconnectCommand { get; }

        private string _topMessage = "기준영역에 검지, 중지, 약지를 보여주세요";
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

        public string topMessage
        {
            get => _topMessage;
            set { _topMessage = value; OnPropertyChanged(); }
        }

        public BitmapSource bitmapShow
        {
            get => _bitmap;
            set { _bitmap = value; OnPropertyChanged(); }
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
            if (serialService.connectionState == EConnectionState.Connected && !isSending && isRefBoxValid && !isImageValid)
            {
                isSending = true;
                try
                {
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
                Application.Current.Dispatcher.Invoke(new Action(() => {
                    if (bitmapTmp == null || bitmapTmp.PixelWidth != frame.Width || bitmapTmp.PixelHeight != frame.Height)
                    {
                        OpenCvSharp.Rect roi = new OpenCvSharp.Rect(60, 80, 420, 320);
                        frame = frame[roi];
                        OpenCvSharp.Rect white_ref = new OpenCvSharp.Rect(80, 205, 34, 34);
                        Cv2.Rectangle(frame, white_ref, Scalar.Red, 1);

                        bitmapTmp = new WriteableBitmap(frame.Width, frame.Height, 96, 96, System.Windows.Media.PixelFormats.Bgr24, null);
                        bitmapShow = bitmapTmp;
                    }
                    UtilsForMatImage.WriteBufferDirectly(frame, bitmapTmp);
                }));

                if (isSending || serialService.connectionState != EConnectionState.Connected || isDetectionComplete) return;

                lock (frameLock)
                {
                    if (frameToSend != null && !frameToSend.IsDisposed)
                    {
                        OpenCvSharp.Rect roi = new OpenCvSharp.Rect(60, 0, 320, 320);
                        frame[roi].CopyTo(frameToSend);
                    }
                    else
                    {
                        frameToSend = frame.Clone();
                    }
                }

                if (UtilsForMatImage.CheckIfRegionWhite(frame, new OpenCvSharp.Rect(80, 205, 34, 34)))
                {
                    isRefBoxValid = true;
                    topMessage = "이미지 전송 후 결과를 기다리는 중입니다";
                    SendImage();
                }
                else
                {
                    isRefBoxValid = false;
                    topMessage = "손의 위치를 기준선 안쪽으로 조정해주세요.";
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
                topMessage = "손가락이 충분히 탐지되지 않았습니다.";
                isImageValid = false;
                return;
            }

            if (received.errorCode == 0x02)
            {
                topMessage = "손이 너무 가깝거나 위치가 잘못됐습니다.";
                isImageValid = false;
                return;
            }

            isImageValid = true;
            isDetectionComplete = true;

            this.amp = received.ampere;
            this.volt = received.voltage;
            this.pred = received.prediction;

            topMessage = $"{pred*100.0f}% 확률로 빈혈 입니다.";
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

        }
    }
}
