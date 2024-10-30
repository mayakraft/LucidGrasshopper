using ArenaNET;
using Emgu.CV;
using Eto.Drawing;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static LucidArena.TritonDevice;

// In order to load the result of this wizard, you will also need to
// add the output bin/ folder of this project to the list of loaded
// folder in Grasshopper.
// You can use the _GrasshopperDeveloperSettings Rhino command for that.

namespace LucidArena
{
    public class LucidCamera : GH_Component
    {
        // image timeout (milliseconds)
        const UInt32 TIMEOUT = 2000;
        bool previousSnapPhoto = false;
        int snapCount = 0;

        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public LucidCamera()
          : base("Lucid Camera", "Camera",
              "Read from Lucid camera devices",
              "Lucid", "Subcategory")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Snap Photo", "Snap", "Toggle input to take a photo", GH_ParamAccess.item);
            pManager.AddTextParameter("File Path", "Path", "Location of the .PNG file", GH_ParamAccess.item);
            pManager[0].Optional = true;
            pManager[1].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Status", "Status", "Information", GH_ParamAccess.item);
            pManager.AddGenericParameter("Photo (cv::Mat)", "Photo", "The photo as an OpenCV Mat()", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool snapPhoto = false;
            string userfilepath = string.Empty;
            List<string> result = new List<string>();
            ArenaNET.IImage image;
            Mat imageMat;
            int width, height;

            DA.GetData(0, ref snapPhoto);
            DA.GetData(1, ref userfilepath);

            if (previousSnapPhoto == snapPhoto) { return; }
            previousSnapPhoto = snapPhoto;

            var tritonDevices = LucidManager.devices.Where(device => {
                String deviceModelName = ((ArenaNET.IString)device.NodeMap.GetNode("DeviceModelName")).Value;
                return deviceModelName.Contains("TRI") && deviceModelName.Contains("-C");
            }).ToList();

            if (tritonDevices.Count == 0)
            {
                DA.SetData(0, $"{tritonDevices.Count} Triton cameras found.");
                return;
            }

            try
            {
                //if (userfilepath == string.Empty) throw new Exception("required path to a new .PNG");
                if (Path.GetExtension(userfilepath) != ".png") result.Add("expecting .png file type");

                (image, imageMat, width, height) = TakePhoto(tritonDevices[0], out string photoInfo);

                // save file
                if (userfilepath != null && userfilepath != string.Empty)
                {
                    if (!Directory.Exists(Path.GetDirectoryName(userfilepath)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(userfilepath));
                    }
                    image.Bitmap.Save(userfilepath, System.Drawing.Imaging.ImageFormat.Png);
                }

                DA.SetData(1, imageMat);
                result.Add($"capture successful: {userfilepath}");
            }
            catch (Exception ex)
            {
                result.Add($"unsuccessful: {ex.Message}");
            }

            result.Add($"snap: {snapCount++}");
            DA.SetData(0, string.Join("\n", result));
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
            get { return new Guid("d55aa1ae-0ae5-46e3-9fea-83d431a1d889"); }
        }
    }
}
