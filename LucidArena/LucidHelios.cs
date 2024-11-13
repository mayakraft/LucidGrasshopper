using ArenaNET;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Linq;
using System.Collections.Generic;
using Rhino.Commands;
using Rhino.Display;
using static LucidArena.HeliosDevice;

// In order to load the result of this wizard, you will also need to
// add the output bin/ folder of this project to the list of loaded
// folder in Grasshopper.
// You can use the _GrasshopperDeveloperSettings Rhino command for that.

namespace LucidArena
{
    public class LucidHelios : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public LucidHelios()
          : base("Lucid Helios", "Helios",
              "Capture raw 3D data from a Helios camera",
              "Lucid", "Subcategory")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Snap Cloud", "Snap", "Toggle input to capture the point cloud", GH_ParamAccess.item);
            pManager.AddNumberParameter("Filter Confidence", "Filter by Confidence", "Filter points based on the sensor confidence", GH_ParamAccess.item);

            pManager.AddIntervalParameter("Filter X Range", "X Range", "Filter to include only points within this range", GH_ParamAccess.item);
            pManager.AddIntervalParameter("Filter Y Range", "Y Range", "Filter to include only points within this range", GH_ParamAccess.item);
            pManager.AddIntervalParameter("Filter Z Range", "Z Range", "Filter to include only points within this range", GH_ParamAccess.item);
            
            pManager.AddTextParameter("Exposure Mode", "Exposure", "Set the exposure mode", GH_ParamAccess.item);
            pManager.AddTextParameter("Conversion Gain", "Gain", "Set the conversion gain", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Image Accumulation", "Accumulation", "Set the image accumulation", GH_ParamAccess.item);
            
            pManager.AddBooleanParameter("Spacial Filtering", "Filtering", "Activate spacial filtering", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Confidence Threshold", "Confidence", "Activate confidence threshold", GH_ParamAccess.item);
            
            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
            pManager[7].Optional = true;
            pManager[8].Optional = true;
            pManager[9].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Status", "Status", "Device Information", GH_ParamAccess.item);
            //pManager.AddPointParameter("Points", "Points", "List of points composing the point cloud", GH_ParamAccess.list);
            //pManager.AddNumberParameter("Intensity", "Intensity", "List of intensities for each point", GH_ParamAccess.list);
            pManager.AddGeometryParameter("Point Cloud", "Cloud", "A Rhino Geometry Point Cloud with points, no colors", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var snapPhoto = false;
            var confidence = 0.0;
            var xInterval = Interval.Unset;
            var yInterval = Interval.Unset;
            var zInterval = Interval.Unset;
            var exposureTime = string.Empty;
            var conversionGain = string.Empty;
            var imageAccumulation = -1;
            var spatialFilter = true;
            var confidenceThreshold = true;

            var info = new List<string>();
            var points = new List<Point3d>();
            var intensities = new List<int>();
            var cloud = new PointCloud();

            DA.GetData(0, ref snapPhoto);
            DA.GetData(1, ref confidence);

            DA.GetData(2, ref xInterval);
            DA.GetData(3, ref yInterval);
            DA.GetData(4, ref zInterval);

            DA.GetData(5, ref exposureTime);
            DA.GetData(6, ref conversionGain);
            DA.GetData(7, ref imageAccumulation);
            DA.GetData(8, ref spatialFilter);
            DA.GetData(9, ref confidenceThreshold);

            var settings = new HeliosSettings();
            if (exposureTime != string.Empty) { settings.exposureTime = exposureTime; }
            if (conversionGain != string.Empty) { settings.conversionGain = conversionGain; }
            if (imageAccumulation != -1) { settings.imageAccumulation = imageAccumulation; }
            settings.spatialFilter = spatialFilter;
            settings.confidenceThreshold = confidenceThreshold;

            var heliosDevices = LucidManager.devices.Where(device => {
                String deviceModelName = ((ArenaNET.IString)device.NodeMap.GetNode("DeviceModelName")).Value;
                return deviceModelName.StartsWith("HLT") || deviceModelName.StartsWith("HTP");
            }).ToList();

            if (heliosDevices.Count == 0)
            {
                DA.SetData(0, "No Helios depth camera found.");
                DA.SetData(1, cloud);
                return;
            }

            try
            {
                (points, intensities) = HeliosDevice.GetPointCloud(heliosDevices[0], settings);
            }
            catch (Exception error)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error.ToString());
                DA.AbortComponentSolution();
            }

            if (confidence != 0.0)
            {
                points = points
                    .Where((point, i) => intensities[i] > confidence)
                    .ToList();
            }

            // if the interval is valid, the user has connected an interval to the component input
            // filter the output data.
            if (xInterval.IsValid)
            {
                //intensities = intensities.Where((_, i) => xInterval.IncludesParameter(points[i].X)).ToList();
                points = points.Where(point => xInterval.IncludesParameter(point.X)).ToList();
            }
            if (yInterval.IsValid)
            {
                //intensities = intensities.Where((_, i) => yInterval.IncludesParameter(points[i].Y)).ToList();
                points = points.Where(point => yInterval.IncludesParameter(point.Y)).ToList();
            }
            if (zInterval.IsValid)
            {
                //intensities = intensities.Where((_, i) => zInterval.IncludesParameter(points[i].Z)).ToList();
                points = points.Where(point => zInterval.IncludesParameter(point.Z)).ToList();
            }

            cloud.AddRange(points);

            DA.SetData(0, $"{settings}");
            //DA.SetDataList(1, points);
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
            get { return new Guid("fbc39a3c-0ae5-46e3-9fea-83d431a1d889"); }
        }
    }
}
