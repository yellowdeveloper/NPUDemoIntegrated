using NPUDemoIntegrated.GlobalManagers;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace NPUDemoIntegrated.Models.OBJModule
{
    class WebCamControl
    {
        private VideoCapture _capture;
        private CancellationTokenSource _cts;
        private int _access_try = 0;

        public event Action<Mat> FrameUpdate;
        public void WebCamInitialize()
        {
            _capture?.Dispose();
            _capture = new VideoCapture(0);

            if (_capture.IsOpened())
            {
                GlobalLogManager.Instance.ConsoleLog("Web Cam enabled");
                _cts = new CancellationTokenSource();
                Task.Run(() => CaptureFrame(_cts.Token));
            }
            else
            {
                GlobalLogManager.Instance.ConsoleLog("ERROR!! Web Cam Initialize Failed");
                GlobalLogManager.Instance.AddLogToFile("ERROR", "capture failed. no frame to save :: waiting for frame :: " + _access_try.ToString());
            }
        }

        private async Task CaptureFrame(CancellationToken token)
        {
            while (_capture != null && _capture.IsOpened() && !token.IsCancellationRequested)
            {
                using (var frame = new Mat())
                {
                    _capture.Read(frame);
                    if (frame.Empty())
                    {
                        // if no frame for 1sec, reconnect cam
                        if (_access_try < 5)
                        {
                            _access_try++;
                            Console.WriteLine("capture failed. no frame to save :: waiting for frame :: " + _access_try.ToString());
                            GlobalLogManager.Instance.AddLogToFile("DEBUG", "capture failed. no frame to save :: waiting for frame :: " + _access_try.ToString());
                            await Task.Delay(500);
                            continue;
                        }

                        _cts.Cancel();

                        while (_cts.IsCancellationRequested)
                        {
                            GlobalLogManager.Instance.ConsoleLog("WARN.. Try to Initialize WebCam again ... ");
                            GlobalLogManager.Instance.AddLogToFile("DEBUG", "Try to Initialize WebCam again ... ");
                            WebCamInitialize();
                            await Task.Delay(500);
                        }
                    }
                    else
                    {
                        _access_try = 0;
                        FrameUpdate?.Invoke(frame.Clone());
                        await Task.Delay(33);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_cts != null)
            {
                try
                {
                    _cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
                catch (AggregateException)
                {
                }
                finally
                {
                    try { _cts.Dispose(); } catch { }
                    _cts = null;
                }
            }

            if (_capture != null)
            {
                try
                {
                    if (_capture.IsOpened())
                    {
                        _capture.Release();
                    }
                    _capture.Dispose();
                }
                catch (Exception ex)
                {
                    GlobalLogManager.Instance.ConsoleLog($"Capture Dispose Error: {ex.Message}");
                }
                finally
                {
                    _capture = null;
                }
            }

            GlobalLogManager.Instance.ConsoleLog("All Resources Disposed");
            GlobalManagers.GlobalLogManager.Instance.AddLogToFile("DEBUG", "WebCamControl Disposed");
        }
    }
}
