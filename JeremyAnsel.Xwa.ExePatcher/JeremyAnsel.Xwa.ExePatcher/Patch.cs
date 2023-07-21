using System.Collections.Generic;

namespace JeremyAnsel.Xwa.ExePatcher
{
    public sealed class Patch
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public IList<PatchItem> Items { get; } = new List<PatchItem>();
    }
}
