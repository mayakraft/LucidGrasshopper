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
    public class CameraCorrection : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public CameraCorrection()
          : base("Camera Correction", "Correction",
              "Find a correction matrix which aligns a single point from many scans",
              "Lucid", "Subcategory")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // pManager.AddMatrixParameter("Scan Matrices", "Matrices", "a list of orientation matrices taken at the time of the scan", GH_ParamAccess.list);
            pManager.AddTransformParameter("Scan Matrices", "Matrices", "a list of orientation matrices taken at the time of the scan", GH_ParamAccess.list);
            pManager.AddPointParameter("Reference Points Initial", "Points 0", "a list of reference points, one for each scan, which should lie on top of each other", GH_ParamAccess.list);
            pManager.AddPointParameter("Reference Points Transformed", "Points T", "a list of reference points, one for each scan, which should lie on top of each other", GH_ParamAccess.list);
            
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
            pManager.AddTransformParameter("Solution Matrix", "Result", "The correction matrix to be applied to all future scans", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var info = new List<string>();
            var matrices = new List<Transform>();
            var pointsInitial = new List<Point3d>();
            var pointsTransformed = new List<Point3d>();
            var solution = Transform.Identity;

            DA.GetDataList(0, matrices);
            DA.GetDataList(1, pointsInitial);
            DA.GetDataList(2, pointsTransformed);

            string solveInfo = string.Empty;
            try
            {
                // solution = MatrixSolver.Solve(matrices, points, out var solveInfo);
                // solution = MatrixSolver.Solve(matrices, pointsInitial, pointsTransformed, out var solveInfo);
                solution = CorrectionMatrixSolver.ComputeCorrectionMatrix(matrices, pointsInitial, out solveInfo);
                info.Add(solveInfo);
            }
            catch (Exception error)
            {
                info.Add(solveInfo);
                info.Add(error.Message);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error.ToString());
                DA.AbortComponentSolution();
            }

            DA.SetData(0, string.Join("\n", info));
            DA.SetData(1, solution);
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
            get { return new Guid("c6c3ab16-1ca4-85f9-1fbd-41a789c1d552"); }
        }
    }
}

