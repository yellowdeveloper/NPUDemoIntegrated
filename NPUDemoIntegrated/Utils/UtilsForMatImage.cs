using NPUDemoIntegrated.GlobalManagers;
using NPUDemoIntegrated.Models;
using NPUDemoIntegrated.Models.OBJModule;
using OpenCvSharp;
using System.Windows.Media.Imaging;

namespace NPUDemoIntegrated.Utils
{
    class UtilsForMatImage
    {
        private static readonly int[] sobel_x = { -1, 0, 1, -2, 0, 2, -1, 0, 1 };
        private static readonly int[] sobel_y = { -1, -2, -1, 0, 0, 0, 1, 2, 1 };

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
        public static Rect DrawTextWithBox <TClassArray> (Mat frame, Scalar rectColor, Scalar textColor, TClassArray cls, int prob, OpenCvSharp.Rect box, List<Rect>textBoxs)
        {
            string text = $"class: {cls.ToString()}  prob: {prob}";
            var font = HersheyFonts.Italic;
            double font_scale = 0.8;
            int thickness = 2;

            Size text_size = Cv2.GetTextSize(text, font, font_scale, thickness, out int baseline);
            var coord = new Point(box.X - 1, box.Y - 1);

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

            Rect background_rect = new Rect(
                coord.X,
                coord.Y - text_size.Height - baseline,
                text_size.Width,
                text_size.Height + 1 * baseline
                );

            background_rect = AvoidTextBoxIntersection(background_rect, textBoxs);
            coord.X = background_rect.X;
            coord.Y = background_rect.Y + text_size.Height;

            Cv2.Rectangle(frame, background_rect, rectColor, -1);
            Cv2.PutText(frame, text, coord, font, font_scale, textColor, thickness, LineTypes.AntiAlias);

            GlobalLogManager.Instance.ConsoleLog("Text Box Drawing Completed");
            GlobalLogManager.Instance.AddLogToFile("DEBUG", "Text Box Drawing Completed");

            return background_rect;
        }

        /// <summary>
        /// Avoid Text Box Intersection with Previous Text Boxes
        /// </summary>
        /// <param name="text_box"></param>
        /// <returns></returns>
        private static Rect AvoidTextBoxIntersection(Rect text_box, List<Rect> textBoxs)
        {
            if (textBoxs.Count == 0) return text_box;

            bool is_intersect = false;

            do
            {
                is_intersect = false;
                foreach (var box in textBoxs)
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

        public static unsafe bool FindEdgeInRegion(Mat frame, Rect region)
        {
            int width;
            int height;
            byte* refData;
            // int totalBytes;

            // sobel edge detection
            using (Mat refRect = new Mat())
            {
                Cv2.CvtColor(frame[region], refRect, ColorConversionCodes.BGR2GRAY);

                width = refRect.Width;
                height = refRect.Height;

                refData = (byte*)refRect.DataPointer;
                // totalBytes = (int)refRect.Total() * refRect.ElemSize();
            }

            int thresh = 180;
            int cnt = 0;

            // GlobalLogManager.Instance.ConsoleLog($"Size :: {totalBytes}");

            for (int i = 1; i < height - 1; i += 1)
            {
                for (int j = 1; j < width - 1; j += 1)
                {
                    int sobel_sum = 0;
                    // sobel x
                    for (int k = 0; k < sobel_x.Length; k++)
                    {
                        sobel_sum += refData[((i + (k / 3 - 1)) * width) + (j + (k % 3 - 1))] * sobel_x[k];
                    }
                    if (sobel_sum < 0) sobel_sum = -sobel_sum;
                    if (sobel_sum > thresh) cnt++;

                    // sobel y
                    for (int k = 0; k < sobel_y.Length; k++)
                    {
                        sobel_sum += refData[((i + (k / 3 - 1)) * width) + (j + (k % 3 - 1))] * sobel_y[k];
                    }
                    if (sobel_sum < 0) sobel_sum = -sobel_sum;
                    if (sobel_sum > thresh)
                    {
                        cnt++;
                        //GlobalLogManager.Instance.ConsoleLog($"OVER THRESH :: {cnt}, {sobel_sum}");
                    }
                }
            }

            if (cnt >= 15)
            {
                GlobalLogManager.Instance.ConsoleLog($"ERROR!! EDGE DETECTED CNT :: {cnt}");
                return false;
            }
            else
            {
                return true;
            }
        }

        public static bool CheckIfRegionWhite(Mat frame, Rect region)
        {
            using (Mat refRect = frame[region])
            {
                Scalar meanColor = Cv2.Mean(refRect);

                double meanB = meanColor.Val0;
                double meanG = meanColor.Val1;
                double meanR = meanColor.Val2;

                GlobalLogManager.Instance.ConsoleLog($"RefBox Mean Color - B:{meanB:F1}, G:{meanG:F1}, R:{meanR:F1}");

                double maxColor = Math.Max(meanR, Math.Max(meanG, meanB));
                double minColor = Math.Min(meanR, Math.Min(meanG, meanB));

                if (meanB < 110 || meanG < 110 || meanR < 110)
                {
                    GlobalLogManager.Instance.ConsoleLog("ERROR!! REF REGION IS TOO DARK!");
                    return false;
                }

                if (maxColor - minColor > 35)
                {
                    GlobalLogManager.Instance.ConsoleLog($"ERROR!! REF REGION IS NOT WHITE!!");
                    return false;
                }

                return true;
            }
        }
    }
}
