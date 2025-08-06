using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace Cameca.CustomAnalysis.Pca.Utils
{
    public interface IGridsColorMapProvider
    {
        IColorMap? ColorMapForGrid(bool tinted);
        void SetColorMapFactory(IColorMapFactory? factory);
    }


    internal class PcaGridsColorMapProvider : IGridsColorMapProvider
    {
        IColorMapFactory? factory;
        public PcaGridsColorMapProvider(IColorMapFactory? colorMapFactory)
        {
            this.factory = colorMapFactory;
        }

        public void SetColorMapFactory(IColorMapFactory? colorMapFactory)
        {
            this.factory = colorMapFactory;
        }

        public IColorMap? ColorMapForGrid(bool tinted)
        {
            return tinted ? PcaGridPartitionsColorMap() : PcaGridGrayscaleColorMap();
        }


        private IColorMap? DeserializeColorMap(SerializableColorMap serializedColorMap)
        {
            if (factory != null)
            {
                var colorMap = factory.CreateColorMap(
                    serializedColorMap.Bottom,
                    serializedColorMap.NanColor,
                    serializedColorMap.OutOfRangeBottom,
                    serializedColorMap.OutOfRangeTop,
                    serializedColorMap.Top,
                    serializedColorMap.ColorStops.Select(x =>
                        factory.CreateColorStop(x.RelativePosition, x.TopColor, x.BottomColor)));
                colorMap.BottomValue = serializedColorMap.BottomValue;
                colorMap.TopValue = serializedColorMap.TopValue;
                return colorMap;
            }
            return null;
        }

        private SerializableColorMap SerializablePcaGridPartitionsColorMap()
        {
            // make 4 zones  in the band from 0 to 4
            // 0-1 is regular gray scale
            // 1-2, 2-3, 3-4 are grayscale-ish bands with a tint 
            // that means 3 color stops in addition to the top and bottom colors 
            // of the ColorMap
            SerializableColorMap scm = new SerializableColorMap();
            List<SerializableColorStop> colorStops = new List<SerializableColorStop>();
            var unknownColor = Color.FromRgb(255, 128, 0);
            var whiteColor = Color.FromRgb(255, 255, 255);
            var blackColor = Color.FromRgb(0, 0, 0);
            for (var i = 1; i < 4; ++i)
            {
                SerializableColorStop colorStop = new SerializableColorStop();
                byte br = 64;
                byte bg = 64;
                byte bb = 64;
                byte tr = 255;
                byte tg = 255;
                byte tb = 255;
                colorStop.RelativePosition = ((float)i) * 0.25f;
                switch (i)
                {
                    case 1:
                        // apply red tint to top
                        tr -= 64;
                        break;
                    case 2:
                        tg -= 64;
                        br -= 64;
                        break;
                    case 3:
                        tb -= 64;
                        bg -= 64;
                        break;
                }
                colorStop.BottomColor = Color.FromRgb(br, bg, bb);
                colorStop.TopColor = Color.FromRgb(tr, tg, tb);
                colorStops.Add(colorStop);
            }
            scm.Bottom = whiteColor;
            scm.NanColor = unknownColor;
            scm.OutOfRangeBottom = unknownColor;
            scm.OutOfRangeTop = unknownColor;
            scm.Top = Color.FromRgb(0, 0, 64);

            scm.ColorStops = colorStops;
            scm.BottomValue = 0.0f;
            scm.TopValue = 4.0f;
            return scm;
        }

        private SerializableColorMap SerializablePcaGridColorMap(bool withTint)
        {
            // like the GridPartitions ColorMap, but without the tint
            SerializableColorMap scm = new SerializableColorMap();
            List<SerializableColorStop> colorStops = new List<SerializableColorStop>();
            var unknownColor = Color.FromRgb(255, 128, 0);
            var whiteColor = Color.FromRgb(255, 255, 255);
            var blackColor = Color.FromRgb(0, 0, 0);
            for (var i = 1; i < 4; ++i)
            {
                SerializableColorStop colorStop = new SerializableColorStop();
                byte br = 64;
                byte bg = 64;
                byte bb = 64;
                byte tr = 255;
                byte tg = 255;
                byte tb = 255;
                colorStop.RelativePosition = ((float)i) * 0.25f;
                if (withTint)
                {
                    switch (i)
                    {
                        case 1:
                            // apply red tint to top
                            tr -= 64;
                            break;
                        case 2:
                            tg -= 64;
                            br -= 64;
                            break;
                        case 3:
                            tb -= 64;
                            bg -= 64;
                            break;
                    }
                }
                colorStop.BottomColor = Color.FromRgb(br, bg, bb);
                colorStop.TopColor = Color.FromRgb(tr, tg, tb);
                colorStops.Add(colorStop);
            }
            scm.Bottom = whiteColor;
            scm.NanColor = unknownColor;
            scm.OutOfRangeBottom = unknownColor;
            scm.OutOfRangeTop = unknownColor;
            scm.Top = withTint ? Color.FromRgb(0, 0, 64) : Color.FromRgb(64, 64, 64);

            scm.ColorStops = colorStops;
            scm.BottomValue = 0.0f;
            scm.TopValue = 4.0f;
            return scm;
        }

        private IColorMap PcaGridGrayscaleColorMap()
        {
            // make 4 zones all the zones are normal grayscale
            // 0-1, 1-2, 2-3, 3-4  
            // that means 3 color stops in addition to the top and bottom colors 
            // of the ColorMap, like the PcaGridPartitionsColorMap but without the tint
            bool withTint = false;
            var scm = SerializablePcaGridColorMap(withTint);
            return DeserializeColorMap(scm);
        }
        private IColorMap PcaGridPartitionsColorMap()
        {
            // make 4 zones  in the band from 0 to 4
            // 0-1 is regular gray scale
            // 1-2, 2-3, 3-4 are grayscale-ish bands with a tint 
            // that means 3 color stops in addition to the top and bottom colors 
            // of the ColorMap

            // bool withTint = true;
            // var scm = SerializablePcaGridColorMap(withTint);
            var scm = SerializablePcaGridPartitionsColorMap();
            return DeserializeColorMap(scm);
        }

    }
}
