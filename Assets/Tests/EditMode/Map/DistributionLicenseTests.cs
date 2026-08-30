using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Doom.Map.Tests
{
    /// The standalone build ships a Licenses/ folder (Stage7BuildMenu):
    /// Freedoom's BSD notice and the LGPL text for the Nuked OPL3 port are
    /// redistribution requirements. These pin the license sources on disk so
    /// a moved or deleted file fails the suite, not just the build preflight.
    public class DistributionLicenseTests
    {
        static string Root => Path.GetDirectoryName(Application.dataPath);

        // Mirrors Stage7BuildMenu.DistributionLicenses (editor-only class, so
        // the list is pinned here by value; the build preflight enforces the
        // same set at build time).
        static readonly (string SourcePath, string ShipName)[] Licenses =
        {
            ("Distribution/NOTICES.txt", "NOTICES.txt"),
            ("Distribution/FREEDOOM-LICENSE.txt", "FREEDOOM-LICENSE.txt"),
            ("Assets/ThirdParty/LibTessDotNet/LICENSE.txt", "LIBTESSDOTNET-LICENSE.txt"),
            ("Assets/ThirdParty/NukedOpl/LICENSE.txt", "NUKEDOPL-LICENSE.txt"),
            ("Assets/ThirdParty/SuperXbr/LICENSE.txt", "SUPERXBR-LICENSE.txt"),
        };

        [Test]
        public void Every_distribution_license_source_exists()
        {
            foreach (var (sourcePath, _) in Licenses)
                Assert.That(File.Exists(Path.Combine(Root, sourcePath)), Is.True,
                    $"missing license source '{sourcePath}' — the build ships it");
        }

        [Test]
        public void Notices_reference_every_shipped_license_file()
        {
            string notices = File.ReadAllText(
                Path.Combine(Root, "Distribution", "NOTICES.txt"));
            foreach (var (_, shipName) in Licenses)
            {
                if (shipName == "NOTICES.txt") continue;
                Assert.That(notices, Does.Contain(shipName),
                    $"NOTICES.txt must point the reader at {shipName}");
            }
        }

        /// A future vendored library must bring its license along — every
        /// folder under Assets/ThirdParty carries a LICENSE.txt, and every
        /// one of them is in the shipped set above.
        [Test]
        public void Every_vendored_library_ships_its_license()
        {
            string thirdParty = Path.Combine(Application.dataPath, "ThirdParty");
            foreach (string dir in Directory.GetDirectories(thirdParty))
            {
                string name = Path.GetFileName(dir);
                Assert.That(File.Exists(Path.Combine(dir, "LICENSE.txt")), Is.True,
                    $"Assets/ThirdParty/{name} has no LICENSE.txt");

                string source = $"Assets/ThirdParty/{name}/LICENSE.txt";
                bool shipped = false;
                foreach (var (sourcePath, _) in Licenses)
                    if (sourcePath == source) { shipped = true; break; }
                Assert.That(shipped, Is.True,
                    $"{source} is not in the shipped license set — add it to " +
                    "Stage7BuildMenu.DistributionLicenses and to this test");
            }
        }
    }
}
