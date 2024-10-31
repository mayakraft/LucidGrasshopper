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
using Emgu.CV.Aruco;
using Emgu.CV.Util;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Features2D;
using System.Runtime;
using System.Runtime.InteropServices;
using ArenaNET;
using Eto.Forms;
using static Emgu.CV.Fisheye;
using static Emgu.CV.OCR.Tesseract;
using System.Collections;

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


        //public static List<Mat> AlignArucoImages2(List<Mat> images)
        //{
        //    Mat cameraMatrix, distCoeffs;
        //    Ptr<aruco::Dictionary> dictionary = GetPredefinedDictionary(cv::aruco::DICT_6X6_250);
        //    var board = new CharucoBoard(5, 7, 0.04f, 0.02f, dictionary);
        //    var paramss = new DetectorParameters();
        //    Mat image;
        //    Mat imageCopy;
        //    inputVideo.retrieve(image);
        //    image.copyTo(imageCopy);
        //    std::vector<int> markerIds;
        //    std::vector<std::vector<cv::Point2f>> markerCorners;
        //    cv::aruco::detectMarkers(image, board->dictionary, markerCorners, markerIds, paramss);
        //    // if at least one marker detected
        //    if (markerIds.size() > 0)
        //    {
        //        cv::aruco::drawDetectedMarkers(imageCopy, markerCorners, markerIds);
        //        std::vector<cv::Point2f> charucoCorners;
        //        std::vector<int> charucoIds;
        //        cv::aruco::interpolateCornersCharuco(markerCorners, markerIds, image, board, charucoCorners, charucoIds, cameraMatrix, distCoeffs);
        //        // if at least one charuco corner detected
        //        if (charucoIds.size() > 0)
        //        {
        //            cv::Scalar color = cv::Scalar(255, 0, 0);
        //            cv::aruco::drawDetectedCornersCharuco(imageCopy, charucoCorners, charucoIds, color);
        //            cv::Vec3d rvec, tvec;
        //            bool valid = cv::aruco::estimatePoseCharucoBoard(charucoCorners, charucoIds, board, cameraMatrix, distCoeffs, rvec, tvec);
        //            // if charuco pose is valid
        //            if (valid)
        //                cv::drawFrameAxes(imageCopy, cameraMatrix, distCoeffs, rvec, tvec, 0.1f);
        //        }
        //    }
        //    cv::imshow("out", imageCopy);
        //}
    }
}
