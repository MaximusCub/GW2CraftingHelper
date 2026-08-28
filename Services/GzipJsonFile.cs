using System.IO;
using System.IO.Compression;
using System.Text;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// The module's one gzip container implementation, factored out of
    /// PlanStore so PlanHistoryBlobStore does not fork a second copy of
    /// the same three methods. The container contract is PlanStore's:
    /// Save always writes gzip, Load sniffs the first two bytes for the
    /// gzip magic number so a plain-JSON file written before the
    /// container change still reads.
    /// </summary>
    internal static class GzipJsonFile
    {
        // Gzip's own magic number (RFC 1952 SS2.3.1) - the first two bytes
        // of every gzip member, regardless of what is inside it.
        private static readonly byte[] GzipMagicNumber = { 0x1F, 0x8B };

        internal static bool IsGzip(byte[] bytes)
        {
            return bytes != null
                && bytes.Length >= GzipMagicNumber.Length
                && bytes[0] == GzipMagicNumber[0]
                && bytes[1] == GzipMagicNumber[1];
        }

        internal static byte[] Compress(string json)
        {
            // "null in, empty file out" - preserves PlanStore's pre-gzip
            // contract (File.WriteAllText(path, null) silently wrote a
            // 0-byte file) instead of letting GetBytes(null) throw.
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json ?? string.Empty);
            using (var output = new MemoryStream())
            {
                // leaveOpen: true - GZipStream's Dispose flushes the final
                // deflate block/trailer into `output`; letting it also
                // close `output` would make the ToArray() below throw
                // ObjectDisposedException.
                using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
                {
                    gzip.Write(jsonBytes, 0, jsonBytes.Length);
                }

                return output.ToArray();
            }
        }

        internal static string DecompressToJson(byte[] gzipBytes)
        {
            using (var input = new MemoryStream(gzipBytes))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var reader = new StreamReader(gzip, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
