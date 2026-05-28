using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Doom.Wad.Editor
{
    public static class WadInspectorMenu
    {
        private const string WadRelativePath = "wads/freedoom1.wad";

        [MenuItem("Tools/Doom/Dump freedoom1.wad")]
        public static void DumpFreedoom1()
        {
            var path = Path.Combine(Application.streamingAssetsPath, WadRelativePath);
            if (!File.Exists(path))
            {
                Debug.LogError($"WAD not found at {path}");
                return;
            }

            using var wad = WadFile.Open(path);
            var sb = new StringBuilder();
            sb.AppendLine($"WAD: {path}");
            sb.AppendLine($"Signature: {wad.Header.Signature}");
            sb.AppendLine($"Lumps: {wad.Directory.Count}");

            var maps = new List<string>();
            for (int i = 0; i < wad.Directory.Count; i++)
            {
                if (WadMapNames.IsMapMarker(wad.Directory[i].Name))
                    maps.Add(wad.Directory[i].Name);
            }
            sb.AppendLine($"Maps ({maps.Count}): {string.Join(", ", maps)}");

            sb.AppendLine();
            sb.AppendLine("Directory:");
            for (int i = 0; i < wad.Directory.Count; i++)
            {
                var e = wad.Directory[i];
                sb.AppendLine($"  [{i,4}] {e.Name,-8}  offset={e.Offset,10}  size={e.Size,8}");
            }

            Debug.Log(sb.ToString());
        }
    }
}
