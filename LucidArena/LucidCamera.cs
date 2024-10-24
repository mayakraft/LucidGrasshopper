using ArenaNET;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.IO;

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
            pManager.AddNumberParameter("Device Number", "Device", "Leave empty for automatic device selection, or specify device number to override", GH_ParamAccess.item);
            pManager[0].Optional = true;
            pManager[2].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Status", "Status", "Information", GH_ParamAccess.item);
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
            double deviceNumberInput = -1;
            List<string> result = new List<string>();
            DA.GetData(0, ref snapPhoto);
            DA.GetData(1, ref userfilepath);
            DA.GetData(2, ref deviceNumberInput);

            try
            {
                if (userfilepath == string.Empty) throw new Exception("required path to a new .PNG");
                if (LucidManager.devices.Count == 0) throw new Exception("no available devices");
                if (Path.GetExtension(userfilepath) != ".png") result.Add("expecting .png file type");

                var device = deviceNumberInput == -1
                    ? LucidManager.GetTritonDevice()
                    : LucidManager.devices[(int)deviceNumberInput];

                // prepare
                device.StartStream();
                ArenaNET.IImage image = device.GetImage(TIMEOUT);

                // save file
                if (!Directory.Exists(Path.GetDirectoryName(userfilepath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(userfilepath));
                }
                image.Bitmap.Save(userfilepath, System.Drawing.Imaging.ImageFormat.Png);

                // clean up
                device.RequeueBuffer(image);
                device.StopStream();
                result.Add($"capture successful: {userfilepath}");
            }
            catch (Exception ex)
            {
                result.Add($"unsuccessful: {ex.Message}");
            }

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
