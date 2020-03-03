using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MikeForensicLib.SQLite;

namespace GKZipLib
{
    public class GKZipFile: IEnumerable<CDEntry>
    {
        /// <summary>
        /// Parameterless constructor is intended to be used with the Parse method. 
        /// </summary>
        public GKZipFile()
        {
            CDEntries = new List<CDEntry>();
        }
        /// <summary>
        /// Don't forget to set options first
        /// </summary>
        /// <param name="path"></param>
        public GKZipFile(string path, bool bStoreEntries)
        {
            CDEntries = new List<CDEntry>();
            ZIPPath = path;
            StoringEntries = bStoreEntries;
        }

        public bool StoringEntries { get; set; }
        public event EventHandler<CDEntryEventArgs> OnCDEntryParsed = (o, e) => { };
        public bool IsParsingCompleted { get; set; }
        public event EventHandler CDParsingCompleted = (o, e) => { };
        public void TriggerParsingCompleted()
        {
            CDParsingCompleted(this, new EventArgs());
            IsParsingCompleted = true;
        }

        // The EOCD, or End Of Central Directory, is essentially the marker for the end of the file
        public long EOCDOffset { get; set; }

        // How large is the Central Directory (the EOCD tells us this)
        public int CDSize { get; set; }

        // Overall list of Central Directory entries. Somewhere around 600,000 for a 20GB GK Zip.
        public List<CDEntry> CDEntries { get; set; }

        public byte[] CentralDirectory { get; set; }

        public string ZIPPath { get; set; }

        // Haven't figured out how to do this without borrowing code from the Parse function - compiler wont allow yield return in a lambda.
        public IEnumerator<CDEntry> GetEnumerator()
        {
            using (var fs = new FileStream(ZIPPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                EOCDOffset = LocateEOCD(fs);

                if (EOCDOffset == 0)
                {
                    throw new Exception("Unable to locate EOCD");
                }

                CDSize = fs.ParseInt32(EOCDOffset + 12);

                if (!fs.GetBytes(EOCDOffset - 76, 4).SequenceEqual(SeventySixHeader))
                {
                    Console.WriteLine("Warning: 0606 not found at EOCD-76");
                }

                var cdStartOffset = EOCDOffset - CDSize - 76;
                fs.Seek(cdStartOffset, SeekOrigin.Begin);

                CentralDirectory = new byte[CDSize];
                fs.Read(CentralDirectory, 0, CDSize);

                var curPos = 0;
                while (curPos < CentralDirectory.Length)
                {
                    if (!CentralDirectoryHeader.SequenceEqual(CentralDirectory.Take(4)))
                    {
                        throw new Exception("Some sort of CentralDirectory parsing issue");
                    }

                    var n = BitConverter.ToInt16(CentralDirectory, curPos + 28);
                    var m = BitConverter.ToInt16(CentralDirectory, curPos + 30);
                    var k = BitConverter.ToInt16(CentralDirectory, curPos + 32);

                    var cd = new CDEntry()
                    {
                        Parent = this,
                        RelativeOffset = curPos,
                        AbsoluteOffset = cdStartOffset + curPos,
                        Name = ASCIIEncoding.ASCII.GetString(CentralDirectory, curPos + 46, n),
                        LengthFileNameN = n,
                        LengthExtraFieldM = m,
                        LengthCommentK = k,
                        CompressionMethod = BitConverter.ToInt16(CentralDirectory, curPos + 10),
                    };


                    if (StoringEntries)
                        CDEntries.Add(cd);

                    curPos += 46 + n + m + k;

                    yield return cd;


                    
                }
                fs.Close();
                TriggerParsingCompleted();

                if (!StoringEntries)
                    CentralDirectory = null;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool DebugToConsole
        {
            get
            {
                return GKZipFile.bDebugToConsole;
            }
            set
            {
                GKZipFile.bDebugToConsole = value;
            }
        }
        
        /// <summary>
        /// This is an alternative way to invoke parsing without having to set the OnCDEntryParsed event first.
        /// </summary>
        /// <param name="inputPath"></param>
        /// <param name="bStoreEntries"></param>
        /// <param name="entryParsedAction"></param>
        public void Parse(string inputPath, bool bStoreEntries, Action<CDEntry> entryParsedAction)
        {
            OnCDEntryParsed += (o, e) => { entryParsedAction(e.Entry); };
            Parse(inputPath, bStoreEntries);
        }

        static public List<dynamic> Query(string path, string dbPath, string sql, dynamic parameters)
        {
            var ret = new List<dynamic>();
            var inputFi = new FileInfo(path);
            var extractionSuccess = false;
            foreach (var cdentry in new GKZipFile(path, false))
            {
                if (cdentry.Name.IndexOf(dbPath, 0, StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    // extract it
                    cdentry.ExtractTo(inputFi.Directory.FullName + "\\" + cdentry.ShortName);
                    extractionSuccess = true;
                }
            }

            if (extractionSuccess)
            {
                using (var con = new SQLiteConnection(@"Data Source=" + inputFi.Directory.FullName + "\\" + dbPath.Split('/').Last()))
                {
                    con.Open();

                    ret.AddRange(con.Query(sql, (object)parameters));

                    con.Close();
                }
            }

            return ret;
        }

        /// <summary>
        /// Parse a ZIP file. Be sure to configure OnCDEntryParsed before running this method, or use the alternate 3-parameter form of this method.
        /// </summary>
        /// <param name="inputPath"></param>
        /// <param name="bStoreEntries">When true, parsed Central Directory entries will be stored in memory.</param>
        public void Parse(string inputPath, bool bStoreEntries)
        {
            ZIPPath = inputPath;
            using (var fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                EOCDOffset = LocateEOCD(fs);

                if (EOCDOffset == 0)
                {
                    throw new Exception("Unable to locate EOCD");
                }

                CDSize = fs.ParseInt32(EOCDOffset + 12);

                if (!fs.GetBytes(EOCDOffset - 76, 4).SequenceEqual(SeventySixHeader))
                {
                    throw new Exception("0606 not found at EOCD-76");
                }

                var cdStartOffset = EOCDOffset - CDSize - 76;
                fs.Seek(cdStartOffset, SeekOrigin.Begin);

                CentralDirectory = new byte[CDSize];
                fs.Read(CentralDirectory, 0, CDSize);

                var curPos = 0;
                while (curPos < CentralDirectory.Length)
                {
                    if (!CentralDirectoryHeader.SequenceEqual(CentralDirectory.Take(4)))
                    {
                        throw new Exception("SHIT! CentralDirectory parsing issue");
                    }

                    var n = BitConverter.ToInt16(CentralDirectory, curPos + 28);
                    var m = BitConverter.ToInt16(CentralDirectory, curPos + 30);
                    var k = BitConverter.ToInt16(CentralDirectory, curPos + 32);

                    var cd = new CDEntry()
                    {
                        Parent = this,
                        RelativeOffset = curPos,
                        AbsoluteOffset = cdStartOffset + curPos,
                        Name = ASCIIEncoding.ASCII.GetString(CentralDirectory, curPos + 46, n),
                        LengthFileNameN = n,
                        LengthExtraFieldM = m,
                        LengthCommentK = k,
                        CompressionMethod = BitConverter.ToInt16(CentralDirectory, curPos + 10),
                    };

                    OnCDEntryParsed(this, new CDEntryEventArgs() { Entry = cd });


                    if (StoringEntries)
                        CDEntries.Add(cd);

                    curPos += 46 + n + m + k;
                }
                fs.Close();
                TriggerParsingCompleted();

                if (!StoringEntries)
                    CentralDirectory = null;
            }
        }

        static public bool bDebugToConsole = true;
        static public void DebugLog(string info)
        {
            if (bDebugToConsole)
                Console.WriteLine(info);
        }

        static public readonly byte[] LocalFileHeader = new byte[] { 0x50, 0x4b, 0x03, 0x04 };
        static public readonly byte[] DataDescriptor = new byte[] { 0x50, 0x4b, 0x07, 0x08 };
        static public readonly byte[] SeventySixHeader = new byte[] { 0x50, 0x4b, 0x06, 0x06 };
        static public readonly byte[] CentralDirectoryHeader = new byte[] { 0x50, 0x4b, 0x01, 0x02 };
        static public readonly byte[] EndOfCentralDirectoryHeader = new byte[] { 0x50, 0x4b, 0x05, 0x06 };

        static public long GetAbsoluteOffset(long totalLength, long currentPosition, int offset)
        {
            return totalLength - currentPosition + offset;
        }

        static public long LocateEOCD(FileStream fs)
        {
            var segmentSize = 5000;
            var buffer = new byte[segmentSize];
            long totalProgress = 0;

            while (segmentSize + totalProgress < fs.Length)
            {
                fs.Seek(-(segmentSize + totalProgress), SeekOrigin.End);
                fs.Read(buffer, 0, segmentSize);
                totalProgress += segmentSize;

                for (int i = 0; i < buffer.Length; i++)
                {
                    if (EndOfCentralDirectoryHeader.SequenceEqual(buffer.Skip(i).Take(4)))
                        return GetAbsoluteOffset(fs.Length, totalProgress, i);
                }
            }
            return 0;
        }

        /// <summary>
        /// Single file extraction. Perhaps we should also add a way of doing this in a batch?
        /// That way we only use a single FileStream. We could sort by offsets to minimize seeking as well.
        /// </summary>
        /// <param name="zipPath"></param>
        /// <param name="absOffset"></param>
        /// <param name="compressedSize"></param>
        /// <param name="outputPath"></param>
        static public void ExtractTo(string zipPath, long absOffset, int compressedSize, string outputPath)
        {
            using (var fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fs.Seek(absOffset + 26, SeekOrigin.Begin);
                var lengthBytes = new byte[4];
                fs.Read(lengthBytes, 0, 4);
                var n = BitConverter.ToInt16(lengthBytes, 0);
                var m = BitConverter.ToInt16(lengthBytes, 2);

                var targetOffset = absOffset + 30 + n + m;
                fs.Seek(targetOffset, SeekOrigin.Begin);
                using (var outputStream = new FileStream(outputPath, FileMode.Create))
                {
                    var bytesToRead = 2048;
                    var buffer = new byte[bytesToRead];
                    var totalBytesRead = 0;

                    while (totalBytesRead < compressedSize)
                    {
                        if (totalBytesRead + bytesToRead > compressedSize)
                        {
                            bytesToRead = compressedSize - totalBytesRead;
                            buffer = new byte[bytesToRead]; // adjust the length of the buffer
                        }

                        fs.Read(buffer, 0, bytesToRead);
                        outputStream.Write(buffer, 0, bytesToRead);
                        totalBytesRead += bytesToRead;
                    }
                    outputStream.Flush();
                    outputStream.Close();
                }
                fs.Close();
            }
        }
    }
}
