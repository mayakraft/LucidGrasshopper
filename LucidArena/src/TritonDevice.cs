using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LucidArena
{
    public static class TritonDevice
    {
        const String PATH = "Cs_Save_Bitmap";
        const String FILE_NAME = "image.png";
        const String FULL_FILE_NAME = PATH + "\\" + FILE_NAME;

        static public void SaveImage(ArenaNET.IImage image, string filepath)
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
            System.Drawing.Bitmap bitmap = image.Bitmap;
            if (!System.IO.Directory.Exists(PATH)) System.IO.Directory.CreateDirectory(PATH);
            // Save
            //    Because the bitmap is preconverted to a displayable format, the
            //    image will be saved to a displayable format without the need of
            //    a conversion.
            bitmap.Save(filepath, System.Drawing.Imaging.ImageFormat.Png);
        }

    }
}
