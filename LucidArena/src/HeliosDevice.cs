using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LucidArena
{
    internal class HeliosDevice
    {

        // file name
        const String FILE_NAME = "Cs_Helios_MinMaxDepth.ply";

        // pixel format
        const String PIXEL_FORMAT = "Coord3D_ABCY16";

        // image timeout (milliseconds)
        const UInt32 TIMEOUT = 2000;

        // =-=-=-=-=-=-=-=-=-
        // =-=- EXAMPLE -=-=-
        // =-=-=-=-=-=-=-=-=-

        // store x, y, z data in mm and intesity for a given point
        struct PointData
        {
            public short x, y, z, intensity;
        };

        // Demonstrates acquiring 3D data for a specific point
        //  (1) gets image
        //  (2) interprets ABCY data to get x, y, z and intensity
        //  (3) stores data for point with min and max z values
        //  (4) displays 3D data for min and max points

        public static (List<Point3d> points, List<int> intensities) GetPointCloud(ArenaNET.IDevice device)
        {
            try
            {
                // Validate if Scan3dCoordinateSelector node exists. If not -
                //    probaly not Helios camera used running the example
                var checkScan3dCoordinateSelectorNode = (ArenaNET.IEnumeration)device.NodeMap.GetNode("Scan3dCoordinateSelector");
            }
            catch (Exception)
            {
                Console.WriteLine("Scan3dCoordinateSelector node is not found. Please make sure that Helios device is used for the example.\n");
                return (new List<Point3d>(), new List<int>());
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
                return (new List<Point3d>(), new List<int>());
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
            Console.WriteLine("Set {0} to pixel format", PIXEL_FORMAT);

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
            double scaleY = Scan3dCoordinateScaleNode.Value;
            // getting offsetY as float by casting since SetPly() will expect it
            // passed as float
            float offsetY = (float)Scan3dCoordinateOffsetNode.Value;

            Scan3dCoordinateSelectorNode.FromString("CoordinateC");
            double scaleZ = Scan3dCoordinateScaleNode.Value;

            // enable stream auto negotiate packet size
            var streamAutoNegotiatePacketSizeNode = (ArenaNET.IBoolean)device.TLStreamNodeMap.GetNode("StreamAutoNegotiatePacketSize");
            streamAutoNegotiatePacketSizeNode.Value = true;

            // enable stream packet resend
            var streamPacketResendEnableNode = (ArenaNET.IBoolean)device.TLStreamNodeMap.GetNode("StreamPacketResendEnable");
            streamPacketResendEnableNode.Value = true;

            // start stream
            device.StartStream();

            // retrieve image
            //Console.WriteLine("Acquire image");
            ArenaNET.IImage image = device.GetImage(TIMEOUT);

            // prepare info from input buffer
            UInt32 width = image.Width;
            UInt32 height = image.Height;
            UInt32 size = width * height;
            UInt32 srcBpp = image.BitsPerPixel;
            UInt32 srcPixelSize = srcBpp / 8;
            byte[] data = image.DataArray;

            // minDepth z value is set to 32767 to guarantee closer points exist
            // as this is the largest value possible
            PointData minDepth;
            minDepth.x = 0;
            minDepth.y = 0;
            minDepth.z = 32767;
            minDepth.intensity = 0;

            PointData maxDepth;
            maxDepth.x = 0;
            maxDepth.y = 0;
            maxDepth.z = 0;
            maxDepth.intensity = 0;

            // find points with min and max z values
            //Console.WriteLine("Find points with min and max z values");

            // disable warning for unreachable code being treated as an error on
            // compilation
#pragma warning disable 0162

            var points = new List<Point3d>();
            var intensities = new List<int>();

            bool isSignedPixelFormat = false;

            if (PIXEL_FORMAT == "Coord3D_ABCY16s")
            {
                isSignedPixelFormat = true;

                int index = 0;

                for (int i = 0; i < size; i++)
                {
                    // Extract point data to signed 16 bit integer
                    //    The first channel is the x coordinate, second channel
                    //    is the y coordinate, the third channel is the z
                    //    coordinate and the fourth channel is intensity. We
                    //    offset pIn by 2 for each channel because pIn is an 8
                    //    bit integer and we want to read it as a 16 bit integer.
                    short x = BitConverter.ToInt16(data, index);
                    short y = BitConverter.ToInt16(data, index + 2);
                    short z = BitConverter.ToInt16(data, index + 4);
                    short intensity = BitConverter.ToInt16(data, index + 6);

                    // convert x, y and z values to mm using their coordinate
                    // scales
                    x = (short)(x * scaleX);
                    y = (short)(y * scaleY);
                    z = (short)(z * scaleZ);

                    if (z < minDepth.z && z > 0)
                    {
                        minDepth.x = x;
                        minDepth.y = y;
                        minDepth.z = z;
                        minDepth.intensity = intensity;
                    }
                    else if (z > maxDepth.z)
                    {
                        maxDepth.x = x;
                        maxDepth.y = y;
                        maxDepth.z = z;
                        maxDepth.intensity = intensity;
                    }

                    points.Add(new Point3d(x, y, z));
                    intensities.Add(intensity);

                    index += (int)srcPixelSize;
                }
                // display data
                //Console.WriteLine("Minimum depth point found with z distance of {0} mm and intensity {1} at coordinates ({2}mm, {3}mm)",
                //        minDepth.z, minDepth.intensity, minDepth.x, minDepth.y);
                //Console.WriteLine("Maximum depth point found with z distance of {0} mm and intensity {1} at coordinates ({2}mm, {3}mm)",
                //        maxDepth.z, maxDepth.intensity, maxDepth.x, maxDepth.y);
            }
            else if (PIXEL_FORMAT == "Coord3D_ABCY16")
            {
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
                        x = (ushort)(x * scaleX + offsetX);
                        y = (ushort)(y * scaleY + offsetY);
                        z = (ushort)(z * scaleZ);

                        if (z < minDepth.z && z > 0)
                        {
                            minDepth.x = (short)x;
                            minDepth.y = (short)y;
                            minDepth.z = (short)z;
                            minDepth.intensity = (short)intensity;
                        }
                        else if (z > maxDepth.z)
                        {
                            maxDepth.x = (short)x;
                            maxDepth.y = (short)y;
                            maxDepth.z = (short)z;
                            maxDepth.intensity = (short)intensity;
                        }
                        
                        points.Add(new Point3d(x, y, z));
                        intensities.Add(intensity);

                    }

                    index += (int)srcPixelSize;
                }
                // display data
                //Console.WriteLine("Minimum depth point found with z distance of {0} mm and intensity {1} at coordinates ({2}mm, {3}mm)",
                //        minDepth.z, minDepth.intensity, minDepth.x, minDepth.y);
                //Console.WriteLine("Maximum depth point found with z distance of {0} mm and intensity {1} at coordinates ({2}mm, {3}mm)",
                //        maxDepth.z, maxDepth.intensity, maxDepth.x, maxDepth.y);
            }

            // restore warning to being enabled
#pragma warning restore 0162

            //// prepare image parameters
            //SaveNET.ImageParams parameters = new SaveNET.ImageParams(
            //        image.Width,
            //        image.Height,
            //        image.BitsPerPixel,
            //        true);

            //// prepare image writer
            //SaveNET.ImageWriter writer = new SaveNET.ImageWriter(parameters, FILE_NAME);

            //// set default parameters for SetPly()
            //bool filterPoints = true;
            //float offsetZ = 0.0f;

            //// set the output file format of the image writer to .ply
            //writer.SetPly(".ply",
            //                        filterPoints,
            //                        isSignedPixelFormat,
            //                        scaleX,
            //                        offsetX, // using scaleX as scale since all scales = 0.25f
            //                        offsetY,
            //                        offsetZ);

            //// save
            //Console.WriteLine("Save image {1}\n", FILE_NAME);

            //writer.Save(image.DataArray, true);

            // var maxIntensity = intensities.Max();
            // var minIntensity = intensities.Min();

            // clean up example
            device.RequeueBuffer(image);
            device.StopStream();

            // return nodes to their initial values
            pixelFormatNode.FromString(pixelFormatInitial);
            operatingModeNode.FromString(operatingModeInitial);
            //Console.WriteLine("Nodes were set back to initial values");

            return (points, intensities);
        }

        static void AcquireImageAndInterpretData(ArenaNET.IDevice device)
        {
            try
            {
                // Validate if Scan3dCoordinateSelector node exists. If not -
                //    probaly not Helios camera used running the example
                var checkScan3dCoordinateSelectorNode = (ArenaNET.IEnumeration)device.NodeMap.GetNode("Scan3dCoordinateSelector");
            }
            catch (Exception)
            {
                Console.WriteLine("Scan3dCoordinateSelector node is not found. Please make sure that Helios device is used for the example.\n");
                return;
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
                return;
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
            Console.WriteLine("Set {1} to pixel format", PIXEL_FORMAT);

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
            double scaleY = Scan3dCoordinateScaleNode.Value;
            // getting offsetY as float by casting since SetPly() will expect it
            // passed as float
            float offsetY = (float)Scan3dCoordinateOffsetNode.Value;

            Scan3dCoordinateSelectorNode.FromString("CoordinateC");
            double scaleZ = Scan3dCoordinateScaleNode.Value;

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
            UInt32 srcPixelSize = srcBpp / 8;
            byte[] data = image.DataArray;

            // minDepth z value is set to 32767 to guarantee closer points exist
            // as this is the largest value possible
            PointData minDepth;
            minDepth.x = 0;
            minDepth.y = 0;
            minDepth.z = 32767;
            minDepth.intensity = 0;

            PointData maxDepth;
            maxDepth.x = 0;
            maxDepth.y = 0;
            maxDepth.z = 0;
            maxDepth.intensity = 0;

            // find points with min and max z values
            Console.WriteLine("Find points with min and max z values");

            // disable warning for unreachable code being treated as an error on
            // compilation
#pragma warning disable 0162

            bool isSignedPixelFormat = false;

            if (PIXEL_FORMAT == "Coord3D_ABCY16s")
            {
                isSignedPixelFormat = true;

                int index = 0;

                for (int i = 0; i < size; i++)
                {
                    // Extract point data to signed 16 bit integer
                    //    The first channel is the x coordinate, second channel
                    //    is the y coordinate, the third channel is the z
                    //    coordinate and the fourth channel is intensity. We
                    //    offset pIn by 2 for each channel because pIn is an 8
                    //    bit integer and we want to read it as a 16 bit integer.
                    short x = BitConverter.ToInt16(data, index);
                    short y = BitConverter.ToInt16(data, index + 2);
                    short z = BitConverter.ToInt16(data, index + 4);
                    short intensity = BitConverter.ToInt16(data, index + 6);

                    // convert x, y and z values to mm using their coordinate
                    // scales
                    x = (short)(x * scaleX);
                    y = (short)(y * scaleY);
                    z = (short)(z * scaleZ);

                    if (z < minDepth.z && z > 0)
                    {
                        minDepth.x = x;
                        minDepth.y = y;
                        minDepth.z = z;
                        minDepth.intensity = intensity;
                    }
                    else if (z > maxDepth.z)
                    {
                        maxDepth.x = x;
                        maxDepth.y = y;
                        maxDepth.z = z;
                        maxDepth.intensity = intensity;
                    }

                    index += (int)srcPixelSize;
                }
                // display data
                Console.WriteLine("Minimum depth point found with z distance of {0} mm and intensity {1} at coordinates ({2}mm, {3}mm)",
                        minDepth.z, minDepth.intensity, minDepth.x, minDepth.y);
                Console.WriteLine("Maximum depth point found with z distance of {0} mm and intensity {1} at coordinates ({2}mm, {3}mm)",
                        maxDepth.z, maxDepth.intensity, maxDepth.x, maxDepth.y);
            }
            else if (PIXEL_FORMAT == "Coord3D_ABCY16")
            {
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
                        x = (ushort)(x * scaleX + offsetX);
                        y = (ushort)(y * scaleY + offsetY);
                        z = (ushort)(z * scaleZ);

                        if (z < minDepth.z && z > 0)
                        {
                            minDepth.x = (short)x;
                            minDepth.y = (short)y;
                            minDepth.z = (short)z;
                            minDepth.intensity = (short)intensity;
                        }
                        else if (z > maxDepth.z)
                        {
                            maxDepth.x = (short)x;
                            maxDepth.y = (short)y;
                            maxDepth.z = (short)z;
                            maxDepth.intensity = (short)intensity;
                        }
                    }

                    index += (int)srcPixelSize;
                }
                // display data
                Console.WriteLine("Minimum depth point found with z distance of {0} mm and intensity {1} at coordinates ({2}mm, {3}mm)",
                        minDepth.z, minDepth.intensity, minDepth.x, minDepth.y);
                Console.WriteLine("Maximum depth point found with z distance of {0} mm and intensity {1} at coordinates ({2}mm, {3}mm)",
                        maxDepth.z, maxDepth.intensity, maxDepth.x, maxDepth.y);
            }

            // restore warning to being enabled
#pragma warning restore 0162

            //// prepare image parameters
            //SaveNET.ImageParams parameters = new SaveNET.ImageParams(
            //        image.Width,
            //        image.Height,
            //        image.BitsPerPixel,
            //        true);

            //// prepare image writer
            //SaveNET.ImageWriter writer = new SaveNET.ImageWriter(parameters, FILE_NAME);

            //// set default parameters for SetPly()
            //bool filterPoints = true;
            //float offsetZ = 0.0f;

            //// set the output file format of the image writer to .ply
            //writer.SetPly(".ply",
            //                        filterPoints,
            //                        isSignedPixelFormat,
            //                        scaleX,
            //                        offsetX, // using scaleX as scale since all scales = 0.25f
            //                        offsetY,
            //                        offsetZ);

            //// save
            //Console.WriteLine("Save image {1}\n", FILE_NAME);

            //writer.Save(image.DataArray, true);

            // clean up example
            device.RequeueBuffer(image);
            device.StopStream();

            // return nodes to their initial values
            pixelFormatNode.FromString(pixelFormatInitial);
            operatingModeNode.FromString(operatingModeInitial);
            Console.WriteLine("Nodes were set back to initial values");
        }

    }
}
