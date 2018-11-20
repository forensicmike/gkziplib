using System;
using System.IO;

namespace GKZipLib
{
    public class CDEntry
    {
        public GKZipFile Parent { get; set; }

        // We won't auto parse EVERYTHING about each entry. Basically just the file name and the start offset, relative to the CD.
        public long RelativeOffset { get; set; }
        public long AbsoluteOffset { get; set; }
        public string Name { get; set; }

        public string ShortName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Name))
                    return string.Empty;
                else
                    return Name.Substring(Name.LastIndexOf('/') + 1);
            }
        }

        public string DictionaryKey { get; set; }

        public int LengthFileNameN { get; set; }
        public int LengthExtraFieldM { get; set; }
        public int LengthCommentK { get; set; }

        public int CompressedSize { get; set; }
        public int UncompressedSize { get; set; }

        // This only gets resolved for noteworthy entries, created as nullable as a reminder
        public long? FileEntryOffset { get; set; }

        /// <summary>
        /// Same as ExtractTo but uses the file's existing name within the archive.
        /// </summary>
        /// <param name="outputFolderPath">Target output path</param>
        public void ExtractToFolder(string outputFolderPath)
        {
            if (!Directory.Exists(outputFolderPath))
            {
                var di = Directory.CreateDirectory(outputFolderPath);
                if (di == null)
                {
                    throw new Exception("Directory issue? Doesn't exist and unable to create..");
                }
            }
            else
            {
                if (!outputFolderPath.EndsWith("\\"))
                    outputFolderPath += "\\";
                ExtractTo(outputFolderPath + ShortName);
            }
        }

        /// <summary>
        /// This only works if we've populated our CentralDirectory property (set the second parameter of parse call to true)
        /// </summary>
        /// <returns>Successful or not</returns>
        public bool GetFileEntryOffset()
        {
            if (FileEntryOffset != null)
                return true;

            CompressedSize = BitConverter.ToInt32(Parent.CentralDirectory, (int)RelativeOffset + 20);
            UncompressedSize = BitConverter.ToInt32(Parent.CentralDirectory, (int)RelativeOffset + 24);

            var cur = 0;
            var bFound = false;
            while (cur < LengthExtraFieldM)
            {
                var relPos = (int)RelativeOffset + 46 + LengthFileNameN;
                var length = BitConverter.ToInt16(Parent.CentralDirectory, cur + relPos + 2);

                if (Parent.CentralDirectory[relPos + cur] == 0x1 && Parent.CentralDirectory[relPos + cur + 1] == 0x00)
                {
                    FileEntryOffset = BitConverter.ToInt64(Parent.CentralDirectory, cur + relPos + 4);
                    bFound = true;
                }
                cur += length + 4;
            }
            
            GKZipFile.DebugLog($"RE: {Name}");
            if (!bFound)
            {
                FileEntryOffset = (long)BitConverter.ToUInt32(Parent.CentralDirectory, (int)RelativeOffset + 42);
                GKZipFile.DebugLog($"File absolute offset is (loop failed; fallback value): {FileEntryOffset}");
                return false;
            }
            else
            {
                GKZipFile.DebugLog($"File absolute offset is {FileEntryOffset}");
                return true;
            }
        }

        

        public void ExtractTo(string outputPath)
        {
            GKZipFile.DebugLog($"Extracting {Name} to {outputPath}");

            if (FileEntryOffset == null)
                GetFileEntryOffset();

            using (var fs = new FileStream(Parent.ZIPPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fs.Seek(FileEntryOffset.Value + 26, SeekOrigin.Begin);
                var lengthBytes = new byte[4];
                fs.Read(lengthBytes, 0, 4);
                var n = BitConverter.ToInt16(lengthBytes, 0);
                var m = BitConverter.ToInt16(lengthBytes, 2);

                var targetOffset = FileEntryOffset.Value + 30 + n + m;
                GKZipFile.DebugLog($"Seeking to {targetOffset} ({FileEntryOffset} + 30 + {n} + {m}");
                fs.Seek(targetOffset, SeekOrigin.Begin);
                using (var outputStream = new FileStream(outputPath, FileMode.Create))
                {
                    var bytesToRead = 2048;
                    var buffer = new byte[bytesToRead];
                    var totalBytesRead = 0;
                    
                    while (totalBytesRead < CompressedSize)
                    {
                        if (totalBytesRead + bytesToRead > CompressedSize)
                        {
                            bytesToRead = CompressedSize - totalBytesRead;
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

        public byte[] ExtractToMemory()
        {
            GKZipFile.DebugLog($"Extracting {Name} to memory");

            if (FileEntryOffset == null)
                GetFileEntryOffset();

            using (var fs = new FileStream(Parent.ZIPPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fs.Seek(FileEntryOffset.Value + 26, SeekOrigin.Begin);
                var lengthBytes = new byte[4];
                fs.Read(lengthBytes, 0, 4);
                var n = BitConverter.ToInt16(lengthBytes, 0);
                var m = BitConverter.ToInt16(lengthBytes, 2);

                var targetOffset = FileEntryOffset.Value + 30 + n + m;
                GKZipFile.DebugLog($"Seeking to {targetOffset} ({FileEntryOffset} + 30 + {n} + {m}");
                fs.Seek(targetOffset, SeekOrigin.Begin);
                using (var outputStream = new MemoryStream())
                {
                    var bytesToRead = 2048;
                    var buffer = new byte[bytesToRead];
                    var totalBytesRead = 0;

                    while (totalBytesRead < CompressedSize)
                    {
                        if (totalBytesRead + bytesToRead > CompressedSize)
                        {
                            bytesToRead = CompressedSize - totalBytesRead;
                            buffer = new byte[bytesToRead]; // adjust the length of the buffer
                        }

                        fs.Read(buffer, 0, bytesToRead);
                        outputStream.Write(buffer, 0, bytesToRead);
                        totalBytesRead += bytesToRead;
                    }
                    outputStream.Flush();
                    outputStream.Close();
                    fs.Close();
                    return outputStream.ToArray();
                }
                
            }
        }
    }
}
