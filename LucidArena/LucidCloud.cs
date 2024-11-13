using ArenaNET;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Linq;
using System.Collections.Generic;
using Rhino.Commands;
using Rhino.Display;
using static LucidArena.ColorCloud;
using Emgu.CV;

// In order to load the result of this wizard, you will also need to
// add the output bin/ folder of this project to the list of loaded
// folder in Grasshopper.
// You can use the _GrasshopperDeveloperSettings Rhino command for that.

namespace LucidArena
{
    public class LucidCloud : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public LucidCloud()
          : base("Lucid Cloud", "Cloud",
              "Create a colored point cloud using Triton and Helios cameras in unison",
              "Lucid", "Subcategory")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Snap Cloud", "Snap", "Toggle input to capture the point cloud", GH_ParamAccess.item);
            pManager.AddNumberParameter("Intensity Threshhold", "Threshhold", "Filter to only include points with intensity above this threshold", GH_ParamAccess.item);

            pManager.AddNumberParameter("Triton Matrix", "Triton Mat", "the result of calibrating the Triton Camera. 3x3", GH_ParamAccess.list);
            pManager.AddNumberParameter("Triton Distortion Coefficients", "Distortion", "the result of calibrating the Triton Camera. 1xN", GH_ParamAccess.list);
            pManager.AddNumberParameter("Translation", "Translation", "the translation component between the two cameras. 1x3 or 3x1", GH_ParamAccess.list);
            pManager.AddNumberParameter("Rotation", "Rotation", "the rotation component between the two cameras. 1x3 or 3x1", GH_ParamAccess.list);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Status", "Status", "Device Information", GH_ParamAccess.item);
            pManager.AddGeometryParameter("Point Cloud", "Cloud", "A Rhino Geometry Point Cloud with points and colors", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var snapPhoto = false;
            var threshhold = 0.0;
            List<double> calibMat = new List<double> { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
            List<double> distCoef = new List<double> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            List<double> translation = new List<double> { 0, 0, 0 };
            List<double> rotation = new List<double> { 0, 0, 0 };

            var points = new List<Point3d>();
            var colors = new List<Color4f>();
            var intensities = new List<int>();
            var cloud = new PointCloud();

            List<string> info = new List<string>();

            DA.GetData(0, ref snapPhoto);
            DA.GetData(1, ref threshhold);

            DA.GetDataList(2, calibMat);
            DA.GetDataList(3, distCoef);
            DA.GetDataList(4, translation);
            DA.GetDataList(5, rotation);

            var tritonDevices = LucidManager.devices.Where(device => {
                String deviceModelName = ((ArenaNET.IString)device.NodeMap.GetNode("DeviceModelName")).Value;
                return deviceModelName.Contains("TRI") && deviceModelName.Contains("-C");
            }).ToList();

            var heliosDevices = LucidManager.devices.Where(device => {
                String deviceModelName = ((ArenaNET.IString)device.NodeMap.GetNode("DeviceModelName")).Value;
                return deviceModelName.StartsWith("HLT") || deviceModelName.StartsWith("HTP");
            }).ToList();

            if (heliosDevices.Count == 0 || tritonDevices.Count == 0)
            {
                DA.SetData(0, $"{heliosDevices.Count} Helios and {tritonDevices.Count} Triton cameras found.");
                DA.SetData(1, cloud);
                return;
            }

            try
            {
                Mat calibrationMatrix = new Mat(3, 3, Emgu.CV.CvEnum.DepthType.Cv64F, 1);
                Mat distortionCoefficients = new Mat(1, 14, Emgu.CV.CvEnum.DepthType.Cv64F, 1);
                Mat rotationVector = new Mat(1, 3, Emgu.CV.CvEnum.DepthType.Cv64F, 1);
                Mat translationVector = new Mat(1, 3, Emgu.CV.CvEnum.DepthType.Cv64F, 1);

                calibrationMatrix.SetTo(calibMat.ToArray());
                distortionCoefficients.SetTo(distCoef.ToArray());
                rotationVector.SetTo(rotation.ToArray());
                translationVector.SetTo(translation.ToArray());

                calibrationMatrix.Reshape(3, 3);

                (points, intensities, colors) = CaptureImageAndCloud(
                    tritonDevices[0],
                    heliosDevices[0],
                    calibrationMatrix,
                    distortionCoefficients,
                    rotationVector,
                    translationVector);

                // filter by intensity
                var drawingColors = colors
                    .Where((_, i) => intensities[i] > threshhold)
                    .Select(color => color.AsSystemColor())
                    .ToList();
                points = points
                    .Where((_, i) => intensities[i] > threshhold)
                    .ToList();

                cloud.AddRange(points, drawingColors);

                //var count = Math.Min(Math.Min(points.Count, colors.Count), intensities.Count);
                //for (int i = 0; i < count; i++)
                //{
                //    if (intensities[i] > threshhold)
                //    {
                //        cloud.Add(points[i], colors[i].AsSystemColor());
                //    }
                //}
            }
            catch (Exception error)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error.ToString());
                DA.AbortComponentSolution();
            }

            DA.SetData(0, string.Join("\n", info));
            DA.SetData(1, cloud);
        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                //return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("93576e3e-c0b3-4c16-aa84-a85c580aee9e"); }
        }
    }
}
