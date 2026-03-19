using NPUDemoIntegrated.GlobalManagers;
using NPUDemoIntegrated.Models;
using NPUDemoIntegrated.Models.OBJModule;
using NPUDemoIntegrated.Utils;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace NPUDemoIntegrated.ViewModels
{
    /// <summary>
    /// OBJ View Model Class: inherits from BaseViewModel
    /// </summary>
    class OBJViewModel : BaseViewModel
    {
        /// <summary>
        /// readonly Variables for Module Control
        /// </summary>
        private readonly OBJSerialService serialService;
        // private readonly Timer timer;
        private readonly Stopwatch stopwatch = new Stopwatch();
        private WebCamControl webCamControl;

        /// <summary>
        /// Lock Objects for Thread Safety
        /// </summary>
        private readonly object frameLock = new object();
        private readonly object bboxLock = new object();
        private readonly object sendLock = new object();

        /// <summary>
        /// Image Variables for Update Images
        /// </summary>
        private BitmapSource _bitmap;
        private BitmapSource _bitmapSent;
        private WriteableBitmap bitmapTmp;
        private WriteableBitmap bitmapSentTmp;
        private Mat frameToSend;
        private Mat frameToDraw;
        private Mat tmpMat;

        /// <summary>
        /// List Variables for Drawing Inference Results
        /// </summary>
        private List<OpenCvSharp.Rect> bbox = new List<OpenCvSharp.Rect>();

        /// <summary>
        /// UI Binded Properties
        /// </summary>
        private bool _isSendAuto = false;
        private double _fps = 0.0;
        private float _volt = 0.0f;
        private float _amp = 0.0f;

        /// <summary>
        /// Internal Variables for Communication Control
        /// </summary>
        private bool isSending = false;
        private int tryCount = 0;

        // just for test
        private bool testFlag = false;

        /// <summary>
        /// config objects
        /// </summary>
        public OBJConfig objConfig { get; }
        public SerialConfig serialConfig { get; }

        /// <summary>
        /// Button Commands
        /// </summary>
        public override ICommand ConnectCommand { get; }
        public override ICommand DisconnectCommand { get; }
        public ICommand SendCommand { get; }

        /// <summary>
        /// Title Bindings for Header View
        /// </summary>
        public override string title => "Doksan NPU Real-Time Vision AI Demonstration";
        public override string subTitle => "Real-time camera input and on-device object detection inference";

        /// <summary>
        /// public Property for Display Image Binding (Real-Time)
        /// </summary>
        public BitmapSource bitmapShow
        {
            get => _bitmap;
            set { _bitmap = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// public Property for Display Image Binding (inferenced)
        /// </summary>
        public BitmapSource bitmapSent
        {
            get => _bitmapSent;
            set { _bitmapSent = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Color Property for Connection Status Indicator Binding
        /// </summary>
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

        /// <summary>
        /// Frame Rate Property Binding
        /// </summary>
        public double fps
        {
            get => _fps;
            set { _fps = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Auto Send Option Property Binding
        /// </summary>
        public bool isSendAuto
        {
            get => _isSendAuto;
            set
            {
                _isSendAuto = value;
                GlobalLogManager.Instance.ConsoleLog("isSendAutoEnabled");
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Power Consumption Property Binding (Watt)
        /// </summary>
        public float volt
        {
            get => _volt;
            set { _volt = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Power Consumption Property Binding (Ampere)
        /// </summary>
        public float amp
        {
            get => _amp;
            set { _amp = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// ViewModel Constructor for Obect Detection Module
        /// </summary>
        /// <remarks>
        /// Create Instances, Commands, Event Handlers and set Timer
        /// </remarks>
        /// <param name="_serialConfig">
        /// for Dependency Injection of Config(Serial) Object
        /// </param>
        /// <param name="_objConfig">
        /// for Dependency Injection of Config(Object Detection Model) Object
        /// </param>
        /// <param name="service">
        /// for Dependency Injection of SerialService Object
        /// </param>
        public OBJViewModel(SerialConfig _serialConfig, OBJConfig _objConfig, OBJSerialService service)
        {
            serialService = service;
            objConfig = _objConfig;
            serialConfig = _serialConfig;

            tmpMat = new Mat();

            //_viewModelId = DateTime.Now.ToString("\nInstance Creadted Time == HH:mm:ss.fff\n");

            serialService.PointsReceived += OnPointsReceived;

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

            SendCommand = new RelayCommand(async param => {
                isSendAuto = false;
                await SendFramePeriodically();
                if (is_menu_open) is_menu_open = !is_menu_open;
                // GlobalLogManager.Instance.ConsoleLog("Manual Send Completed");
            });
        }

        /// <summary>
        /// Method for Sending Frame Periodically
        /// </summary>
        /// <remarks>
        /// It Copies current frame (one for sending, one for drawing) under lock and pass as an argument to SerialCommunication Method(Declared in SerialService)
        /// </remarks>
        /// <returns></returns>
        private async Task SendFramePeriodically()
        {
            GlobalLogManager.Instance.ConsoleLog($"SendFramePeriodicallyEntered trc::{tryCount} | state:: {serialService.connectionState} | isSending:: {isSending}");
            if (!isSending && tryCount >= 15 && serialService.connectionState == EConnectionState.WaitingForInference)
            {
                serialService.connectionState = EConnectionState.Connected;
                GlobalLogManager.Instance.ConsoleLog($"WARN.. SendFrame Re-Called: connection_status set to: {serialService.connectionState}");
                GlobalLogManager.Instance.AddLogToFile("DEBUG", $"SendFrame Re-Called: connection_status set to: {serialService.connectionState}");
            }
            if (!isSending && serialService.connectionState == EConnectionState.Connected)
            {
                isSending = true;

                GlobalLogManager.Instance.ConsoleLog($"OK.. Image Sending Method Called ... ");
                GlobalLogManager.Instance.AddLogToFile("DEBUG", $"Image Sending Method Called ... ");

                tryCount = 0;

                GlobalLogManager.Instance.ConsoleLog("\nMat Variable set");
                lock (frameLock)
                {
                    GlobalLogManager.Instance.ConsoleLog("\nFrameLock Called");
                    if (frameToSend == null) testFlag = true;
                    else {
                        try
                        {
                            frameToSend.CopyTo(tmpMat);
                        }
                        catch (Exception ex)
                        {
                            GlobalLogManager.Instance.ConsoleLog($"ERROR {ex}");
                        }
                    }
                }

                if (testFlag)
                {
                    try
                    {
                        GlobalLogManager.Instance.ConsoleLog($"WARN.. frame empty, sending 0 ~ 255");
                        await serialService.SerialCommunication(new Mat());
                        return;
                    }
                    finally
                    {
                        isSending = false;
                    }
                }

                Mat resized;
                if (serialConfig.imgMode == EImageMode.RESIZE)
                {
                    resized = UtilsForMatImage.Resize(tmpMat, objConfig.imgSize == EImageSize.S320 ? 320 : 384);
                }
                else
                {
                    resized = UtilsForMatImage.Pad(tmpMat, objConfig.imgSize == EImageSize.S320 ? 320 : 384);
                }

                lock (sendLock)
                {
                    GlobalLogManager.Instance.ConsoleLog("\nSendLock Called");
                    if (frameToDraw != null && !frameToDraw.IsDisposed)
                    {
                        tmpMat.CopyTo(frameToDraw);
                    }
                    else
                    {
                        frameToDraw = tmpMat.Clone();
                    }
                }

                try
                {
                    GlobalLogManager.Instance.ConsoleLog("\nSerialCommunication Called");
                    stopwatch.Restart();
                    await serialService.SerialCommunication(resized);
                }
                finally
                {
                    isSending = false;
                    resized.Dispose();
                }
            }
            else if (serialService.connectionState == EConnectionState.WaitingForInference && isSendAuto)
            {
                GlobalLogManager.Instance.ConsoleLog($"SendFrame Failed ... is_sending: {isSending}  connection_status: {serialService.connectionState} try_count: {tryCount}"  );
                //GlobalLogManager.Instance.AddLogToFile("ERROR", $"SendFrame Failed ... is_sending: {_is_sending}  connection_status: {_connection_status}  try_count: {_try_count}");
                tryCount++;
            }
        }

        /// <summary>
        /// Event: On FrameUpdate from WebCamControl
        /// </summary>
        /// <remarks>
        /// updates in 33ms interval, Copies current Frame for Real-Tiem Display and Processing SendFramePeriodically Method
        /// </remarks>
        /// <param name="frame"></param>
        private void OnFrameUpdate(Mat frame)
        {
            try
            {
                if (Application.Current == null) return;
                Application.Current.Dispatcher.Invoke(new Action(() => {
                    if (bitmapTmp == null || bitmapTmp.PixelWidth != frame.Width || bitmapTmp.PixelHeight != frame.Height)
                    {
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

                if (isSendAuto)
                {
                    // GlobalLogManager.Instance.ConsoleLog($"isSendAutoConditionEntered condition :: {serialService.connectionState }");
                    _ = SendFramePeriodically();
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
        /// <summary>
        /// Event: On PointsReceived from SerialService
        /// </summary>
        /// <remarks>
        /// When SerialService raises PointsReceived Event after getting Inference Results from NPU
        /// it draws Bboxes and Texts on the current frame and updates the Display Image
        /// </remarks>
        /// <param name="volt">
        /// Power Comsumption Value Received from NPU (Watt)
        /// </param>
        /// <param name="amp">
        /// Power Comsumption Value Received from NPU (ampere)
        /// </param>
        /// <param name="b_box">
        /// Bbox Value Received from NPU 
        /// </param>
        /// <param name="cls">
        /// Class Value Received from NPU
        /// </param>
        /// <param name="prob">
        /// Probability Value Received from NPU
        /// </param>
        private void OnPointsReceived(float volt, float amp, List<OpenCvSharp.Rect> b_box, List<OBJConfig.EClassArray> cls, List<int> prob)
        {
            string save_path = Path.Combine(GlobalConfigManager.Instance.GetImageFolderPath(), GlobalConfigManager.Instance.GetNowImageFileName());
            // GlobalLogManager.Instance.ConsoleLog($"OK.. Points Received ... Drawing Bbox");
            GlobalLogManager.Instance.AddLogToFile("DEBUG", "Points Received ... Drawing Bbox");

            int cnt = 0;

            lock (bboxLock)
            {
                bbox = b_box;
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
                    Cv2.Rectangle(frame_to_draw, box, Scalar.Red, 2);

                    textBoxs.Add(UtilsForMatImage.DrawTextWithBox(frame_to_draw, Scalar.Red, Scalar.White, cls[cnt], prob[cnt], box, textBoxs));
                    cnt++;
                }
                // Cv2.ImWrite(save_path, frame_to_draw);
            }

            // GlobalLogManager.Instance.ConsoleLog("OK.. Bbox drawing Completed ... Check Image\n");
            GlobalLogManager.Instance.AddLogToFile("DEBUG", "Bbox drawing Completed ... Check Image\n");

            stopwatch.Stop();
            var elapsed = stopwatch.Elapsed.TotalSeconds;

            // GlobalLogManager.Instance.ConsoleLog($"volt & amp: {amp}, {volt}");

            Application.Current.Dispatcher.Invoke(() => {
                if (bitmapSentTmp == null || bitmapSentTmp.PixelWidth != frame_to_draw.Width || bitmapSentTmp.PixelHeight != frame_to_draw.Height)
                {
                    bitmapSentTmp = new WriteableBitmap(frame_to_draw.Width, frame_to_draw.Height, 96, 96, System.Windows.Media.PixelFormats.Bgr24, null);
                    bitmapSent = bitmapSentTmp;
                }
                UtilsForMatImage.WriteBufferDirectly(frame_to_draw, bitmapSentTmp);

                fps = 1 / elapsed;

                this.volt = volt;
                this.amp = amp;

                frame_to_draw.Dispose();
            });

            // GlobalLogManager.Instance.ConsoleLog($"Frame Rate:: {fps}");
        }

        public override void DeactivateModule(EModuleType targetModule)
        {
            while (serialService.connectionState == EConnectionState.SendingImage)
            {
                GlobalLogManager.Instance.ConsoleLog($"now connection state ::{serialService.connectionState} wait until sending is finished");
                Thread.Sleep(5);
            }
            GlobalLogManager.Instance.ConsoleLog($"now connection state ::{serialService.connectionState}");

            isSendAuto = false;

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

            tmpMat = new Mat();
        }

        public override void Dispose()
        {
            if (webCamControl != null)
            {
                webCamControl.FrameUpdate -= OnFrameUpdate;
                webCamControl.Dispose();
                webCamControl = null;
            }

            serialService.PointsReceived -= OnPointsReceived;

            if (frameToDraw != null) frameToDraw.Dispose();
            if (frameToSend != null) frameToSend.Dispose();
            if (tmpMat != null) tmpMat.Dispose();
        }
    }
}
