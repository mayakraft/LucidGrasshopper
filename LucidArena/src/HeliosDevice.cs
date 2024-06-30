using Grasshopper.Kernel.Types.Transforms;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LucidArena
{
    internal class HeliosDevice
    {
        private static (List<Point3d> points, List<int> intensities) GetPointCloudUnsigned(
            byte[] data,
            UInt32 size,
            int srcPixelSize,
            float scaleX,
            float scaleY,
            float scaleZ,
            float offsetX,
            float offsetY)
        {
            var points = new List<Point3d>();
            var intensities = new List<int>();
            int index = 0;
            for (int i = 0; i < size; i++)
            {
                // Extract point data to signed 16 bit integer
                //    The first channel is the x coordinate, second channel
                //    is the y coordinate, the third channel is the z
                //    coordinate and the fourth channel is intensity. We
                //    offset pIn by 2 for each channel because pIn is an 8
                //    bit integer and we want to read it as a 16 bit integer.
                ushort x = BitConverter.ToUInt16(data, index);
                ushort y = BitConverter.ToUInt16(data, index + 2);
                ushort z = BitConverter.ToUInt16(data, index + 4);
                ushort intensity = BitConverter.ToUInt16(data, index + 6);

                float _x, _y, _z;

                // if z is less than max value, as invalid values get
                // filtered to 65535
                if (z < 65535)
                {
                    // Convert x, y and z to millimeters
                    //    Using each coordinates' appropriate scales, convert
                    //    x, y and z values to mm. For the x and y
                    //    coordinates in an unsigned pixel format, we must
                    //    then add the offset to our converted values in
                    //    order to get the correct position in millimeters.
                    _x = x * scaleX + offsetX;
                    _y = y * scaleY + offsetY;
                    _z = z * scaleZ;
                    points.Add(new Point3d(_x, _y, _z));
                    intensities.Add(intensity);
                }

                index += srcPixelSize;
            }
            return (points, intensities);
        }

        public static (List<Point3d> points, List<int> intensities) GetPointCloud(ArenaNET.IDevice device)
        {
            var points = new List<Point3d>();
            var intensities = new List<int>();

            const UInt32 TIMEOUT = 2000;
            const String PIXEL_FORMAT = "Coord3D_ABCY16";

            try
            {
                // Validate if Scan3dCoordinateSelector node exists. If not -
                //    probaly not Helios camera used running the example
                var checkScan3dCoordinateSelectorNode = (ArenaNET.IEnumeration)device.NodeMap.GetNode("Scan3dCoordinateSelector");
            }
            catch (Exception)
            {
                Console.WriteLine("Scan3dCoordinateSelector node is not found. Please make sure that Helios device is used for the example.\n");
                return (points, intensities);
            }

            try
            {
                // Validate if Scan3dCoordinateOffset node exists. If not -
                //    probaly Helios has an old firmware
                var checkScan3dCoordinateOffset = (ArenaNET.IFloat)device.NodeMap.GetNode("Scan3dCoordinateOffset");
            }
            catch (Exception)
            {
                Console.WriteLine("Scan3dCoordinateOffset node is not found. Please update Helios firmware.\n");
                return (points, intensities);
            }

            // check if Helios2 camera used for the example
            bool isHelios2 = false;
            var deviceModelNameNode = (ArenaNET.IString)device.NodeMap.GetNode("DeviceModelName");
            String deviceModelName = deviceModelNameNode.Value;
            if (deviceModelName.StartsWith("HLT") || deviceModelName.StartsWith("HTP"))
            {
                isHelios2 = true;
            }


            // Get node values that will be changed in order to return their values at
            //    the end of the example

            var pixelFormatNode = (ArenaNET.IEnumeration)device.NodeMap.GetNode("PixelFormat");
            String pixelFormatInitial = pixelFormatNode.Entry.Symbolic;

            var operatingModeNode = (ArenaNET.IEnumeration)device.NodeMap.GetNode("Scan3dOperatingMode");
            String operatingModeInitial = operatingModeNode.Entry.Symbolic;

            // Set pixel format
            //    Warning: HLT003S-001 / Helios2 - has only Coord3D_ABCY16 in
            //    this case This example demonstrates data interpretation for
            //    both a signed or unsigned pixel format. Default PIXEL_FORMAT
            //    here is set to Coord3D_ABCY16 but this can be modified to be a
            //    signed pixel format by changing it to Coord3D_ABCY16s.

            pixelFormatNode.FromString(PIXEL_FORMAT);

            // set operating mode distance

            if (isHelios2)
            {
                Console.WriteLine("Set 3D operating mode to Distance3000mm");
                operatingModeNode.FromString("Distance3000mmSingleFreq");
            }
            else
            {
                Console.WriteLine("Set 3D operating mode to Distance1500mm");
                operatingModeNode.FromString("Distance1500mm");
            }

            // get the offset for x and y to correctly adjust values when in an
            // unsigned pixel format
            Console.WriteLine("Get xyz coordinate scales and offsets\n");

            // get Scan3dCoordinate nodes
            var Scan3dCoordinateSelectorNode = (ArenaNET.IEnumeration)device.NodeMap.GetNode("Scan3dCoordinateSelector");
            var Scan3dCoordinateScaleNode = (ArenaNET.IFloat)device.NodeMap.GetNode("Scan3dCoordinateScale");
            var Scan3dCoordinateOffsetNode = (ArenaNET.IFloat)device.NodeMap.GetNode("Scan3dCoordinateOffset");

            Scan3dCoordinateSelectorNode.FromString("CoordinateA");
            // getting scaleX as float by casting since SetPly() will expect it
            // passed as float
            float scaleX = (float)Scan3dCoordinateScaleNode.Value;
            // getting offsetX as float by casting since SetPly() will expect it
            // passed as float
            float offsetX = (float)Scan3dCoordinateOffsetNode.Value;

            Scan3dCoordinateSelectorNode.FromString("CoordinateB");
            float scaleY = (float)Scan3dCoordinateScaleNode.Value;
            // getting offsetY as float by casting since SetPly() will expect it
            // passed as float
            float offsetY = (float)Scan3dCoordinateOffsetNode.Value;

            Scan3dCoordinateSelectorNode.FromString("CoordinateC");
            float scaleZ = (float)Scan3dCoordinateScaleNode.Value;

            // enable stream auto negotiate packet size
            var streamAutoNegotiatePacketSizeNode = (ArenaNET.IBoolean)device.TLStreamNodeMap.GetNode("StreamAutoNegotiatePacketSize");
            streamAutoNegotiatePacketSizeNode.Value = true;

            // enable stream packet resend
            var streamPacketResendEnableNode = (ArenaNET.IBoolean)device.TLStreamNodeMap.GetNode("StreamPacketResendEnable");
            streamPacketResendEnableNode.Value = true;

            // start stream
            device.StartStream();

            // retrieve image
            Console.WriteLine("Acquire image");
            ArenaNET.IImage image = device.GetImage(TIMEOUT);

            // prepare info from input buffer
            UInt32 width = image.Width;
            UInt32 height = image.Height;
            UInt32 size = width * height;
            UInt32 srcBpp = image.BitsPerPixel;
            int srcPixelSize = (int)(srcBpp / 8);
            byte[] data = image.DataArray;

            (points, intensities) = GetPointCloudUnsigned(data, size, srcPixelSize, scaleX, scaleY, scaleZ, offsetX, offsetY);

            // clean up example
            device.RequeueBuffer(image);
            device.StopStream();

            // return nodes to their initial values
            pixelFormatNode.FromString(pixelFormatInitial);
            operatingModeNode.FromString(operatingModeInitial);
            Console.WriteLine("Nodes were set back to initial values");

            return (points, intensities);
        }
    }
}
