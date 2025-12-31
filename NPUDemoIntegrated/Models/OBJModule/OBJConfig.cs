using NPUDemoIntegrated.Utils;
using OpenCvSharp.XImgProc;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public enum ImageMode { RESIZE, PAD }
public enum ImageSize { S384, S320 }

namespace NPUDemoIntegrated.Models.OBJModule
{
    class OBJConfig: SerialConfig
    {
        private bool _is_send_all = true;
        private int _chunk_size = 1024;
        private int _prob_thres = 50;
        private bool _is_spi_enable = false;

        private ImageMode _img_mode = ImageMode.RESIZE;
        private ImageSize _img_size = ImageSize.S320;

        public enum ClassArray
        {
            person, bicycle, car, motorbike, aeroplane, bus, train, truck, boat, traffic_light, fire_hydrant,
            stop_sign, parking_meter, bench, bird, cat, dog, horse, sheep, cow, elephant, bear, zebra, giraffe,
            backpack, umbrella, handbag, tie, suitcase, frisbee, skis, snowboard, sports_ball, kite, baseball_bat,
            baseball_glove, skateboard, surfboard, tennis_racket, bottle, wine_glass, cup, fork, knife, spoon,
            bowl, banana, apple, sandwich, orange, broccoli, carrot, hot_dog, pizza, donut, cake, chair, sofa,
            pottedplant, bed, diningtable, toilet, tvmonitor, laptop, mouse, remote, keyboard, cell_phone, microwave,
            oven, toaster, sink, refrigerator, book, clock, vase, scissors, teddy_bear, hair_drier, toothbrush
        }

        public bool[] vis_state = new bool[80];

        public int chunk_size
        {
            get { return _chunk_size; }
            set { _chunk_size = value; OnPropertyChanged(); }
        }

        public bool is_send_all
        {
            get { return _is_send_all; }
            set { _is_send_all = value; OnPropertyChanged(); }
        }

        public bool is_spi_enable
        {
            get { return _is_spi_enable; }
            set { _is_spi_enable = value; OnPropertyChanged(); }
        }

        public int prob_thres
        {
            get { return _prob_thres; }
            set { _prob_thres = value; OnPropertyChanged(); }
        }

        public ImageMode img_mode
        {
            get { return _img_mode; }
            set { _img_mode = value; OnPropertyChanged(); }
        }
        public ImageSize img_size
        {
            get { return _img_size; }
            set { _img_size = value; OnPropertyChanged(); }
        }
    }
}
