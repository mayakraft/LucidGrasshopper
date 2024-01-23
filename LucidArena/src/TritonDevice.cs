using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LucidArena
{
    internal class TritonDevice
    {
        const String TAB1 = "  ";

        // Save: Bitmap
        //    This example introduces basic save capabilities using Microsoft's
        //    .NET framework for saving images. It shows the construction of a
        // bitmap object from the pixel data of an image and then saves it to the
        // specified file path.

        // =-=-=-=-=-=-=-=-=-
        // =-=- SETTINGS =-=-
        // =-=-=-=-=-=-=-=-=-

        // File name
        //    The relative path and file name to save to. After running the
        //    example, an image should exist at the location specified. The image
        //    writer chooses the file format by the image's extension. Aside from
        //    PNG (.png), images can be saved as any format available in the .NET
        //    libary.
        const String PATH = "Cs_Save_Bitmap";
        const String FILE_NAME = "image.png";
        const String FULL_FILE_NAME = PATH + "\\" + FILE_NAME;

        // =-=-=-=-=-=-=-=-=-
        // =-=- EXAMPLE -=-=-
        // =-=-=-=-=-=-=-=-=-

        // demonstrates saving an image
        // (1) converts image to a displayable pixel format
        // (2) prepares image parameters
        // (3) prepares image writer
        // (4) saves image
        // (5) destroys converted image
        static public void SaveImage(ArenaNET.IImage image)
        {
            // Get bitmap
            //    ArenaNET leverages Microsoft's .NET framework for saving
            //    images. Simply ensure the System.Drawing reference in order to
            //    grab the bitmap. Internally, grabbing an image's bitmap
            //    converts it into the BGRa8 format, which coincides with
            //    Microsoft's 32-bit RGB format
            //    (System.Drawing.Imaging.PixelFormat.Format32bppRgb). Note that
            //    the bitmap data is a shallow copy of the image data, and so the
            //    bitmap is only valid for the lifecycle of its parent image.
            Console.WriteLine("{0}Get bitmap", TAB1);

            System.Drawing.Bitmap bitmap = image.Bitmap;

            // Create directory
            //    Check whether the path exists and if not, create it. The .NET
            //    framework requires that paths already exist before saving to
            //    them.
            Console.WriteLine("{0}Create {1} directory", TAB1, PATH);

            if (!System.IO.Directory.Exists(PATH))
            {
                System.IO.Directory.CreateDirectory(PATH);
            }

            // Save
            //    Because the bitmap is preconverted to a displayable format, the
            //    image will be saved to a displayable format without the need of
            //    a conversion.
            Console.WriteLine("{0}Save image {1}", TAB1, FULL_FILE_NAME);

            bitmap.Save(
            FULL_FILE_NAME,
            System.Drawing.Imaging.ImageFormat.Png);
        }

    }
}
