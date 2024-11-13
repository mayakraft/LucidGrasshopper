using ArenaNET;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Linq;
using System.Collections.Generic;
using Rhino.Commands;
using Rhino.Display;
using static LucidArena.AlignCameras;
using Emgu.CV;

// In order to load the result of this wizard, you will also need to
// add the output bin/ folder of this project to the list of loaded
// folder in Grasshopper.
// You can use the _GrasshopperDeveloperSettings Rhino command for that.

namespace LucidArena
{
    public class LucidAlignCameras : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public LucidAlignCameras()
          : base("Lucid Align Cameras", "Align",
              "Find the displacement between a Triton and Helios camera",
              "Lucid", "Subcategory")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Calibrate", "Calibrate", "Make a calibration between Triton and Helios", GH_ParamAccess.item);
            pManager.AddNumberParameter("Triton Matrix", "TritonMat", "the result of calibrating the Triton Camera", GH_ParamAccess.list);
            pManager.AddNumberParameter("Triton Distortion Coefficients", "Distortion", "the result of calibrating the Triton Camera", GH_ParamAccess.list);
            //pManager.AddGenericParameter("cv::Mat Triton Matrix", "RAW TritonMat", "the result of calibrating the Triton Camera", GH_ParamAccess.item);
            //pManager.AddGenericParameter("cv::Mat Triton Distortion RAW Coefficients", "Distortion", "the result of calibrating the Triton Camera", GH_ParamAccess.item);
            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Status", "Status", "Device Information", GH_ParamAccess.item);
            pManager.AddNumberParameter("Translation", "Translation", "the translation component between the two cameras", GH_ParamAccess.list);
            pManager.AddNumberParameter("Rotation", "Rotation", "the rotation component between the two cameras", GH_ParamAccess.list);
            //pManager.AddGenericParameter("cv::Mat Translation", "Raw Translation", "the translation component between the two cameras", GH_ParamAccess.item);
            //pManager.AddGenericParameter("cv::Mat Rotation", "Raw Rotation", "the rotation component between the two cameras", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var snapPhoto = false;
            Array translationArray = Array.CreateInstance(typeof(double), 0);
            Array rotationArray = Array.CreateInstance(typeof(double), 0);
            List<string> info = new List<string>();
            List<double> calibMat = new List<double> { 1, 0, 0, 0, 1, 0, 0, 0, 1};
            List<double> distCoef = new List<double> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0};
            // Mat calibrationMatrix = new Mat(3, 3, Emgu.CV.CvEnum.DepthType.Cv8U, 1);
            // Mat distortionCoefficients = new Mat(1, 14, Emgu.CV.CvEnum.DepthType.Cv8U, 1);

            DA.GetData(0, ref snapPhoto);
            DA.GetData(1, ref calibMat);
            DA.GetData(2, ref distCoef);
            // DA.GetData(1, ref calibrationMatrix);
            // DA.GetData(2, ref distortionCoefficients);

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
                //DA.SetDataList(1, translation.GetData());
                //DA.SetDataList(2, rotation.GetData());
                //DA.SetData(3, translation);
                //DA.SetData(4, rotation);
                return;
            }

            try
            {
                Mat calibrationMatrix = new Mat(3, 3, Emgu.CV.CvEnum.DepthType.Cv64F, 1);
                Mat distortionCoefficients = new Mat(1, 14, Emgu.CV.CvEnum.DepthType.Cv64F, 1);
                calibrationMatrix.SetTo(calibMat.ToArray());
                distortionCoefficients.SetTo(distCoef.ToArray());

                calibrationMatrix.Reshape(3, 3);

                var (translation, rotation) = CalculateAndSaveOrientationValues(tritonDevices[0], heliosDevices[0], calibrationMatrix, distortionCoefficients);
                DA.SetDataList(1, translation.GetData());
                DA.SetDataList(2, rotation.GetData());
                //DA.SetData(3, translation);
                //DA.SetData(4, rotation);
            }
            catch (Exception error)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error.ToString());
                DA.AbortComponentSolution();
            }

            DA.SetData(0, string.Join("\n", info));
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
            get { return new Guid("ab130f86-dffa-4a6c-8651-b0e02de5330d"); }
        }
    }
}
