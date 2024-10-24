using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LucidArena
{
    public static class PLYFile
    {
        const String TAB1 = "  ";

        // Save: Introduction
        //    This example introduces the basic save capabilities of the save
        //    library. It shows the construction of an image parameters object
        //    and an image writer, and saves a single image.

        // =-=-=-=-=-=-=-=-=-
        // =-=- SETTINGS =-=-
        // =-=-=-=-=-=-=-=-=-

        // File name
        //    The relative path and file name to save to. After running the
        //    example, an image should exist at the location specified. The image
        //    writer chooses the file format by the image's extension. Aside from
        //    PNG (.png), images can be saved as any format available in the .NET
        //    libary.
        const String FILE_NAME = "Cs_Save_Ply/Cs_Save_Ply.ply";

        // =-=-=-=-=-=-=-=-=-
        // =-=- EXAMPLE -=-=-
        // =-=-=-=-=-=-=-=-=-

        static bool ValidateDevice(ArenaNET.IDevice device)
        {
            try
            {
                // validate if Scan3dCoordinateSelector node exists. If not -
                // probaly not Helios camera used running the example
                var checkScan3dCoordinateSelectorNode = (ArenaNET.IEnumeration)device.NodeMap.GetNode("Scan3dCoordinateSelector");
            }
            catch (Exception)
            {
                Console.WriteLine("{0}Scan3dCoordinateSelector node is not found. Please make sure that Helios device is used for the example.\n", TAB1);
                return false;
            }

            try
            {
                // validate if Scan3dCoordinateOffset node exists. If not -
                // probaly Helios has an old firmware
                var checkScan3dCoordinateOffset = (ArenaNET.IFloat)device.NodeMap.GetNode("Scan3dCoordinateOffset");
            }
            catch (Exception)
            {
                Console.WriteLine("{0}Scan3dCoordinateOffset node is not found. Please update Helios firmware.\n", TAB1);
                return false;
            }

            return true;
        }

        // demonstrates saving an image
        // (1) converts image to a displayable pixel format
        // (2) prepares image parameters
        // (3) prepares image writer
        // (4) saves image
        // (5) destroys converted image
        static void SaveImage(ArenaNET.IImage image, String filename)
        {

            bool isSignedPixelFormat = false;

            if (image.PixelFormat == ArenaNET.EPfncFormat.Coord3D_ABC16s || image.PixelFormat == ArenaNET.EPfncFormat.Coord3D_ABCY16s)
            {
                isSignedPixelFormat = true;
            }

            // Prepare image parameters
            //    An image's width, height, and bits per pixel are required to
            //    save to disk. Its size and stride (i.e. pitch) can be
            //    calculated from those 3 inputs. Notice that an image's size and
            //    stride use bytes as a unit while the bits per pixel uses bits.
            Console.WriteLine("{0}Prepare image parameters", TAB1);

            SaveNET.ImageParams parameters = new SaveNET.ImageParams(
                    image.Width,
                    image.Height,
                    image.BitsPerPixel,
                    true);

            // Prepare image writer
            //    The image writer requires 3 arguments to save an image: the
            //    image's parameters, a specified file name or pattern, and the
            //    image data to save. Providing these should result in a
            //    successfully saved file on the disk. Because an image's
            //    parameters and file name pattern may repeat, they can be passed
            //    into the image writer's constructor.
            Console.WriteLine("{0}Prepare image writer", TAB1);

            SaveNET.ImageWriter writer = new SaveNET.ImageWriter(parameters, filename);

            // set default parameters for SetPly()
            bool filterPoints = true;
            float scale = 0.25f;
            float offsetA = 0.0f;
            float offsetB = 0.0f;
            float offsetC = 0.0f;

            // set the output file format of the image writer to .ply
            writer.SetPly(".ply", filterPoints, isSignedPixelFormat, scale, offsetA, offsetB, offsetC);

            // Save
            //    Passing image data into the image writer using the Save()
            //    function triggers a save. todo - creates a dir if not already
            //    existing
            Console.WriteLine("{0}Save image", TAB1);

            writer.Save(image.DataArray, true);
        }

        // =-=-=-=-=-=-=-=-=-
        // =- PREPARATION -=-
        // =- & CLEAN UP =-=-
        // =-=-=-=-=-=-=-=-=-

        static void Main(string[] args)
        {
            Console.WriteLine("Cs_Save_Ply\n");

            try
            {
                // prepare example
                ArenaNET.ISystem system = ArenaNET.Arena.OpenSystem();
                system.UpdateDevices(100);
                if (system.Devices.Count == 0)
                {
                    Console.WriteLine("\nNo camera connected\nPress enter to complete");
                    Console.Read();
                    return;
                }
                ArenaNET.IDevice device = system.CreateDevice(system.Devices[0]);

                // enable stream auto negotiate packet size
                var streamAutoNegotiatePacketSizeNode = (ArenaNET.IBoolean)device.TLStreamNodeMap.GetNode("StreamAutoNegotiatePacketSize");
                streamAutoNegotiatePacketSizeNode.Value = true;

                // enable stream packet resend
                var streamPacketResendEnableNode = (ArenaNET.IBoolean)device.TLStreamNodeMap.GetNode("StreamPacketResendEnable");
                streamPacketResendEnableNode.Value = true;

                device.StartStream();
                ArenaNET.IImage image = device.GetImage(2000);

                bool isDeviceValid = ValidateDevice(device);

                if (isDeviceValid == true)
                {
                    if (image.PixelFormat == ArenaNET.EPfncFormat.Coord3D_ABC16 || image.PixelFormat == ArenaNET.EPfncFormat.Coord3D_ABCY16 || image.PixelFormat == ArenaNET.EPfncFormat.Coord3D_ABC16s || image.PixelFormat == ArenaNET.EPfncFormat.Coord3D_ABCY16s)
                    {
                        // run example
                        Console.WriteLine("Commence example\n");
                        SaveImage(image, FILE_NAME);
                        Console.WriteLine("\nExample complete");
                    }
                    else
                    {
                        Console.WriteLine("This example requires camera to be in a 3D image format like Coord3D_ABC16, Coord3D_ABCY16, Coord3D_ABC16s or Coord3D_ABCY16s\n");
                    }
                }

                // clean up example
                device.RequeueBuffer(image);
                device.StopStream();
                system.DestroyDevice(device);
                ArenaNET.Arena.CloseSystem(system);
            }
            catch (Exception ex)
            {
                Console.WriteLine("\nException thrown: {0}", ex.Message);
            }

            Console.WriteLine("Press enter to complete");
            Console.Read();
        }
    }
}
