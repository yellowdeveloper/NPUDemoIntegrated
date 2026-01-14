using NPUDemoIntegrated.Utils;
using OpenCvSharp.XImgProc;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NPUDemoIntegrated.Models.OBJModule
{
    class OBJConfig: Notifier
    {
        private EImageSize _imgSize = EImageSize.S320;

        public enum EClassArray
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

        public EImageSize imgSize
        {
            get { return _imgSize; }
            set { _imgSize = value; OnPropertyChanged(); }
        }
    }
}
