using System;
using System.Collections.Generic;
using System.Threading;
using System.Drawing;

using Emgu.CV;
using Emgu.CV.Util;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Features2D;
using System.Runtime;
using System.Runtime.InteropServices;

namespace LucidArena
{
    internal class TritonDevice
    {

        // image timeout
        const UInt32 TIMEOUT = 200;

        // number of calibration images to compare
        const UInt32 NUM_IMAGES = 10;

        //// calibration value file name
        //const String FILE_NAME = "tritoncalibration.yml";

        // time to sleep between images (in milliseconds)
        const Int32 SLEEP_MS = 1000;

        public class SETTINGS
        {
            public enum Pattern
            {
                NOT_EXISTING,
                CHESSBOARD,
                CIRCLES_GRID,
                ASYMMETRIC_CIRCLES_GRID
            };
            public enum InputType
            {
                INVALID,
                CAMERA,
                VIDEO_FILE,
                IMAGE_LIST
            };
            public Size BoardSize; // The size of the board -> Number of items by width and height
            public Pattern CalibrationPattern { get; set; } // One of the Chessboard, circles, or asymmetric circle pattern
            public float SquareSize; // The size of a square in your defined unit (point, millimeter,etc).
            public UInt32 NrFrames; // The number of frames to use from the input for calibration
            public float AspectRatio { get; set; } // The aspect ratio
            public int Delay { get; set; } // In case of a video input
            public bool WritePoints { get; set; } // Write detected feature points
            public bool WriteExtrinsics { get; set; }  // Write extrinsic parameters
            public bool CalibZeroTangentDist { get; set; } // Assume zero tangential distortion
            public bool CalibFixPrincipalPoint { get; set; } // Fix the principal point at the center
            public bool FlipVertical { get; set; } // Flip the captured images around the horizontal axis
            public string OutputFileName { get; set; } // The name of the file where to write
            public bool ShowUndistorsed { get; set; } // Show undistorted images after calibration
            public string Input { get; set; }  // The input ->
            public bool useFisheye = false; // use fisheye camera model for calibration
            public bool FixK1 { get; set; } // Fix K1 distortion coefficient
            public bool FixK2 { get; set; } // Fix K2 distortion coefficient
            public bool FixK3 { get; set; } // Fix K3 distortion coefficient
            public bool FixK4 { get; set; } // Fix K4 distortion coefficient
            public bool FixK5 { get; set; } // Fix K5 distortion coefficient

            public UInt32 CameraID { get; set; }
            public List<string> ImageList { get; set; } = new List<string>();
            public UInt32 AtImageList { get; set; }
            public VideoCapture InputCapture { get; set; }
            public InputType Input_Type;
            public bool GoodInput;
            public UInt32 Flag { get; set; }
            private string PatternToUse { get; set; }

            public SETTINGS()
            {
                GoodInput = false;
            }
        }

        // calculates and saves calibration values, first the "camera matrix", second the "distance coefficients"
        public static (Mat, Mat) CalculateAndSaveCalibrationValues(ArenaNET.IDevice device, out string info)
        {
            info = string.Empty;
            // get node values that will be changed in order to return their values at
            // the end of the example
            var acquisitionModeNode = (ArenaNET.IEnumeration)device.NodeMap.GetNode("AcquisitionMode");
            String acquisitionModeInitial = acquisitionModeNode.Entry.Symbolic;
            var pixelFormatNode = (ArenaNET.IEnumeration)device.NodeMap.GetNode("PixelFormat");
            String pixelFormatInitial = pixelFormatNode.Entry.Symbolic;

            // enable stream auto negotiate packet size
            var streamAutoNegotiatePacketSizeNode = (ArenaNET.IBoolean)device.TLStreamNodeMap.GetNode("StreamAutoNegotiatePacketSize");
            streamAutoNegotiatePacketSizeNode.Value = true;

            // enable stream packet resend
            var streamPacketResendEnableNode = (ArenaNET.IBoolean)device.TLStreamNodeMap.GetNode("StreamPacketResendEnable");
            streamPacketResendEnableNode.Value = true;

            // set pixel format
            Console.WriteLine("Set pixel format to 'Mono8'");
            pixelFormatNode.FromString("Mono8");

            // set acquisition mode
            Console.WriteLine("Set acquisition mode to 'Continuous'");
            acquisitionModeNode.FromString("Continuous");

            // set buffer handling mode
            Console.WriteLine("Set buffer handling mode to 'NewestOnly'");
            var bufferHandlingModeNode = (ArenaNET.IEnumeration)device.TLStreamNodeMap.GetNode("StreamBufferHandlingMode");
            bufferHandlingModeNode.FromString("NewestOnly");

            // start stream
            device.StartStream();

            // get sets of calibration points
            Console.WriteLine("Getting {0} sets of calibration points", NUM_IMAGES);
            Console.WriteLine("Move the calibration target around the frame for best results");

            Size patternSize = new Size(5, 4);
            VectorOfVectorOfPointF calibrationPoints = new VectorOfVectorOfPointF();
            Size imageSize = new Size();
            UInt32 attempts = 0;
            UInt32 images = 0;
            Int32 gridCentersFound = 0;
            UInt32 successes = 0;

            while (successes < NUM_IMAGES)
            {
                ArenaNET.IImage image = null;
                try
                {
                    // get image
                    attempts++;
                    image = device.GetImage(TIMEOUT);
                    images++;
                    if (image == null)
                        throw new Exception("Incomplete image");
                    Int32 width = (int)image.Width;
                    Int32 height = (int)image.Height;
                    imageSize.Width = width;
                    imageSize.Height = height;

                    // copy data into an OpenCV matrix
                    Mat imageMatrix = new Mat(imageSize.Height, imageSize.Width, DepthType.Cv8U, 1);
                    imageMatrix.SetTo(new Gray(0).MCvScalar);
                    Byte[] imageData = image.DataArray;
                    Marshal.Copy(imageData, 0, imageMatrix.DataPointer, width * height);

                    device.RequeueBuffer(image);

                    // find calibration circles
                    VectorOfPointF gridCenters = new VectorOfPointF();
                    FindCalibrationPoints(imageMatrix, gridCenters);
                    gridCentersFound = gridCenters.Size;
                    if (gridCentersFound == 20)
                    {
                        calibrationPoints.Push(gridCenters);
                        successes++;
                    }
                }
                catch
                {
                    // on failure, ignore and retry
                }

                Console.Write("Attempt {0}: {1} images, {2} circles found, {3} calibration points\r", attempts, images, gridCentersFound, successes);

                // sleep between images
                Thread.Sleep(SLEEP_MS);
            }

            // calculate camera matrix and distance coefficients
            Console.WriteLine("\nCalculating camera matrix and distance coefficients");
            Mat cameraMatrix = new Mat();
            Mat distCoeffs = new Mat();
            SETTINGS s = new SETTINGS();
            s.NrFrames = NUM_IMAGES;
            s.Input_Type = SETTINGS.InputType.IMAGE_LIST;
            s.CalibrationPattern = SETTINGS.Pattern.CIRCLES_GRID;
            s.Flag = ((int)CalibType.RationalModel);
            List<Mat> rvecs = new List<Mat>();
            List<Mat> tvecs = new List<Mat>();
            List<float> reprojErrs = new List<float>();
            double totalAvgErr = 0;
            bool calculationSucceeded = Calculate(s, imageSize, out cameraMatrix, out distCoeffs, calibrationPoints, rvecs, tvecs, reprojErrs, out totalAvgErr);

            info += $"success {calculationSucceeded}\n";
            info += $"total average error {totalAvgErr}\n";

            device.StopStream();

            pixelFormatNode.FromString(pixelFormatInitial);
            acquisitionModeNode.FromString(acquisitionModeInitial);
            return (cameraMatrix, distCoeffs);
        }

        static bool Calculate(SETTINGS s, Size imageSize, out Mat cameraMatrix, out Mat distCoeffs, VectorOfVectorOfPointF imagePoints, List<Mat> rvecs, List<Mat> tvecs, List<float> reprojErrs, out double totalAvgErr)
        {
            // ! [fixed_aspect]
            cameraMatrix = Mat.Eye(3, 3, DepthType.Cv64F, 1);

            if ((s.Flag & (UInt32)CalibType.FixAspectRatio) != 0)
            {
                Mat diagonalElement = cameraMatrix.Row(0).Col(0);
                diagonalElement.SetTo(new MCvScalar(s.AspectRatio));
            }

            // ! [fixed_aspect]
            if (s.useFisheye)
            {
                distCoeffs = Mat.Zeros(4, 1, DepthType.Cv64F, 1);
            }
            else
            {
                distCoeffs = Mat.Zeros(8, 1, DepthType.Cv64F, 1);
            }

            VectorOfVectorOfPoint3D32F objectPoints = new VectorOfVectorOfPoint3D32F();
            objectPoints.Push(new VectorOfPoint3D32F());

            s.BoardSize.Width = 5;
            s.BoardSize.Height = 4;
            s.SquareSize = 50;

            CalcBoardCornerPositions(s.BoardSize, s.SquareSize, objectPoints[0], s.CalibrationPattern);

            // resize objectPoints to imagePoints
            if (objectPoints.Size > imagePoints.Size)
            {
                VectorOfVectorOfPoint3D32F newObjectPoints = new VectorOfVectorOfPoint3D32F();
                for (int i = 0; i < imagePoints.Size; i++)
                {
                    newObjectPoints.Push(new VectorOfPoint3D32F[] { objectPoints[i] });
                }
                objectPoints = newObjectPoints;
            }
            else
            {
                while (objectPoints.Size < imagePoints.Size)
                {
                    objectPoints.Push(new VectorOfPoint3D32F[] { objectPoints[0] });
                }
            }

            // find intrinsic and extrinsic camera parameters
            double rms;

            MCvTermCriteria termCriteria = new MCvTermCriteria(100, 0.0001);
            Mat[] rvecsArray = rvecs.ToArray();
            Mat[] tvecsArray = tvecs.ToArray();

            if (s.useFisheye)
            {
                Mat _rvecs = new Mat();
                Mat _tvecs = new Mat();
                Fisheye.Calibrate(objectPoints, imagePoints, imageSize, cameraMatrix, distCoeffs, _rvecs, _tvecs, (Fisheye.CalibrationFlag)s.Flag, termCriteria);

                for (Int32 i = 0; i < imagePoints.Size; i++)
                {
                    rvecs.Add(_rvecs.Row(i));
                    tvecs.Add(_tvecs.Row(i));
                }
            }
            else
            {
                MCvPoint3D32f[][] objPointsArray = objectPoints.ToArrayOfArray();
                PointF[][] imgPointsArray = imagePoints.ToArrayOfArray();

                CvInvoke.CalibrateCamera(objPointsArray, imgPointsArray, imageSize, cameraMatrix, distCoeffs, (CalibType)s.Flag, termCriteria, out rvecsArray, out tvecsArray);

            }

            System.Drawing.Point posCameraMatrix = new System.Drawing.Point();
            System.Drawing.Point posDistCoeffs = new System.Drawing.Point();

            bool checkCameraMatrix = CvInvoke.CheckRange(cameraMatrix, true, ref posCameraMatrix, Double.MinValue, Double.MaxValue);
            bool checkDistCoeffs = CvInvoke.CheckRange(distCoeffs, true, ref posDistCoeffs, Double.MinValue, Double.MaxValue);

            bool ok = checkCameraMatrix && checkDistCoeffs;

            totalAvgErr = ComputeReprojectionErrors(objectPoints, imagePoints, rvecsArray, tvecsArray, cameraMatrix, distCoeffs, reprojErrs, s.useFisheye);
            return ok;
        }

        static double ComputeReprojectionErrors(VectorOfVectorOfPoint3D32F objectPoints, VectorOfVectorOfPointF imagePoints, Mat[] rvecs, Mat[] tvecs, Mat cameraMatrix, Mat distCoeffs, List<float> perViewErrors, bool fisheye)
        {
            VectorOfPointF imagePoints2 = new VectorOfPointF();
            Int32 totalPoints = 0;
            double totalErr = 0;
            double err;

            int desiredSize = objectPoints.Size;
            while (perViewErrors.Count < desiredSize)
            {
                perViewErrors.Add(default(float));
            }
            while (perViewErrors.Count > desiredSize)
            {
                perViewErrors.RemoveAt(perViewErrors.Count - 1); // Remove from the end
            }

            for (Int32 i = 0; i < objectPoints.Size; i++)
            {
                if (fisheye)
                {
                    Fisheye.ProjectPoints(objectPoints[i], imagePoints2, rvecs[i], tvecs[i], cameraMatrix, distCoeffs);
                }
                else
                {
                    CvInvoke.ProjectPoints(objectPoints[i], rvecs[i], tvecs[i], cameraMatrix, distCoeffs, imagePoints2);
                }
                err = CvInvoke.Norm(imagePoints[i], imagePoints2, NormType.L2);
                Int32 n = objectPoints[i].Size;
                perViewErrors[i] = (float)Math.Sqrt(err * err / n);
                totalErr += err * err;
                totalPoints += n;
            }
            return Math.Sqrt(totalErr / totalPoints);
        }

        static void CalcBoardCornerPositions(Size boardSize, float squareSize, VectorOfPoint3D32F corners, SETTINGS.Pattern patternType)
        {
            corners.Clear();

            List<MCvPoint3D32f> tempList = new List<MCvPoint3D32f>();

            for (int i = 0; i < boardSize.Height; ++i)
            {
                for (int j = 0; j < boardSize.Width; ++j)
                {
                    tempList.Add(new MCvPoint3D32f(j * squareSize, i * squareSize, 0));
                }
            }

            corners.Push(tempList.ToArray());
        }

        static bool FindCalibrationPoints(Mat imageInOrig, VectorOfPointF gridCenters)
        {
            float scaling = 1.0f;
            Mat imageIn = imageInOrig;

            SimpleBlobDetectorParams brightParams = new SimpleBlobDetectorParams
            {
                FilterByColor = true,
                blobColor = 255,  // white circles in the calibration target
                FilterByCircularity = true,
                MinCircularity = 0.8f
            };

            // Create the SimpleBlobDetector using the configured parameters
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
            }

            // scale back the grid centers
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

    }
}
