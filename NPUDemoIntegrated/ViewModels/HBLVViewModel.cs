using NPUDemoIntegrated.GlobalManagers;
using NPUDemoIntegrated.Models;
using NPUDemoIntegrated.Models.IRModule;
using NPUDemoIntegrated.Models.OBJModule;
using NPUDemoIntegrated.Utils;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace NPUDemoIntegrated.ViewModels
{
    class HBLVViewModel : BaseViewModel
    {
        private readonly IRSerialService _serialService;
        private WebCamControl webCamControl;
        public IRConfig irConfig { get; }
        public SerialConfig serialConfig { get; }

        private readonly object frameLock = new object();

        private WriteableBitmap bitmapTmp;
        private BitmapSource _bitmap;
        private Mat frameToSend;

        public override ICommand ConnectCommand { get; }
        public override ICommand DisconnectCommand { get; }

        public override string title => "Doksan NPU Real-Time Vision AI Demonstration";
        public override string subTitle => "Real-time camera input and on-device hemoglobin level inference";
        public override double windowHeight => 900;
        public override double windowWidth => 1180;
        public override ResizeMode resizeMode => ResizeMode.NoResize;

        public BitmapSource bitmapShow
        {
            get => _bitmap;
            set { _bitmap = value; OnPropertyChanged(); }
        }

        public HBLVViewModel(SerialConfig _serialConfig, IRConfig _irConfig, IRSerialService service)
        {
            irConfig = _irConfig;
            serialConfig = _serialConfig;
            _serialService = service;
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

                        bitmapTmp = new WriteableBitmap(frame.Width, frame.Height, 96, 96, System.Windows.Media.PixelFormats.Bgr24, null);
                        bitmapShow = bitmapTmp;
                    }
                    UtilsForMatImage.WriteBufferDirectly(frame, bitmapTmp);
                }));

                lock (frameLock)
                {
                    if (frameToSend != null && !frameToSend.IsDisposed)
                    {
                        frame.CopyTo(frameToSend);
                    }
                    else
                    {
                        frameToSend = frame.Clone();
                    }
                }

                //if (isSendAuto)
                //{
                //    GlobalLogManager.Instance.ConsoleLog("isSendAutoConditionEntered");
                //    _ = SendFramePeriodically();
                //}

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

        public void CompareWithPrevFrame()
        {


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
