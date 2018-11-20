using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GKZipLib
{
    static public class Example
    {
        static public void RunExample(string pathToZip)
        {
            var sw = new Stopwatch();

            var inputPath = pathToZip;

            // Determine zip size in GB
            var gb = new FileInfo(inputPath).Length / 1000 / 1000 / 1000;
            Console.WriteLine($"Examining {gb}GB zip...");

            // Note the use of the new constructor here, this is because we are no longer calling the .Parse method explicitly
            var gkzip = new GKZipLib.GKZipFile(inputPath, false);

            // Let's count every file in the Zip using LINQ
            sw.Start();
            Console.WriteLine($"There are {gkzip.Count()} entries in the zip.");
            sw.Stop();
            // How long did it take?
            Console.WriteLine($"Parsing completed in {sw.ElapsedMilliseconds}ms");

            // Round 2...
            sw.Reset();

            // Now let's selectively pick out all the files that end in .plist
            sw.Start();
            var plistPaths = new List<string>();
            foreach (var item in gkzip.Where(x => x.Name.EndsWith(".plist")))
            {
                plistPaths.Add(item.Name);
            }
            Console.WriteLine($"There are {plistPaths.Count} files ending in '.plist' in the zip.");
            sw.Stop();
            Console.WriteLine($"Parsing completed in {sw.ElapsedMilliseconds}ms");
        }
    }
}
