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
    public class LucidCloudPoints : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public LucidCloudPoints()
          : base("Lucid Cloud Points", "Cloud Pts",
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
            pManager[0].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Status", "Status", "Device Information", GH_ParamAccess.item);
            pManager.AddPointParameter("Points", "Points", "List of points composing the point cloud", GH_ParamAccess.list);
            pManager.AddColourParameter("Colors", "Colors", "List of colors composing the point cloud", GH_ParamAccess.list);
            pManager.AddNumberParameter("Intensity", "Intensity", "List of intensities for each point", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var snapPhoto = false;

            var points = new List<Point3d>();
            var colors = new List<Color4f>();
            var intensities = new List<int>();

            List<string> info = new List<string>();

            DA.GetData(0, ref snapPhoto);

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
                DA.SetDataList(1, points);
                DA.SetDataList(2, colors);
                DA.SetDataList(3, intensities);
                return;
            }

            try
            {
                List<double> calibMat = new List<double> { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
                List<double> distCoef = new List<double> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                List<double> translation = new List<double> { 0, 0, 0 };
                List<double> rotation = new List<double> { 0, 0, 0 };
                Mat calibrationMatrix = new Mat(3, 3, Emgu.CV.CvEnum.DepthType.Cv64F, 1);
                Mat distortionCoefficients = new Mat(1, 14, Emgu.CV.CvEnum.DepthType.Cv64F, 1);
                Mat rotationVector = new Mat(1, 3, Emgu.CV.CvEnum.DepthType.Cv64F, 1);
                Mat translationVector = new Mat(1, 3, Emgu.CV.CvEnum.DepthType.Cv64F, 1);

                calibrationMatrix.SetTo(calibMat.ToArray());
                distortionCoefficients.SetTo(distCoef.ToArray());
                rotationVector.SetTo(rotation.ToArray());
                translationVector.SetTo(translation.ToArray());

                (points, intensities, colors) = CaptureImageAndCloud(
                    tritonDevices[0],
                    heliosDevices[0],
                    calibrationMatrix,
                    distortionCoefficients,
                    rotationVector,
                    translationVector);
                //info.Add(calibration);
            }
            catch (Exception error)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error.ToString());
                DA.AbortComponentSolution();
            }

            DA.SetData(0, string.Join("\n", info));
            DA.SetDataList(1, points);
            DA.SetDataList(2, colors);
            DA.SetDataList(3, intensities);
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
            get { return new Guid("320821e0-bcee-42e2-b12e-56d4bedbac17"); }
        }
    }
}
