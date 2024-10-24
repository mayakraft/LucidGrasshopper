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
    internal class CorrectionMatrixSolver
    {
        // Method to compute the correction matrix C
        // public static Matrix<double> ComputeCorrectionMatrix(List<Transform> transforms, List<Point3d> referencePoints)
        public static Transform ComputeCorrectionMatrix(List<Transform> transforms, List<Point3d> referencePoints, out string info)
        {
            info = string.Empty;
            int numPoints = referencePoints.Count;

            // Assuming a 4x4 correction matrix (homogeneous transformation)
            Matrix<double> A = DenseMatrix.OfArray(new double[3 * numPoints, 16]); // 16 unknowns in a 4x4 matrix
            Vector<double> b = DenseVector.OfArray(new double[3 * numPoints]);

            // Fill A and b according to the least squares system
            for (int i = 0; i < numPoints; i++)
            {
                // Transform matrix for point cloud i
                Transform T = transforms[i];
                Matrix<double> Ti = TransformToMatrix(T); // Convert Rhino Transform to MathNet matrix

                // Reference point from the transformed cloud
                Point3d p = referencePoints[i];

                // Extract coordinates of the point (p_x, p_y, p_z) and fill the rows for the least squares system
                double px = p.X;
                double py = p.Y;
                double pz = p.Z;

                // Ti^{-1} * p: Applying inverse of transform to p (convert back to original space)
                var success = T.TryGetInverse(out var inverseT);
                if (!success) throw new Exception("Matrix not invertible");
                Point3d pOriginal = inverseT * p;

                // Fill the matrix A and vector b for each point
                // 3 rows per point: x, y, z component
                for (int row = 0; row < 3; row++)
                {
                    for (int col = 0; col < 16; col++)
                    {
                        // Fill the corresponding row and column of A
                        A[3 * i + row, col] = FillAForPointCloud(row, col, pOriginal);
                    }

                    // Fill the target vector b with reference points
                    switch (row)
                    {
                        case 0:
                            b[3 * i + row] = px; // x-coordinate
                            break;
                        case 1:
                            b[3 * i + row] = py; // y-coordinate
                            break;
                        case 2:
                            b[3 * i + row] = pz; // z-coordinate
                            break;
                    }
                }
            }
 
            for (int i = 0; i < 3 * numPoints; i++) {
                for (int j = 0; j < 16; j++) info += $"{A[i, j]} ";
                info += "\n";
            }
            info += "\n";

            // var solution = A.QR().Solve(b);
            // Solve the least squares system A * C = b
            // Vector<double> solution = A.TransposeThisAndMultiply(A).Cholesky().Solve(A.TransposeThisAndMultiply(b));
            var solution = SolveLeastSquares(A, b, out var squaresInfo);
            info += squaresInfo;

            // Reshape the solution into a 4x4 correction matrix C
            Matrix<double> correctionMatrix = DenseMatrix.OfArray(new double[4, 4]);
            for (int i = 0; i < 16; i++) {
                correctionMatrix[i / 4, i % 4] = solution[i];
            }

            var correctionVec = A.Solve(b);

            for (int i = 0; i < 16; i++) info += $"{solution[i]}\n";

            return MatrixToRhinoTransform(correctionMatrix);
        }

        // Convert Rhino Transform to MathNet matrix
        private static Matrix<double> TransformToMatrix(Transform T)
        {
            return DenseMatrix.OfArray(new double[,]
            {
                { T.M00, T.M01, T.M02, T.M03 },
                { T.M10, T.M11, T.M12, T.M13 },
                { T.M20, T.M21, T.M22, T.M23 },
                { T.M30, T.M31, T.M32, T.M33 }
            });
        }

        public static Transform MatrixToRhinoTransform(Matrix<double> matrix)
        {
            if (matrix.RowCount != 4 || matrix.ColumnCount != 4)
                throw new ArgumentException("Matrix must be 4x4.");

            Transform transform = Transform.Identity;

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    transform[i, j] = matrix[i, j];
                }
            }

            return transform;
        }

        // Fill matrix A for a point cloud at a given row and column
        private static double FillAForPointCloud(int row, int col, Point3d pOriginal)
        {
            if (row == 0)
            {
                // First row: dealing with x-coordinate
                if (col == 0) return pOriginal.X;
                if (col == 1) return pOriginal.Y;
                if (col == 2) return pOriginal.Z;
                if (col == 3) return 1.0;
                if (col >= 4 && col <= 15) return 0.0;
            }
            else if (row == 1)
            {
                // Second row: dealing with y-coordinate
                if (col == 0 || col == 1 || col == 2 || col == 3) return 0.0;
                if (col == 4) return pOriginal.X;
                if (col == 5) return pOriginal.Y;
                if (col == 6) return pOriginal.Z;
                if (col == 7) return 1.0;
                if (col >= 8 && col <= 15) return 0.0;
            }
            else if (row == 2)
            {
                // Third row: dealing with z-coordinate
                if (col == 0 || col == 1 || col == 2 || col == 3) return 0.0;
                if (col == 4 || col == 5 || col == 6 || col == 7) return 0.0;
                if (col == 8) return pOriginal.X;
                if (col == 9) return pOriginal.Y;
                if (col == 10) return pOriginal.Z;
                if (col == 11) return 1.0;
                if (col >= 12 && col <= 15) return 0.0;
            }

            throw new IndexOutOfRangeException("Invalid row or column");
        }
        
        private static Vector<double> SolveLeastSquares(Matrix<double> A, Vector<double> b, out string info)
        {
            info = string.Empty;
            // Step 1: Compute AᵀA
            var AtA = A.TransposeThisAndMultiply(A);
            
            // Step 2: Symmetrize AtA
            AtA = 0.5 * (AtA + AtA.Transpose());

            // Step 3: Regularization
            var lambda = 1e-5; // Small regularization value
            var AtA_reg = AtA + lambda * Matrix<double>.Build.DenseIdentity(AtA.RowCount);

            // Log dimensions for debugging
            info += $"A dimensions: {A.RowCount}x{A.ColumnCount}";
            info += $"b dimensions: {b.Count}";

            // Log values for debugging
            info += "Matrix A:";
            info += A.ToString();
            info += "Vector b:";
            info += b.ToString();

            // Step 4: Try to solve using QR decomposition
            Vector<double> solution;

            try
            {
                // Use QR decomposition
                solution = AtA_reg.QR().Solve(b);
            }
            catch (Exception ex)
            {
                info += $"QR Solve Error: {ex.Message}";
                throw; // Re-throw the exception after logging
            }

            return solution;
        }
    }
}
