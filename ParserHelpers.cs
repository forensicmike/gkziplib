using System;
using System.IO;

namespace GKZipLib
{
    static public class ParserHelpers
    {
        static public int ParseInt16(this FileStream fs, long startOffset)
        {
            fs.Seek(startOffset, SeekOrigin.Begin);
            var buff = new byte[2];
            fs.Read(buff, 0, 2);
            return BitConverter.ToInt16(buff, 0);
        }
        static public int ParseInt32(this FileStream fs, long startOffset)
        {
            fs.Seek(startOffset, SeekOrigin.Begin);
            var buff = new byte[4];
            fs.Read(buff, 0, 4);
            return BitConverter.ToInt32(buff, 0);
        }
        static public byte[] GetBytes(this FileStream fs, long startOffset, int byteCount)
        {
            var ret = new byte[byteCount];

            fs.Seek(startOffset, SeekOrigin.Begin);
            fs.Read(ret, 0, byteCount);

            return ret;
        }

        static public string GetTempExtractPath()
        {
            var ret = System.IO.Path.GetTempPath() + "\\_gkfastview\\";
            if (!Directory.Exists(ret))
                Directory.CreateDirectory(ret);
            return ret;
        }
    }
}
