using NPUDemoIntegrated.GlobalManagers;
using NPUDemoIntegrated.Models;
using NPUDemoIntegrated.Models.OBJModule;
using OpenCvSharp;
using System.Windows.Media.Imaging;

namespace NPUDemoIntegrated.Utils
{
    class UtilsForMatImage
    {
        /// <summary>
        /// Resize given Frame
        /// </summary>
        /// <param name="src"></param>
        /// <returns></returns>
        public static Mat Resize(Mat src, int size)
        {
            GlobalLogManager.Instance.ConsoleLog("Resizing bbox Image ...");
            GlobalLogManager.Instance.AddLogToFile("DEBUG", "Resizing Image ...");

            Size newSize = new Size(size, size);

            Mat resizedImage = new Mat();
            Cv2.Resize(src, resizedImage, newSize, 0, 0, InterpolationFlags.Linear);

            return resizedImage;
        }

        public static Mat Pad(Mat src, int size)
        {
            GlobalLogManager.Instance.ConsoleLog("Padding bbox Image ...");
            GlobalLogManager.Instance.AddLogToFile("DEBUG", "Padding bbox Image ...");

            Scalar color = new Scalar(0, 0, 0); // Black padding

            double w = src.Width;
            double h = src.Height;

            double ratio = w > h ? size / w : size / h;

            int w_resized = (int)(w * ratio);
            int h_resized = (int)(h * ratio);

            using (Mat resizedImage = new Mat())
            {
                Cv2.Resize(src, resizedImage, new Size(w_resized, h_resized), 0, 0, InterpolationFlags.Area);

                Mat canvas = new Mat(size, size, src.Type(), color);

                int top = (size - h_resized) / 2;
                int left = (size - w_resized) / 2;

                Rect roi = new Rect(left, top, w_resized, h_resized);
                resizedImage.CopyTo(canvas[roi]);

                return canvas;
            }
        }

        /// <summary>
        /// Draw Text And Text Box on The Frame
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="cls"></param>
        /// <param name="prob"></param>
        /// <param name="box"></param>
        /// <returns></returns>
        //private OpenCvSharp.Rect DrawTextWithBox(Mat frame, OBJConfig.EClassArray cls, int prob, OpenCvSharp.Rect box)
        //{
        //    string text = $"class: {cls.ToString()}  prob: {prob}";
        //    var font = HersheyFonts.Italic;
        //    double font_scale = 0.8;
        //    int thickness = 2;

        //    OpenCvSharp.Size text_size = Cv2.GetTextSize(text, font, font_scale, thickness, out int baseline);
        //    var coord = new OpenCvSharp.Point(box.X - 1, box.Y - 1);

        //    if (box.Y - text_size.Height < 0)
        //    {
        //        GlobalLogManager.Instance.ConsoleLog("Text Box Out of Bound Found! Adjusting ...");
        //        GlobalLogManager.Instance.AddLogToFile("DEBUG", "Text Box Out of Bound Found! Adjusting ...");
        //        coord.Y = box.Y + text_size.Height + 1;
        //    }
        //    if (box.X + text_size.Width > 640)
        //    {
        //        GlobalLogManager.Instance.ConsoleLog("Text Box Out of Bound Found! Adjusting ...");
        //        GlobalLogManager.Instance.AddLogToFile("DEBUG", "Text Box Out of Bound Found! Adjusting ...");
        //        coord.X = box.X - ((box.X + text_size.Width) - 640);
        //    }

        //    OpenCvSharp.Rect background_rect = new OpenCvSharp.Rect(
        //        coord.X,
        //        coord.Y - text_size.Height - baseline,
        //        text_size.Width,
        //        text_size.Height + 1 * baseline
        //        );

        //    background_rect = AvoidTextBoxIntersection(background_rect);
        //    coord.X = background_rect.X;
        //    coord.Y = background_rect.Y + text_size.Height;

        //    Cv2.Rectangle(frame, background_rect, Scalar.Red, -1);
        //    Cv2.PutText(frame, text, coord, font, font_scale, Scalar.White, thickness, LineTypes.AntiAlias);

        //    GlobalLogManager.Instance.ConsoleLog("Text Box Drawing Completed");
        //    GlobalLogManager.Instance.AddLogToFile("DEBUG", "Text Box Drawing Completed");

        //    return background_rect;
        //}

        /// <summary>
        /// Avoid Text Box Intersection with Previous Text Boxes
        /// </summary>
        /// <param name="text_box"></param>
        /// <returns></returns>
        //private OpenCvSharp.Rect AvoidTextBoxIntersection(OpenCvSharp.Rect text_box)
        //{
        //    if (textBoxs.Count == 0) return text_box;

        //    bool is_intersect = false;

        //    do
        //    {
        //        is_intersect = false;
        //        foreach (var box in textBoxs)
        //        {
        //            if (text_box.IntersectsWith(box))
        //            {
        //                GlobalLogManager.Instance.ConsoleLog("Text Box Intersection Found! Avoiding ...");
        //                GlobalLogManager.Instance.AddLogToFile("DEBUG", "Text Box Intersection Found! Avoiding ...");
        //                text_box.Y = box.Bottom + 3;
        //                is_intersect = true;
        //                break;
        //            }
        //        }
        //    } while (is_intersect);
        //    return text_box;
        //}

        public static unsafe void WriteBufferDirectly(Mat frame, WriteableBitmap tmp)
        {
            tmp.Lock();

            try
            {
                IntPtr framePtr = frame.Data;
                IntPtr tmpPtr = tmp.BackBuffer;

                int frameStride = (int)frame.Step();
                int tmpStride = tmp.BackBufferStride;
                int height = frame.Height;

                int totalLength = frame.Width * frame.ElemSize();

                if (frameStride == tmpStride)
                {
                    long totalBytes = (long)frameStride * height;
                    Buffer.MemoryCopy((void*)framePtr, (void*)tmpPtr, totalBytes, totalBytes);
                }
                else
                {
                    byte* pSrc = (byte*)framePtr;
                    byte* pDst = (byte*)tmpPtr;

                    for (int row = 0; row < height; row++)
                    {
                        Buffer.MemoryCopy(pSrc, pDst, tmpStride, totalLength);
                        pSrc += frameStride;
                        pDst += tmpStride;
                    }
                }
                tmp.AddDirtyRect(new System.Windows.Int32Rect(0, 0, tmp.PixelWidth, tmp.PixelHeight));
            }

            finally
            {
                tmp.Unlock();
            }
        }
    }
}
