using Grasshopper.Kernel.Types.Transforms;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static LucidArena.HeliosDevice;
using System.Drawing;

using Emgu.CV;
using Emgu.CV.Util;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Features2D;
using System.Runtime;
using System.Runtime.InteropServices;
using ArenaNET;
using Rhino.Display;

namespace LucidArena
{
    internal class ColorCloud
    {
        const String TAB1 = "  ";
        const String TAB2 = "    ";

        // Helios RGB: Overlay
        //    This example is part 3 of a 3-part example on color overlay over 3D images.
        //    With the system calibrated, we can now remove the calibration target from
        //    the scene and grab new images with the Helios and Triton cameras, using the
        //    calibration result to find the RGB color for each 3D point measured with
        //    the Helios. Based on the output of solvePnP we can project the 3D points
        //    measured by the Helios onto the RGB camera image using the OpenCV function
        //    projectPoints. Grab a Helios image with the GetHeliosImage()
        //    function(output: xyz_mm) and a Triton RGB image with the
        //    GetTritionRGBImage() function(output: triton_rgb). The following code shows
        //    how to project the Helios xyz points onto the Triton image, giving a(row,
        //    col) position for each 3D point. We can sample the Triton image at
        //    that(row, col) position to find the 3D point's RGB value.

        // =-=-=-=-=-=-=-=-=-
        // =-=- SETTINGS =-=-
        // =-=-=-=-=-=-=-=-=-

        // image timeout
        const UInt32 TIMEOUT = 200;

        // orientation values file name
        const String FILE_NAME_IN = "orientation.yml";

        // file name
        const String FILE_NAME_OUT = "Images\\Cpp_HLTRGB_3_Overlay.ply";

        // =-=-=-=-=-=-=-=-=-
        // =-=- EXAMPLE -=-=-
        // =-=-=-=-=-=-=-=-=-

        public static (List<Point3d> points, List<int> intensities, List<Color4f> colors) CaptureImageAndCloud(ArenaNET.IDevice deviceTRI, ArenaNET.IDevice deviceHLT)
        {
            // get node values that will be changed in order to return their values at
            // the end of the example
            var pixelFormatNodeTRI = (ArenaNET.IEnumeration)deviceTRI.NodeMap.GetNode("PixelFormat");
            String pixelFormatInitialTRI = pixelFormatNodeTRI.Entry.Symbolic;

            var pixelFormatNodeHLT = (ArenaNET.IEnumeration)deviceTRI.NodeMap.GetNode("PixelFormat");
            String pixelFormatInitialHLT = pixelFormatNodeHLT.Entry.Symbolic;

            // Read in camera matrix, distance coefficients, and rotation and translation vectors
            Mat cameraMatrix = new Mat();
            Mat distCoeffs = new Mat();
            Mat rotationVector = new Mat();
            Mat translationVector = new Mat();

            FileStorage fs = new FileStorage(FILE_NAME_IN, FileStorage.Mode.Read);
            fs.GetNode("cameraMatrix").ReadMat(cameraMatrix);
            fs.GetNode("distCoeffs").ReadMat(distCoeffs);
            fs.GetNode("rotationVector").ReadMat(rotationVector);
            fs.GetNode("translationVector").ReadMat(translationVector);

            fs.Dispose();

            // Get an image from Helios2
            Console.WriteLine("{0}Get and prepare HLT image", TAB1);

            ArenaNET.IImage imageHLT = null;
            Mat imageMatrixXYZ = new Mat();
            int width = 0;
            int height = 0;
            double scale = 0;
            double offsetX = 0, offsetY = 0, offsetZ = 0;

            GetImageHLT(deviceHLT, ref imageHLT, ref imageMatrixXYZ, ref width, ref height, ref scale, ref offsetX, ref offsetY, ref offsetZ);
            // prepare info from input buffer
            UInt32 sizeHLT = (UInt32)(width * height);
            UInt32 srcBpp = imageHLT.BitsPerPixel;
            int srcPixelSize = (int)(srcBpp / 8);
            byte[] data = imageHLT.DataArray;

            var (points, intensities) = GetPointCloudUnsigned(data, sizeHLT, srcPixelSize, (float)scale, (float)scale, (float)scale, (float)offsetX, (float)offsetY, (float)offsetZ);

            CvInvoke.Imwrite(FILE_NAME_OUT + "XYZ.jpg", imageMatrixXYZ);

            // Get an image from Triton
            Console.WriteLine("{0}Get and prepare TRI image", TAB1);

            ArenaNET.IImage imageTRI = null;
            Mat imageMatrixRGB = new Mat();
            GetImageTRI(deviceTRI, ref imageTRI, ref imageMatrixRGB);
            CvInvoke.Imwrite(FILE_NAME_OUT + "RGB.jpg", imageMatrixRGB);

            // Overlay RGB color data onto 3D XYZ points
            Console.WriteLine("{0}Overlay the RGB color data onto the 3D XYZ points", TAB1);

            // reshape image matrix
            Console.WriteLine("{0}Reshape XYZ matrix", TAB2);

            int size = imageMatrixXYZ.Rows * imageMatrixXYZ.Cols;
            Mat xyzPoints = imageMatrixXYZ.Reshape(3, size);

            // project points
            Console.WriteLine("{0}Project points", TAB2);

            Mat projectedPointsTRI = new Mat();

            CvInvoke.ProjectPoints(xyzPoints, rotationVector, translationVector, cameraMatrix, distCoeffs, projectedPointsTRI);

            // loop through projected points to access RGB data at those points
            Console.WriteLine("{0}Get values at projected points", TAB2);

            byte[] colorData = new byte[width * height * 3];
            Image<Bgr, byte> rgbImg = imageMatrixRGB.ToImage<Bgr, byte>();

            var colors = new List<Color4f>();

            for (int i = 0; i < width * height; i++)
            {
                Matrix<float> matrixRow = new Matrix<float>(1, 2); // Create a 1x2 matrix for the row
                projectedPointsTRI.Row(i).CopyTo(matrixRow);

                int colTRI = (int)Math.Round(matrixRow[0, 0]);
                int rowTRI = (int)Math.Round(matrixRow[0, 1]);

                // only handle appropriate points
                if (rowTRI < 0 ||
                    colTRI < 0 ||
                    rowTRI >= rgbImg.Rows ||
                    colTRI >= rgbImg.Cols)
                    continue;

                // access corresponding XYZ and RGB data
                byte R = rgbImg.Data[rowTRI, colTRI, 2];
                byte G = rgbImg.Data[rowTRI, colTRI, 1];
                byte B = rgbImg.Data[rowTRI, colTRI, 0];

                // grab RGB data to save colored .ply
                colorData[i * 3 + 0] = B;
                colorData[i * 3 + 1] = G;
                colorData[i * 3 + 2] = R;
                colors.Add(new Color4f(R, G, B, 1.0f));
            }

            // save result
            Console.WriteLine("{0}Save image to {1}", TAB1, FILE_NAME_OUT);

            // prepare to save
            SaveNET.ImageParams parameters = new SaveNET.ImageParams(
                imageHLT.Width,
                imageHLT.Height,
                imageHLT.BitsPerPixel,
                true);

            SaveNET.ImageWriter plyWriter = new SaveNET.ImageWriter(parameters, FILE_NAME_OUT);

            // save .ply with color data
            bool filterPoints = true;
            bool isSignedPixelFormat = false;

            plyWriter.SetPly(".ply", filterPoints, isSignedPixelFormat, (float)scale, (float)offsetX, (float)offsetY, (float)offsetZ);

            plyWriter.Save(imageHLT.DataArray, colorData);

            // return nodes to their initial values
            pixelFormatNodeTRI.FromString(pixelFormatInitialTRI);
            pixelFormatNodeHLT.FromString(pixelFormatInitialHLT);

            // =-=-=-=-=-=-=-=-=-
            // =-  CLEAN UP  =-=-
            // =-=-=-=-=-=-=-=-=-

            //if (deviceTRI != null)
            //{
            //    system.DestroyDevice(deviceTRI);
            //}

            //if (deviceHLT != null)
            //{
            //    system.DestroyDevice(deviceHLT);
            //}
            return (points, intensities, colors);
        }

        static void GetImageTRI(ArenaNET.IDevice deviceTRI, ref ArenaNET.IImage ppOutImage, ref Mat tritonRGB)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                var pixelFormatNodeTRI = (ArenaNET.IEnumeration)deviceTRI.NodeMap.GetNode("PixelFormat");
                pixelFormatNodeTRI.FromString("RGB8");
            }
            else if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                var pixelFormatNodeTRI = (ArenaNET.IEnumeration)deviceTRI.NodeMap.GetNode("PixelFormat");
                pixelFormatNodeTRI.FromString("BGR8");
            }

            deviceTRI.StartStream();
            ArenaNET.IImage image = deviceTRI.GetImage(2000);

            // copy image because original will be deleted after function call
            ArenaNET.IImage copyImage = ArenaNET.ImageFactory.Copy(image);
            ppOutImage = copyImage;

            Int32 width = (int)image.Width;
            Int32 height = (int)image.Height;

            tritonRGB = new Mat(height, width, DepthType.Cv8U, 3);

            Marshal.Copy(image.DataArray, 0, tritonRGB.DataPointer, height * width * 3);

            // clean up
            deviceTRI.RequeueBuffer(image);
            deviceTRI.StopStream();
        }

        static void GetImageHLT(ArenaNET.IDevice deviceHLT, ref ArenaNET.IImage ppOutImage, ref Mat xyzMm, ref int width, ref int height, ref double xyzScaleMm, ref double xOffsetMm, ref double yOffsetMm, ref double zOffsetMm)
        {
            // Read the scale factor and offsets to convert from unsigned 16-bit values
            //    in the Coord3D_ABCY16 pixel format to coordinates in mm
            var scan3dCoordinateScaleNode = (ArenaNET.IFloat)deviceHLT.NodeMap.GetNode("Scan3dCoordinateScale");
            xyzScaleMm = scan3dCoordinateScaleNode.Value;

            var scan3dCoordinateSelectorNodeA = (ArenaNET.IEnumeration)deviceHLT.NodeMap.GetNode("Scan3dCoordinateSelector");
            scan3dCoordinateSelectorNodeA.FromString("CoordinateA");

            var scan3dCoordinateOffsetNodeA = (ArenaNET.IFloat)deviceHLT.NodeMap.GetNode("Scan3dCoordinateOffset");
            xOffsetMm = scan3dCoordinateOffsetNodeA.Value;

            var scan3dCoordinateSelectorNodeB = (ArenaNET.IEnumeration)deviceHLT.NodeMap.GetNode("Scan3dCoordinateSelector");
            scan3dCoordinateSelectorNodeB.FromString("CoordinateB");

            var scan3dCoordinateOffsetNodeB = (ArenaNET.IFloat)deviceHLT.NodeMap.GetNode("Scan3dCoordinateOffset");
            yOffsetMm = scan3dCoordinateOffsetNodeB.Value;

            var scan3dCoordinateSelectorNodeC = (ArenaNET.IEnumeration)deviceHLT.NodeMap.GetNode("Scan3dCoordinateSelector");
            scan3dCoordinateSelectorNodeC.FromString("CoordinateC");

            var scan3dCoordinateOffsetNodeZ = (ArenaNET.IFloat)deviceHLT.NodeMap.GetNode("Scan3dCoordinateOffset");
            zOffsetMm = scan3dCoordinateOffsetNodeZ.Value;

            // start stream
            deviceHLT.StartStream();
            ArenaNET.IImage image = deviceHLT.GetImage(2000);

            // copy image because original will be deleted after function call
            ArenaNET.IImage copyImage = ArenaNET.ImageFactory.Copy(image);
            ppOutImage = copyImage;


            width = (int)image.Width;
            height = (int)image.Height;

            xyzMm = new Mat(height, width, DepthType.Cv32F, 3);

            byte[] inputDataBytes = image.DataArray;
            UInt16[] inputData = new UInt16[inputDataBytes.Length / 2];
            Buffer.BlockCopy(inputDataBytes, 0, inputData, 0, inputDataBytes.Length);

            int dataIndex = 0;

            for (Int32 ir = 0; ir < height; ++ir)
            {
                for (Int32 ic = 0; ic < width; ++ic)
                {
                    // Get unsigned 16 bit values for X,Y,Z coordinates
                    ushort xU16 = inputData[dataIndex + 0];
                    ushort yU16 = inputData[dataIndex + 1];
                    ushort zU16 = inputData[dataIndex + 2];

                    // Convert 16-bit X, Y, Z to float values in mm
                    MCvScalar pixelValue = new MCvScalar(
                       (float)(xU16 * xyzScaleMm + xOffsetMm),
                       (float)(yU16 * xyzScaleMm + yOffsetMm),
                       (float)(zU16 * xyzScaleMm + zOffsetMm)
                    );

                    xyzMm.Row(ir).Col(ic).SetTo(pixelValue);

                    dataIndex += 4;
                }
            }

            deviceHLT.RequeueBuffer(image);
            deviceHLT.StopStream();
        }
    }
}