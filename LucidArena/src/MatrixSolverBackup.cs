using Rhino.FileIO;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace LucidArena
{
    internal class MatrixSolver
    {
            
        // Function to populate A matrix for the linear least squares problem
        private static Matrix<double> BuildAMatrix(List<Transform> transforms, List<Point3d> points) {
            // Number of scans
            int numScans = points.Count;

            // A is a (3 * numScans) x 16 matrix
            Matrix<double> A = DenseMatrix.OfArray(new double[3 * numScans, 16]);

            // Loop through each scan
            for (int i = 0; i < numScans; i++) {
                Point3d point = points[i];
                Transform transform = transforms[i];

                // Loop through the rows (0 for x, 1 for y, 2 for z)
                for (int row = 0; row < 3; row++) {
                    for (int col = 0; col < 4; col++) {
                        // Fill A based on the product of the transform and the point coordinates
                        A[3 * i + row, 4 * col + 0] = transform[row, 0] * (col == 3 ? 1 : point.X);
                        A[3 * i + row, 4 * col + 1] = transform[row, 1] * (col == 3 ? 1 : point.Y);
                        A[3 * i + row, 4 * col + 2] = transform[row, 2] * (col == 3 ? 1 : point.Z);
                        A[3 * i + row, 4 * col + 3] = transform[row, 3] * (col == 3 ? 1 : 1); // Homogeneous coordinate
                    }
                }
            }

            return A;
        }

        // Function to build the b vector (target positions)
        private static Vector<double> BuildBVector(List<Point3d> referencePoints) {
            int numScans = referencePoints.Count;

            // b is a (3 * numScans) vector containing the reference point positions
            Vector<double> b = DenseVector.OfArray(new double[3 * numScans]);

            // Fill b with the reference points' x, y, z components
            for (int i = 0; i < numScans; i++) {
                Point3d refPoint = referencePoints[i];
                b[3 * i + 0] = refPoint.X;
                b[3 * i + 1] = refPoint.Y;
                b[3 * i + 2] = refPoint.Z;
            }

            return b;
        }

        // Function to compute the correction matrix using least squares solver
        private static Transform SolveForCorrectionMatrix(Matrix<double> A, Vector<double> b) {
            // Solve the linear system using the least squares approach: A * C = b
            Vector<double> C_vector = A.PseudoInverse() * b;

            // Convert the resulting vector into a 4x4 matrix
            Transform correctionMatrix = Transform.Identity;

            // Fill in the correction matrix from the solved vector (C_vector)
            for (int row = 0; row < 4; row++) {
                for (int col = 0; col < 4; col++) {
                    correctionMatrix[row, col] = C_vector[4 * row + col];
                }
            }

            return correctionMatrix;
        }

        // Main execution function
        public static Transform Solve(
            List<Transform> T, // List of transforms for each scan
            List<Point3d> P,   // List of reference points (should align)
            List<Point3d> refP, // List of actual reference points in the aligned space
            out string info) // Information on the process
        {
            // Build the A matrix
            Matrix<double> A = BuildAMatrix(T, P);

            // Build the b vector
            Vector<double> b = BuildBVector(refP);

            // Solve for the correction matrix
            Transform C = SolveForCorrectionMatrix(A, b);

            info = "Correction matrix successfully computed.";
            return C;
        }

        public static Transform Solve(List<Transform> transforms, List<Point3d> points, out string info)
        {
            info = string.Empty;
            int numPoints = points.Count;
            Matrix<double> A = Matrix<double>.Build.Dense(3 * numPoints, 16);
            Matrix<double> b = Matrix<double>.Build.Dense(3 * numPoints, 1);
            // Matrix A = new Matrix(3 * numPoints, 16);
            // Matrix b = new Matrix(3 * numPoints, 1);

            for (int i = 0; i < numPoints; i++)
            {
                Point3d p = points[i];
                Transform T = transforms[i];

                Point3d tp = T * p;

                // for (int row = 0; row < 3; row++)
                // {
                //     for (int col = 0; col < 16; col++)
                //     {
                //         // A[3 * i + row, col] = /* todo */ 0;
                //     }
                // }
                for (int m = 0; m < 4; m++)
                {
                    var pointValue = m == 3 ? 1.0 : points[i][m];
                    A[i * 3 + 0, m * 4 + 0] = transforms[i][0, 0] * pointValue;
                    A[i * 3 + 0, m * 4 + 1] = transforms[i][0, 1] * pointValue;
                    A[i * 3 + 0, m * 4 + 2] = transforms[i][0, 2] * pointValue;
                    A[i * 3 + 0, m * 4 + 3] = transforms[i][0, 3] * pointValue;

                    A[i * 3 + 1, m * 4 + 0] = transforms[i][1, 0] * pointValue;
                    A[i * 3 + 1, m * 4 + 1] = transforms[i][1, 1] * pointValue;
                    A[i * 3 + 1, m * 4 + 2] = transforms[i][1, 2] * pointValue;
                    A[i * 3 + 1, m * 4 + 3] = transforms[i][1, 3] * pointValue;

                    A[i * 3 + 2, m * 4 + 0] = transforms[i][2, 0] * pointValue;
                    A[i * 3 + 2, m * 4 + 1] = transforms[i][2, 1] * pointValue;
                    A[i * 3 + 2, m * 4 + 2] = transforms[i][2, 2] * pointValue;
                    A[i * 3 + 2, m * 4 + 3] = transforms[i][2, 3] * pointValue;
                }

                b[3 * i + 0, 0] = tp.X;
                b[3 * i + 1, 0] = tp.Y;
                b[3 * i + 2, 0] = tp.Z;
            }

            // debug
            for (int i = 0; i < 3 * numPoints; i++)
            {
                for (int j = 0; j < 16; j++)
                {
                    info += $"{A[i, j]} ";
                }
                info += "\n";
            }

            info += "\n";

            var correctionVec = A.Solve(b);

            for (int i = 0; i < 16; i++)
            {
                info += $"{correctionVec[i, 0]}\n";
            }

            // if (!success) throw new Exception("No solution found");

            Transform C = Transform.Identity;
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    C[i, j] = correctionVec[4 * i + j, 0];
                }
            }

            return C;
        }
    }
}
