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

namespace LucidArena
{
    internal class AlignCameras
    {
        const String TAB1 = "  ";
        const String TAB2 = "    ";
        const String TAB3 = "      ";

        // Helios RGB: Orientation
        //    This example is part 2 of a 3-part example on color overlay over 3D images.
        //    Color data can be overlaid over 3D images by reading the
        //    3D points ABC (XYZ) from the Helios and projecting them onto
        //    the Triton color (RGB) camera directly. This requires first solving for the
        //    orientation of the Helios coordinate system relative to the Triton's
        //    native coordinate space (rotation and translation wise). This step can be
        //    achieved by using the open function solvePnP(). Solving for orientation of
        //    the Helios relative to the Triton requires a single image of the
        //    calibration target from each camera. Place the calibration target near the
        //    center of both cameras field of view and at an appropriate distance from
        //    the cameras. Make sure the calibration target is placed at the same
        //    distance you will be imaging in your application. Make sure not to move the
        //    calibration target or cameras in between grabbing the Helios image and
        //    grabbing the Triton image.

        // =-=-=-=-=-=-=-=-=-
        // =-=- SETTINGS =-=-
        // =-=-=-=-=-=-=-=-=-

        // image timeout
        const UInt32 TIMEOUT = 2000;

        // calibration value file name
        const String FILE_NAME_IN = "tritoncalibration.yml";

        // orentation values file name
        const String FILE_NAME_OUT = "orientation.yml";

        // =-=-=-=-=-=-=-=-=-
        // =-=- EXAMPLE -=-=-
        // =-=-=-=-=-=-=-=-=-

        // calculates and saves calibration values
        public static (Array, Array) CalculateAndSaveOrientationValues(ArenaNET.IDevice deviceTRI, ArenaNET.IDevice deviceHLT)
        {
            // get node values that will be changed in order to return their values at
            // the end of the example
            var pixelFormatNodeTRI = (ArenaNET.IEnumeration)deviceTRI.NodeMap.GetNode("PixelFormat");
            String pixelFormatInitialTRI = pixelFormatNodeTRI.Entry.Symbolic;

            var pixelFormatNodeHLT = (ArenaNET.IEnumeration)deviceTRI.NodeMap.GetNode("PixelFormat");
            String pixelFormatInitialHLT = pixelFormatNodeHLT.Entry.Symbolic;

            // Read in camera matrix and distance coefficients
            Console.WriteLine("{0}Read camera matrix and distance coefficients from file '{1}'", TAB1, FILE_NAME_IN);

            FileStorage fs = new FileStorage(FILE_NAME_IN, FileStorage.Mode.Read);
            Mat cameraMatrix = new Mat();
            Mat distCoeffs = new Mat();

            fs.GetNode("cameraMatrix").ReadMat(cameraMatrix);
            fs.GetNode("distCoeffs").ReadMat(distCoeffs);

            fs.Dispose();

            // Get an image from Helios2
            Console.WriteLine("{0}Get and prepare HLT image", TAB1);
            Mat imageMatrixHLTIntensity = new Mat();
            Mat imageMatrixHLTXYZ = new Mat();
            GetImageHLT(deviceHLT, ref imageMatrixHLTIntensity, ref imageMatrixHLTXYZ);

            // Get an image from Triton
            Console.WriteLine("{0}Get and prepare TRI iage", TAB1);
            Mat imageMatrixTRI = new Mat();
            GetImageTRI(deviceTRI, ref imageMatrixTRI);

            // Calculate orientation values
            Console.WriteLine("{0}Calculate orientation values", TAB1);
            VectorOfPointF gridCentersHLT = new VectorOfPointF();
            VectorOfPointF gridCentersTRI = new VectorOfPointF();

            // find HLT calibration points using HLT intensity image
            Console.WriteLine("{0}Find points in HLT image", TAB2);
            FindCalibrationPointsHLT(imageMatrixHLTIntensity, gridCentersHLT);

            if (gridCentersHLT.Size != 20)
            {
                throw new InvalidOperationException("Unable to find points in HLT intensity image");
            }

            // find TRI calibration points
            Console.WriteLine("{0}Find points in TRI image", TAB2);
            FindCalibrationPointsTRI(imageMatrixTRI, gridCentersTRI);

            if (gridCentersTRI.Size != 20)
            {
                throw new InvalidOperationException("Unable to find points in TRI image");
            }

            // prepare for PnP
            Console.WriteLine("{0}Prepare for PnP", TAB2);

            VectorOfPoint3D32F targetPoints3Dmm = new VectorOfPoint3D32F();
            VectorOfPointF targetPoints3DPixels = new VectorOfPointF();
            VectorOfPointF targetPoints2DPixels = new VectorOfPointF();

            for (int i = 0; i < gridCentersTRI.Size; i++)
            {
                UInt32 c1 = (UInt32)Math.Round(gridCentersHLT[i].X);
                UInt32 r1 = (UInt32)Math.Round(gridCentersHLT[i].Y);
                UInt32 c2 = (UInt32)Math.Round(gridCentersTRI[i].X);
                UInt32 r2 = (UInt32)Math.Round(gridCentersTRI[i].Y);

                float[,,] data = (float[,,])imageMatrixHLTXYZ.GetData();

                float x = data[r1, c1, 0];
                float y = data[r1, c1, 1];
                float z = data[r1, c1, 2];

                MCvPoint3D32f pt = new MCvPoint3D32f(x, y, z);
                Console.WriteLine("{0}Point {1}: [{2}, {3}, {4}]", TAB3, i, pt.X, pt.Y, pt.Z);

                targetPoints3Dmm.Push(new MCvPoint3D32f[] { pt });
                targetPoints3DPixels.Push(new PointF[] { gridCentersHLT[i] });
                targetPoints2DPixels.Push(new PointF[] { gridCentersTRI[i] });
            }

            if (cameraMatrix.IsEmpty || distCoeffs.IsEmpty)
            {
                throw new InvalidOperationException("Camera matrix or distortion coefficients are not properly initialized.");
            }

            if (targetPoints3Dmm.Size == 0 || targetPoints2DPixels.Size == 0)
            {
                throw new InvalidOperationException("3D points or 2D points are not properly initialized.");
            }

            Mat rotationVector = new Mat();
            Mat translationVector = new Mat();
            bool orientationSucceeded = CvInvoke.SolvePnP(targetPoints3Dmm, targetPoints2DPixels, cameraMatrix, distCoeffs, rotationVector, translationVector);

            Console.WriteLine($"{TAB2}Orientation {(orientationSucceeded ? "succeeded" : "failed")}");

            // Save orientation information
            Console.WriteLine($"{TAB1}Save camera matrix, distance coefficients, and rotation and translation vectors to file '{FILE_NAME_OUT}'");

            FileStorage fs2 = new FileStorage(FILE_NAME_OUT, FileStorage.Mode.Write);
            fs2.Write(cameraMatrix, "cameraMatrix");
            fs2.Write(distCoeffs, "distCoeffs");
            fs2.Write(rotationVector, "rotationVector");
            fs2.Write(translationVector, "translationVector");

            fs2.Dispose();

            // return nodes to their initial values
            pixelFormatNodeTRI.FromString(pixelFormatInitialTRI);
            pixelFormatNodeHLT.FromString(pixelFormatInitialHLT);

            return (translationVector.GetData(), rotationVector.GetData());

            // =-=-=-=-=-=-=-=-=-
            // =-  CLEAN UP  =-=-
            // =-=-=-=-=-=-=-=-=-

            // destroy device
            //system.DestroyDevice(deviceTRI);
            //system.DestroyDevice(deviceHLT);
        }

        static bool FindCalibrationPointsTRI(Mat imageInOrig, VectorOfPointF gridCenters)
        {
            float scaling = 1.0f;
            Mat imageIn = imageInOrig;

            /*ArenaView*/
            SimpleBlobDetectorParams brightParams = new SimpleBlobDetectorParams
            {
                FilterByColor = true,
                blobColor = 255,  // white circles in the calibration target
                FilterByCircularity = true,
                MinCircularity = 0.8f
            };

            SimpleBlobDetector blobDetector = new SimpleBlobDetector(brightParams);

            // pattern_size(num_cols, num_rows) num_cols: number of columns (number of
            // circles in a row) of the calibration target viewed by the camera num_rows:
            // number of rows (number of circles in a column) of the calibration target
            // viewed by the camera Specify according to the orientation of the
            // calibration target
            Size patternSize = new Size(5, 4);

            bool isFound = CvInvoke.FindCirclesGrid(imageIn, patternSize, gridCenters, CalibCgType.SymmetricGrid, blobDetector);

            double scaledNRows = 2400.0;
            while (!isFound && scaledNRows >= 100)
            {
                scaledNRows /= 2.0;
                scaling = (float)((double)imageInOrig.Rows / scaledNRows);
                CvInvoke.Resize(imageInOrig, imageIn, new Size((Int32)((double)imageInOrig.Cols / scaling), (Int32)((double)imageInOrig.Rows / scaling)));
                isFound = CvInvoke.FindCirclesGrid(imageIn, patternSize, gridCenters, CalibCgType.SymmetricGrid, blobDetector);
                Console.WriteLine("{0}Found {1} circle centers.", TAB2, gridCenters.Size);
            }

            // Scale back the grid centers
            VectorOfPointF newCenters = new VectorOfPointF();
            for (int i = 0; i < gridCenters.Size; i++)
            {
                PointF scaledPoint = gridCenters[i];
                scaledPoint.X *= scaling;
                scaledPoint.Y *= scaling;
                newCenters.Push(new PointF[] { scaledPoint });
            }
            gridCenters.Clear();
            gridCenters.Push(newCenters.ToArray());
            return isFound;
        }

        static void FindCalibrationPointsHLT(Mat imageIn, VectorOfPointF gridCenters)
        {
            SimpleBlobDetectorParams brightParams = new SimpleBlobDetectorParams
            {
                FilterByColor = true,
                blobColor = 255,  // white circles in the calibration target
                ThresholdStep = 2,
                MinArea = 10.0f,
                MaxArea = 1000.0f
            };
            SimpleBlobDetector blobDetector = new SimpleBlobDetector(brightParams);

            // pattern_size(num_cols, num_rows) num_cols: number of columns (number of
            // circles in a row) of the calibration target viewed by the camera num_rows:
            // number of rows (number of circles in a column) of the calibration target
            // viewed by the camera Specify according to the orientation of the
            // calibration target
            Size patternSize = new Size(5, 4);

            // Find max value in input image
            double minValue = 0;
            double maxValue = 0;
            System.Drawing.Point minLoc = new System.Drawing.Point();
            System.Drawing.Point maxLoc = new System.Drawing.Point();
            CvInvoke.MinMaxLoc(imageIn, ref minValue, ref maxValue, ref minLoc, ref maxLoc);

            // Scale image to 8-bit, using full 8-bit range
            Mat image8Bit = new Mat();
            imageIn.ConvertTo(image8Bit, DepthType.Cv8U, 255.0 / maxValue);

            CvInvoke.FindCirclesGrid(image8Bit, patternSize, gridCenters, CalibCgType.SymmetricGrid, blobDetector);
        }

        static void GetImageTRI(ArenaNET.IDevice deviceTRI, ref Mat tritonImage)
        {
            var pixelFormatNodeTRI = (ArenaNET.IEnumeration)deviceTRI.NodeMap.GetNode("PixelFormat");
            pixelFormatNodeTRI.FromString("RGB8");

            // enable stream auto negotiate packet size
            var streamAutoNegotiatePacketSizeNode = (ArenaNET.IBoolean)deviceTRI.TLStreamNodeMap.GetNode("StreamAutoNegotiatePacketSize");
            streamAutoNegotiatePacketSizeNode.Value = true;

            // enable stream packet resend
            var streamPacketResendEnableNode = (ArenaNET.IBoolean)deviceTRI.TLStreamNodeMap.GetNode("StreamPacketResendEnable");
            streamPacketResendEnableNode.Value = true;

            // start stream
            deviceTRI.StartStream();
            ArenaNET.IImage image = deviceTRI.GetImage(TIMEOUT);

            // convert Triton image to mono for dot finding
            ArenaNET.IImage convert = ArenaNET.ImageFactory.Convert(image, ArenaNET.EPfncFormat.Mono8);
            Int32 width = (int)image.Width;
            Int32 height = (int)image.Height;
            Size patternSize = new Size(5, 4);
            tritonImage = new Mat(height, width, DepthType.Cv8U, 1);

            Marshal.Copy(convert.DataArray, 0, tritonImage.DataPointer, width * height);

            // clean up
            ArenaNET.ImageFactory.Destroy(convert);
            deviceTRI.RequeueBuffer(image);
            deviceTRI.StopStream();
        }

        static void GetImageHLT(ArenaNET.IDevice deviceHLT, ref Mat intensityImage, ref Mat xyzMm)
        {
            var pixelFormatNode = (ArenaNET.IEnumeration)deviceHLT.NodeMap.GetNode("PixelFormat");
            pixelFormatNode.FromString("Coord3D_ABCY16");

            // enable stream auto negotiate packet size
            var streamAutoNegotiatePacketSizeNode = (ArenaNET.IBoolean)deviceHLT.TLStreamNodeMap.GetNode("StreamAutoNegotiatePacketSize");
            streamAutoNegotiatePacketSizeNode.Value = true;

            // enable stream packet resend
            var streamPacketResendEnableNode = (ArenaNET.IBoolean)deviceHLT.TLStreamNodeMap.GetNode("StreamPacketResendEnable");
            streamPacketResendEnableNode.Value = true;

            // Read the scale factor and offsets to convert from unsigned 16-bit values
            //    in the Coord3D_ABCY16 pixel format to coordinates in mm
            var scan3dCoordinateScaleNode = (ArenaNET.IFloat)deviceHLT.NodeMap.GetNode("Scan3dCoordinateScale");
            double xyzScaleMm = scan3dCoordinateScaleNode.Value;

            var scan3dCoordinateSelectorNodeA = (ArenaNET.IEnumeration)deviceHLT.NodeMap.GetNode("Scan3dCoordinateSelector");
            scan3dCoordinateSelectorNodeA.FromString("CoordinateA");

            var scan3dCoordinateOffsetNodeA = (ArenaNET.IFloat)deviceHLT.NodeMap.GetNode("Scan3dCoordinateOffset");
            double xOffsetMm = scan3dCoordinateOffsetNodeA.Value;

            var scan3dCoordinateSelectorNodeB = (ArenaNET.IEnumeration)deviceHLT.NodeMap.GetNode("Scan3dCoordinateSelector");
            scan3dCoordinateSelectorNodeB.FromString("CoordinateB");

            var scan3dCoordinateOffsetNodeB = (ArenaNET.IFloat)deviceHLT.NodeMap.GetNode("Scan3dCoordinateOffset");
            double yOffsetMm = scan3dCoordinateOffsetNodeB.Value;

            var scan3dCoordinateSelectorNodeC = (ArenaNET.IEnumeration)deviceHLT.NodeMap.GetNode("Scan3dCoordinateSelector");
            scan3dCoordinateSelectorNodeC.FromString("CoordinateC");

            var scan3dCoordinateOffsetNodeZ = (ArenaNET.IFloat)deviceHLT.NodeMap.GetNode("Scan3dCoordinateOffset");
            double zOffsetMm = scan3dCoordinateOffsetNodeZ.Value;

            // start stream
            deviceHLT.StartStream();
            ArenaNET.IImage image = deviceHLT.GetImage(TIMEOUT);

            //    Wikipedia: https:en.wikipedia.org/wiki/HSL_and_HSV#From_HSV
            Int32 width = (int)image.Width;
            Int32 height = (int)image.Height;

            xyzMm = new Mat(height, width, DepthType.Cv32F, 3);
            intensityImage = new Mat(height, width, DepthType.Cv16U, 1);

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

                    MCvScalar intensityValue = new MCvScalar(inputData[dataIndex + 3]);
                    intensityImage.Row(ir).Col(ic).SetTo(intensityValue);  // Intensity value

                    dataIndex += 4;
                }
            }

            deviceHLT.RequeueBuffer(image);
            deviceHLT.StopStream();
        }
    }
}