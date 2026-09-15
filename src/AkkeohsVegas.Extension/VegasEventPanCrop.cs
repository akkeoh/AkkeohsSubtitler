using System;
using ScriptPortal.Vegas;
using AkkeohsVegas.Core;

namespace AkkeohsVegas.Extension
{

    public static class VegasEventPanCrop
    {

        private const float SoftMargin = 0.38f;
        private const float LowerThirdMargin = 0.42f;
        private const float SideMargin = 0.28f;

        public static void Apply(Project project, VideoEvent videoEvent, string placement)
        {
            if (project == null || videoEvent == null)
                return;

            placement = PanCropPlacements.Normalize(placement);
            if (string.Equals(placement, PanCropPlacements.Default, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(placement, PanCropPlacements.Center, StringComparison.OrdinalIgnoreCase))
            {

                return;
            }

            if (videoEvent.VideoMotion == null ||
                videoEvent.VideoMotion.Keyframes == null ||
                videoEvent.VideoMotion.Keyframes.Count == 0)
            {
                return;
            }

            int width = 1920;
            int height = 1080;
            try
            {
                if (project.Video != null)
                {
                    if (project.Video.Width > 0)
                        width = project.Video.Width;
                    if (project.Video.Height > 0)
                        height = project.Video.Height;
                }
            }
            catch
            {

            }

            float dx;
            float dy;
            GetOffset(placement, width, height, out dx, out dy);

            VideoMotionKeyframe key = videoEvent.VideoMotion.Keyframes[0];
            key.MoveBy(new VideoMotionVertex(dx, dy));
        }

        private static void GetOffset(string placement, int width, int height, out float dx, out float dy)
        {
            float xSide = width * SideMargin;
            float ySoft = height * SoftMargin;
            float yLower = height * LowerThirdMargin;

            dx = 0f;
            dy = 0f;

            if (string.Equals(placement, PanCropPlacements.LowerThird, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(placement, PanCropPlacements.BottomCenter, StringComparison.OrdinalIgnoreCase))
            {
                dy = -yLower;
            }
            else if (string.Equals(placement, PanCropPlacements.BottomLeft, StringComparison.OrdinalIgnoreCase))
            {
                dx = -xSide;
                dy = -ySoft;
            }
            else if (string.Equals(placement, PanCropPlacements.BottomRight, StringComparison.OrdinalIgnoreCase))
            {
                dx = xSide;
                dy = -ySoft;
            }
            else if (string.Equals(placement, PanCropPlacements.TopCenter, StringComparison.OrdinalIgnoreCase))
            {
                dy = ySoft;
            }
            else if (string.Equals(placement, PanCropPlacements.TopLeft, StringComparison.OrdinalIgnoreCase))
            {
                dx = -xSide;
                dy = ySoft;
            }
            else if (string.Equals(placement, PanCropPlacements.TopRight, StringComparison.OrdinalIgnoreCase))
            {
                dx = xSide;
                dy = ySoft;
            }
        }
    }
}
