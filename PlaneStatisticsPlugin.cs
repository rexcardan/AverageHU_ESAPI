using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMS.TPS.Common.Model;
namespace VMS.TPS
{
    public class Script
    {
        public Script()
        {
        }
        public void Execute(ScriptContext context /*, System.Windows.Window window*/)
        {
            //Get correct structure set... In our case structure set is named "PSOAS"
            var ss = context.Patient.StructureSets
            .FirstOrDefault(s => s.Structures.Any(st => st.Id.Equals("PSOAS", StringComparison.InvariantCultureIgnoreCase)));
            //Find the structure named "L5_MID"
            var l5mid = ss.Structures.FirstOrDefault(s => s.Id.Equals("L5_MID", StringComparison.InvariantCultureIgnoreCase));
            double avg = double.NaN;
            if (l5mid != null)
            {
                //Create a map of slice area to image slice, so we know which slice to sample
                var l5midContours = GetSliceAreas(ss.Image, l5mid);
                //If all slices have a NaN value, then there is no contour
                if (!l5midContours.Any(p => !double.IsNaN(p.Value) && p.Value > 0))
                {
                    MessageBox.Show("L5 Mid not found");
                }
                else
                {
                    //Assumption is L5 contour is only on one slice, find the first slice with a valid area
                    var z = l5midContours.First(p => !double.IsNaN(p.Value) && p.Value > 0).Key;
                    avg = GetSliceHUAvg(ss.Image, z, l5mid);
                    MessageBox.Show(string.Format("L5_MID Stats :\nArea: {0} cm^2\nHUavg = {1} HU", l5midContours[z].ToString("F2"), avg.ToString("F2")));
                }
            }
        }
        /// <summary>
        /// Creates a map of slice position z to area of the structure on that slice. Used for finding the correct slice to sample
        /// </summary>
        private static Dictionary<int, double> GetSliceAreas(Image image, Structure s)
        {
            Dictionary<int, double> slices = new Dictionary<int, double>();
            for (int z = 0; z < image.ZSize; z++)
            {
                var contourArea = GetSliceArea(image, z, s);
                slices.Add(z, contourArea);
            }
            return slices;
        }
        /// <summary>
        /// Calculates the slice area inside of a contour definition for a given slice Z
        /// </summary>
        private static double GetSliceArea(Image image, int sliceZ, Structure psoas)
        {
            var area = double.NaN;
            var contour = psoas.GetContoursOnImagePlane(sliceZ);
            if (contour.Count() > 0)
            {
                var inside = 0;
                for (int x = 0; x < image.XSize; x++)
                {
                    for (int y = 0; y < image.XSize; y++)
                    {
                        var dx = (x * image.XRes * image.XDirection + image.Origin).x;
                        var dy = (y * image.YRes * image.YDirection + image.Origin).y;
                        var dz = (sliceZ * image.ZRes * image.ZDirection + image.Origin).z;
                        if (psoas.IsPointInsideSegment(new VVector(dx, dy, dz)))
                        {
                            inside++;
                        }
                    }
                }
                var vxArea = image.XRes / 10 * image.YRes / 10;
                var contourArea = inside * vxArea;
                area = contourArea;
            }
            return area;
        }
        /// <summary>
        /// For a given slice, and structure this will return the average hounsfield unit inside the contour
        /// </summary>
        private static double GetSliceHUAvg(Image image, int sliceZ, Structure structr)
        {
            var contour = structr.GetContoursOnImagePlane(sliceZ);
            if (contour.Count() > 0)
            {
                int[,] buffer = new int[image.XSize, image.YSize];
                List<double> hus = new List<double>();
                image.GetVoxels(sliceZ, buffer);
                for (int x = 0; x < image.XSize; x++)
                {
                    for (int y = 0; y < image.XSize; y++)
                    {
                        var dx = (x * image.XRes * image.XDirection + image.Origin).x;
                        var dy = (y * image.YRes * image.YDirection + image.Origin).y;
                        var dz = (sliceZ * image.ZRes * image.ZDirection + image.Origin).z;
                        if (structr.IsPointInsideSegment(new VVector(dx, dy, dz)))
                        {
                            var voxel = buffer[x, y];
                            hus.Add(image.VoxelToDisplayValue(voxel));
                        }
                    }
                }
                return hus.Average();
            }
            return double.NaN;
        }
    }
}