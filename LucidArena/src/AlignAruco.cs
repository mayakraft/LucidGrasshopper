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
using Emgu.CV.Aruco;
using Eto.Forms;

namespace LucidArena
{
    internal class AlignAruco
    {
        public static List<Mat> AlignArucoImages(List<Mat> images)
        {
            //// Create charuco board object and CharucoDetector
            //CharucoBoard board(Size(squaresX, squaresY), squareLength, markerLength, dictionary);
            //aruco::CharucoDetector detector(board, charucoParams, detectorParams);

            //// Collect data from each frame
            //vector<Mat> allCharucoCorners, allCharucoIds;

            //vector<vector<Point2f>> allImagePoints;
            //vector<vector<Point3f>> allObjectPoints;

            //vector<Mat> allImages;
            //Size imageSize;

            //while (inputVideo.grab())
            //{
            //    Mat image, imageCopy;
            //    inputVideo.retrieve(image);

            //    vector<int> markerIds;
            //    vector<vector<Point2f>> markerCorners;
            //    Mat currentCharucoCorners, currentCharucoIds;
            //    vector<Point3f> currentObjectPoints;
            //    vector<Point2f> currentImagePoints;

            //    // Detect ChArUco board
            //    detector.detectBoard(image, currentCharucoCorners, currentCharucoIds);
            //    if (key == 'c' && currentCharucoCorners.total() > 3)
            //    {
            //        // Match image points
            //        board.matchImagePoints(currentCharucoCorners, currentCharucoIds, currentObjectPoints, currentImagePoints);

            //        if (currentImagePoints.empty() || currentObjectPoints.empty())
            //        {
            //            cout << "Point matching failed, try again." << endl;
            //            continue;
            //        }

            //        cout << "Frame captured" << endl;

            //        allCharucoCorners.push_back(currentCharucoCorners);
            //        allCharucoIds.push_back(currentCharucoIds);
            //        allImagePoints.push_back(currentImagePoints);
            //        allObjectPoints.push_back(currentObjectPoints);
            //        allImages.push_back(image);

            //        imageSize = image.size();
            //    }
            //}
            //Mat cameraMatrix, distCoeffs;

            //if (calibrationFlags & CALIB_FIX_ASPECT_RATIO)
            //{
            //    cameraMatrix = Mat::eye(3, 3, CV_64F);
            //    cameraMatrix.at<double>(0, 0) = aspectRatio;
            //}

            //// Calibrate camera using ChArUco
            //double repError = calibrateCamera(allObjectPoints, allImagePoints, imageSize, cameraMatrix, distCoeffs,
            //                                  noArray(), noArray(), noArray(), noArray(), noArray(), calibrationFlags);

            return images.Select(img => new Mat()).ToList();
        }
    }
}
