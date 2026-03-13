using NPUDemoIntegrated.GlobalManagers;
using NPUDemoIntegrated.Models;
using NPUDemoIntegrated.Models.HBLVModule;
using NPUDemoIntegrated.Models.IRModule;
using NPUDemoIntegrated.Models.OBJModule;
using NPUDemoIntegrated.Utils;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Controls;
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

        private WriteableBitmap bitmapTmp;
        private BitmapSource _bitmap;
        private Mat frameToSend;

        public override ICommand ConnectCommand { get; }
        public override ICommand DisconnectCommand { get; }

        private string _topMessage = "기준영역에 검지, 중지, 약지를 보여주세요";
        public override string title => "Doksan NPU Real-Time Vision AI Demonstration";
        public override string subTitle => "Real-time camera input and on-device hemoglobin level inference";
        public override double windowHeight => 900;
        public override double windowWidth => 1180;
        public override ResizeMode resizeMode => ResizeMode.NoResize;

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

        public HBLVViewModel(SerialConfig _serialConfig, HBLVConfig _hblvConfig, HBLVSerialService service)
        {
            hblvConfig = _hblvConfig;
            serialConfig = _serialConfig;
            serialService = service;

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

                if (UtilsForMatImage.CheckReferenceBoxIntersection(frame, new OpenCvSharp.Rect(80, 205, 34, 34)))
                {
                    topMessage = "기준영역에 검지, 중지, 약지를 보여주세요"; // TEMPORARY MESSAGE FOR DEBUGGING
                    // SendImage();
                    // TODO:: MAKE A FLAG(IF DETECTION IS COMPLETED, MESSAGE WILL BE "이미지 전송 후 결과를 기다리는 중입니다.")
                    // TODO:: MAKE A FLAG(IF PREDICTION IS COMPLETED, MESSAGE WILL BE "00% 확률로 빈혈/정상 입니다.")
                }
                else
                {
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

        public override void DeactivateModule(EModuleType targetModule)
        {
            this.Dispose();
        }


        public override void ActivateModule()
        {
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

            if (frameToSend != null) frameToSend.Dispose();
        }
    }
}
