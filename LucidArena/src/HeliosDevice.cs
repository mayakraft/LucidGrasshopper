using Grasshopper.Kernel.Types.Transforms;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LucidArena
{
    internal class HeliosDevice
    {
        public static (List<Point3d> points, List<int> intensities) GetPointCloudUnsigned(
            byte[] data,
            UInt32 size,
            int srcPixelSize,
            float scaleX,
            float scaleY,
            float scaleZ,
            float offsetX,
            float offsetY,
            float offsetZ)
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
                    _z = z * scaleZ + offsetZ;
                    points.Add(new Point3d(_x, _y, _z));
                    intensities.Add(intensity);
                }

                index += srcPixelSize;
            }
            return (points, intensities);
        }

        /// <summary>
        /// Get the Helios Camera device version (1 or 2), also checking if this is even a Helios Camera device to begin with.
        /// </summary>
        /// <param name="device">The device in question.</param>
        /// <returns>Returns -1 if not a Helios device. Otherwise, returns 1 or 2 depending on device version.</returns>
        private static int GetHeliosCameraVersion(ArenaNET.IDevice device)
        {
            try
            {
                // Validate if Scan3dCoordinateSelector node exists. If not -  probably not Helios camera
                var checkScan3dCoordinateSelectorNode = (ArenaNET.IEnumeration)device.NodeMap.GetNode("Scan3dCoordinateSelector");
                // Validate if Scan3dCoordinateOffset node exists. If not - probably Helios has an old firmware
                var checkScan3dCoordinateOffset = (ArenaNET.IFloat)device.NodeMap.GetNode("Scan3dCoordinateOffset");
            }
            catch (Exception)
            {
                return -1;
            }

            int heliosVersion = 1;
            var deviceModelNameNode = (ArenaNET.IString)device.NodeMap.GetNode("DeviceModelName");
            String deviceModelName = deviceModelNameNode.Value;
            if (deviceModelName.StartsWith("HLT") || deviceModelName.StartsWith("HTP"))
            {
                heliosVersion = 2;
            }
            return heliosVersion;
        }

        public struct HeliosSettings
        {
            // set exposure time
            public string exposureTime;
            // set gain
            public string conversionGain;
            // set image accumulation
            public int imageAccumulation;
            // enable spatial filter
            public bool spatialFilter;
            // enable confidence threshold
            public bool confidenceThreshold;
            public HeliosSettings(string exposureTime = "Exp1000Us", string conversionGain = "Low", int imageAccumulation = 4, bool spatialFilter = true, bool confidenceThreshold = true)
            {
                this.exposureTime = exposureTime;
                this.conversionGain = conversionGain;
                this.imageAccumulation = imageAccumulation;
                this.spatialFilter = spatialFilter;
                this.confidenceThreshold = confidenceThreshold;
            }
            public override string ToString()
            {
                return $"exposure time: {this.exposureTime}\nconversion gain: {this.conversionGain}\nimage accumulation: {this.imageAccumulation}\nspatial filter: {this.spatialFilter}\nconfidence threshold: {this.confidenceThreshold}";
            }

        }

        public static (List<Point3d> points, List<int> intensities) GetPointCloud(ArenaNET.IDevice device, HeliosSettings settings)
        {
            const UInt32 TIMEOUT = 2000;
            const String PIXEL_FORMAT = "Coord3D_ABCY16";

            int heliosDeviceVersion = GetHeliosCameraVersion(device);
            if (heliosDeviceVersion == 0)
            {
                return (points: new List<Point3d>(), intensities: new List<int>());
            }

            // Get a bunch of node values to be modified as settings.
            var pixelFormatNode = (ArenaNET.IEnumeration)device.NodeMap.GetNode("PixelFormat");
            String pixelFormatInitial = pixelFormatNode.Entry.Symbolic;

            var operatingModeNode = (ArenaNET.IEnumeration)device.NodeMap.GetNode("Scan3dOperatingMode");
            String operatingModeInitial = operatingModeNode.Entry.Symbolic;

            var exposureTimeSelectorNode = (ArenaNET.IEnumeration)device.NodeMap.GetNode("ExposureTimeSelector");
            String exposureTimeSelectorInitial = exposureTimeSelectorNode.Entry.Symbolic;

            var conversionGainNode = (ArenaNET.IEnumeration)device.NodeMap.GetNode("ConversionGain");
            String conversionGainNodeInitial = conversionGainNode.Entry.Symbolic;

            var imageAccumulationNode = (ArenaNET.IInteger)device.NodeMap.GetNode("Scan3dImageAccumulation");
            Int64 imageAccumulationInitial = imageAccumulationNode.Value;

            var spatialFilterNode = (ArenaNET.IBoolean)device.NodeMap.GetNode("Scan3dSpatialFilterEnable");
            Boolean spatialFilterInitial = spatialFilterNode.Value;

            var confidenceThresholdNode = (ArenaNET.IBoolean)device.NodeMap.GetNode("Scan3dConfidenceThresholdEnable");
            Boolean confidenceThresholdInitial = confidenceThresholdNode.Value;

            // Set pixel format
            //    Warning: HLT003S-001 / Helios2 - has only Coord3D_ABCY16 in
            //    this case This example demonstrates data interpretation for
            //    both a signed or unsigned pixel format. Default PIXEL_FORMAT
            //    here is set to Coord3D_ABCY16 but this can be modified to be a
            //    signed pixel format by changing it to Coord3D_ABCY16s.

            pixelFormatNode.FromString(PIXEL_FORMAT);

            // set operating mode distance
            operatingModeNode.FromString(heliosDeviceVersion == 2 ? "Distance3000mmSingleFreq" : "Distance1500mm");
            // set exposure time
            exposureTimeSelectorNode.FromString(settings.exposureTime);
            // set gain
            conversionGainNode.FromString(settings.conversionGain);
            // set image accumulation
            imageAccumulationNode.Value = settings.imageAccumulation;
            // enable spatial filter
            spatialFilterNode.Value = settings.spatialFilter;
            // enable confidence threshold
            confidenceThresholdNode.Value = settings.confidenceThreshold;

            // enable stream auto negotiate packet size
            var streamAutoNegotiatePacketSizeNode = (ArenaNET.IBoolean)device.TLStreamNodeMap.GetNode("StreamAutoNegotiatePacketSize");
            streamAutoNegotiatePacketSizeNode.Value = true;

            // enable stream packet resend
            var streamPacketResendEnableNode = (ArenaNET.IBoolean)device.TLStreamNodeMap.GetNode("StreamPacketResendEnable");
            streamPacketResendEnableNode.Value = true;

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

            float offsetZ = 0;

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

            var (points, intensities) = GetPointCloudUnsigned(data, size, srcPixelSize, scaleX, scaleY, scaleZ, offsetX, offsetY, offsetZ);

            // clean up example
            device.RequeueBuffer(image);
            device.StopStream();

            // return nodes to their initial values
            Console.WriteLine("Nodes were set back to initial values");
            pixelFormatNode.FromString(pixelFormatInitial);
            operatingModeNode.FromString(operatingModeInitial);
            exposureTimeSelectorNode.FromString(exposureTimeSelectorInitial);
            conversionGainNode.FromString(conversionGainNodeInitial);
            imageAccumulationNode.Value = imageAccumulationInitial;
            spatialFilterNode.Value = spatialFilterInitial;
            confidenceThresholdNode.Value = confidenceThresholdInitial;

            return (points, intensities);
        }
    }
}
