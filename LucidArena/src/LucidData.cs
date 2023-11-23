using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using ArenaNET;

namespace LucidArena
{
    public static class LucidData
    {
        const UInt32 IMAGE_TIMEOUT = 2000;
        const UInt32 UPDATE_TIMEOUT = 100;

        //public static string GetImage()
        //{
        //    if (!LucidManager.SystemIsOpen || !LucidManager.DeviceIsConnected) {
        //        return "must connect to device to get image data";
        //    }

        //    //public static ArenaNET.ISystem system;
        //    //public static ArenaNET.IDevice device;

        //    // Acquire image
        //    //    Once a device is created, it is only a single call to acquire
        //    //    an image. The timeout must be larger than the exposure time.
        //    Console.WriteLine("Acquire image");

        //    LucidManager.device.StartStream();
        //    IImage image = LucidManager.device.GetImage(IMAGE_TIMEOUT);

        //    var info = $"{image.Width} {image.Height}";

        //    // Clean up
        //    //    Clean up each of the 3 objects in reverse order: image, device,
        //    //    and system. The list of devices is a standard vector, so it
        //    //    cleans itself up at the end of scope.
        //    Console.WriteLine("Clean up Arena");

        //    LucidManager.device.RequeueBuffer(image);
        //    LucidManager.device.StopStream();

        //    var info2 = $"{image.Width} {image.Height}";
        //    return $"image successful {info} {info2}";
        //}

        public static string GetImagesAsBitmap()
        {
            if (!LucidManager.SystemIsOpen || !LucidManager.DeviceIsConnected)
            {
                return "must connect to device to get image data";
            }

            List<string> results = LucidManager.devices.Select(device =>
            {
                device.StartStream();
                IImage image = device.GetImage(IMAGE_TIMEOUT);

                var info = $"{image.Width} {image.Height}";

                device.RequeueBuffer(image);
                device.StopStream();
                return $"image successful {info}";
            }).ToList();

            return string.Join("\n", results);
        }

        //public static (List<Point3d> points, List<Color> colors) GetImageAsPoints()
        //{
        //    if (!LucidManager.SystemIsOpen || !LucidManager.DeviceIsConnected)
        //    {
        //        return "must connect to device to get image data";
        //    }

        //    Console.WriteLine("Acquire image");

        //    LucidManager.device.StartStream();
        //    IImage image = LucidManager.device.GetImage(IMAGE_TIMEOUT);

        //    var info = $"{image.Width} {image.Height}";

        //    LucidManager.device.RequeueBuffer(image);
        //    LucidManager.device.StopStream();

        //    // var info2 = $"{image.Width} {image.Height}";
        //    return $"image successful {info}";
        //}

        // public static void ConnectDevice(int index) { }

        public static void EnumerateDeviceAndAcquireImage()
        {
            // Enumerate device
            //    Starting Arena just requires opening the system. From there,
            //    update and grab the device list, and create the device. Notice
            //    that failing to update the device list will return an empty
            //    list, even if devices are connected.

            ArenaNET.ISystem system = ArenaNET.Arena.OpenSystem();
            system.UpdateDevices(UPDATE_TIMEOUT);
            if (system.Devices.Count == 0)
            {
                Console.WriteLine("\nNo camera connected\nPress enter to complete");
                Console.Read();
                return;
            }
            Console.WriteLine("Enumerate device");
            ArenaNET.IDevice device = system.CreateDevice(system.Devices[0]);

            // enable stream auto negotiate packet size
            var streamAutoNegotiatePacketSizeNode = (ArenaNET.IBoolean)device.TLStreamNodeMap.GetNode("StreamAutoNegotiatePacketSize");
            streamAutoNegotiatePacketSizeNode.Value = true;

            // enable stream packet resend
            var streamPacketResendEnableNode = (ArenaNET.IBoolean)device.TLStreamNodeMap.GetNode("StreamPacketResendEnable");
            streamPacketResendEnableNode.Value = true;

            // Acquire image
            //    Once a device is created, it is only a single call to acquire
            //    an image. The timeout must be larger than the exposure time.
            Console.WriteLine("Acquire image");

            device.StartStream();
            ArenaNET.IImage image = device.GetImage(IMAGE_TIMEOUT);

            // Clean up
            //    Clean up each of the 3 objects in reverse order: image, device,
            //    and system. The list of devices is a standard vector, so it
            //    cleans itself up at the end of scope.
            Console.WriteLine("Clean up Arena");

            device.RequeueBuffer(image);
            device.StopStream();
            system.DestroyDevice(device);
            ArenaNET.Arena.CloseSystem(system);
        }
    }
}
