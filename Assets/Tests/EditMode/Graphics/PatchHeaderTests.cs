using System.Collections.Generic;
using NUnit.Framework;

namespace Doom.Graphics.Tests
{
    public class PatchHeaderTests
    {
        [Test]
        public void Reads_size_and_offsets_from_header()
        {
            // 3x5 patch with leftOffset 1, topOffset 4, one empty column list.
            var lump = SyntheticGfxBuilder.BuildPatch(
                width: 3, height: 5, leftOffset: 1, topOffset: 4,
                columns: new List<IReadOnlyList<SyntheticGfxBuilder.Post>>());

            var h = Patch.ReadHeader(lump);

            Assert.That((h.Width, h.Height), Is.EqualTo((3, 5)));
            Assert.That((h.LeftOffset, h.TopOffset), Is.EqualTo((1, 4)));
        }
    }
}
