using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GKZipLib
{
    
    static public class BatchExtraction
    {
        /// <summary>
        /// In batch extraction, we provide a list of input files to search and perform extractions from based on a predicate.
        /// </summary>
        /// <param name="inputFiles"></param>
        /// <param name="outputDirectoryPath"></param>
        /// <param name="predicate"></param>
        static public void BatchExtract(string[] inputFiles, string outputDirectoryPath, Func<CDEntry, bool> predicate)
        {
            var outputDirectory = default(DirectoryInfo);
            if (!Directory.Exists(outputDirectoryPath))
            {
                outputDirectory = Directory.CreateDirectory(outputDirectoryPath);
            }
            else
            {
                outputDirectory = new DirectoryInfo(outputDirectoryPath);
            }

            var activeTasks = new List<Task>();
            foreach (var item in inputFiles)
            {
                var inputFile = new FileInfo(item);
                var match = Regex.Match(inputFile.Name, @"(?<udid>[\w\W]*?)_files\.zip");
                if (!match.Success)
                    throw new Exception($"Is this even a GK Zip? {inputFile.Name}");

                
                var udid = match.Groups["udid"].Value;
                Directory.CreateDirectory(outputDirectory.FullName + "\\" + udid);

                activeTasks.Add(Task.Factory.StartNew(() =>
                {
                    var gkz = new GKZipFile(item, false);
                    var sw = new Stopwatch();
                    sw.Start();
                    GKZipFile.DebugLog($"Starting item with udid {udid} {item}");
                    var reviewedEntries = 0;
                    foreach (var entry in gkz)
                    {
                        if (predicate(entry))
                        {
                            entry.ExtractToFolder(outputDirectory.FullName + "\\" + udid + "\\");
                            GKZipFile.DebugLog($"Extracted {entry.Name} to .\\{udid}");
                        }
                        reviewedEntries++;
                    }
                    sw.Stop();
                    GKZipFile.DebugLog($"({udid}) - Work completed in {sw.ElapsedMilliseconds}ms ({reviewedEntries} entries)");
                }));
            }

            Task.WaitAll(activeTasks.ToArray());
        }
    }
}
