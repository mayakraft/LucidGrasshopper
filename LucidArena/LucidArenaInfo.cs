using Grasshopper;
using Grasshopper.Kernel;
using System;
using System.Drawing;

namespace LucidArena
{
    public class LucidArenaInfo : GH_AssemblyInfo
    {
        public override string Name => "LucidArena";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "";

        public override Guid Id => new Guid("4eed83d2-0b5c-413a-8bcc-dab3334c35c4");

        //Return a string identifying you or your company.
        public override string AuthorName => "Univ. Innsbruck, i.sd";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "";
    }
}