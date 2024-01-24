using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LucidArena
{
    public class LucidConnect : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public LucidConnect()
          : base("Lucid Connect", "Connect",
            "Connect to a Lucid Camera Device",
            "Lucid", "Subcategory")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Active", "Active", "Allow the system to connect to devices (default: false)", GH_ParamAccess.item);
            // pManager.AddNumberParameter("Device Number", "Device", "Which camera device are we reading from?", GH_ParamAccess.item);
            pManager[0].Optional = true;
            // pManager[1].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Status", "Status", "System Connection Information", GH_ParamAccess.item);
            pManager.AddTextParameter("Devices", "Devices", "List of available devices", GH_ParamAccess.list);
        }

        private string TryInit()
        {
            if (LucidManager.SystemIsOpen) return "API connection already established";
            if (LucidManager.OpenSystem()) return "API connection successful";
            return "API connection unsuccessful";
        }

        private string TryDeinit()
        {
            string result = LucidManager.SystemIsOpen ? "API is now closed" : "API is already closed";
            LucidManager.CloseSystem();
            return result;
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool active = false;
            DA.GetData(0, ref active);
            List<string> messages = new List<string>();
            List<string> deviceNames = new List<string>();
            try
            {
                messages.Add(active ? TryInit() : TryDeinit());
                messages.Add($"Connected to {LucidManager.ConnectAllDevices()} devices");
                deviceNames = LucidManager.devices
                    .Select(device => ((ArenaNET.IString)device.NodeMap.GetNode("DeviceModelName")).Value)
                    .ToList();
            }
            catch (Exception error)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error.ToString());
                DA.AbortComponentSolution();
            }
            DA.SetData(0, string.Join("\n", messages));
            DA.SetDataList(1, deviceNames);
        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon => null;

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("a9d9aaa8-a9fb-48bb-8fbd-6d18ac3fc477");
    }
}