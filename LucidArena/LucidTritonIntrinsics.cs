using ArenaNET;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Linq;
using System.Collections.Generic;
using Rhino.Commands;
using Rhino.Display;
using static LucidArena.TritonDevice;

using Emgu.CV;
// In order to load the result of this wizard, you will also need to
// add the output bin/ folder of this project to the list of loaded
// folder in Grasshopper.
// You can use the _GrasshopperDeveloperSettings Rhino command for that.

namespace LucidArena
{
    public class LucidTritonIntrinsics : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public LucidTritonIntrinsics()
          : base("Lucid Triton Intrinsics", "Intrinsics",
              "Discover the intrinsics for a Triton Camera",
              "Lucid", "Subcategory")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Calibrate", "Calibrate", "Find the intrinsics", GH_ParamAccess.item);
            pManager[0].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Status", "Status", "Device Information", GH_ParamAccess.item);
            pManager.AddNumberParameter("Camera Matrix", "Camera Matrix", "the camera's Matrix", GH_ParamAccess.list);
            pManager.AddNumberParameter("Distortion Coefficients", "Dist Coeffs", "the camera's distortion coefficients", GH_ParamAccess.list);
            //pManager.AddGenericParameter("cv::Mat Camera Matrix", "Raw Camera Matrix", "the camera's Matrix", GH_ParamAccess.item);
            //pManager.AddGenericParameter("cv::Mat Distortion Coefficients", "Raw Dist Coeffs", "the camera's distortion coefficients", GH_ParamAccess.item);
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
            List<string> info = new List<string>();
            Mat cameraMatrix, distCoeffs;

            DA.GetData(0, ref snapPhoto);

            var tritonDevices = LucidManager.devices.Where(device => {
                String deviceModelName = ((ArenaNET.IString)device.NodeMap.GetNode("DeviceModelName")).Value;
                return deviceModelName.Contains("TRI") && deviceModelName.Contains("-C");
            }).ToList();

            if (tritonDevices.Count == 0)
            {
                DA.SetData(0, "No triton color camera found.");
                return;
            }

            try
            {
                (cameraMatrix, distCoeffs) = CalculateAndSaveCalibrationValues(tritonDevices[0], out var calculationInfo);
                info.Add($"cameraMatrix: {cameraMatrix}");
                info.Add($"distCoeffs: {distCoeffs}");
                info.Add($"camera matrix info (cols, rows): {cameraMatrix.Cols}, {cameraMatrix.Rows}");
                info.Add($"distance coefficients matrix info (cols, rows): {distCoeffs.Cols}, {distCoeffs.Rows}");
                info.Add(calculationInfo);
                DA.SetDataList(1, cameraMatrix.GetData());
                DA.SetDataList(2, distCoeffs.GetData());
                //DA.SetData(3, cameraMatrix);
                //DA.SetData(4, distCoeffs);
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
            get { return new Guid("ef62707f-df44-4a7e-82ad-78d5aaeaeb24"); }
        }
    }
}
