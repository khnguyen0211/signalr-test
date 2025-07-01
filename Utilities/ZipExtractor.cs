using System.IO.Compression;

namespace WarpBootstrap.Utilities
{
    public class ZipExtractor
    {
        public void Extract(string zipFilePath, string destinationFolder)
        {
            if (Directory.Exists(destinationFolder))
            {
                Directory.Delete(destinationFolder, recursive: true);
            }

            ZipFile.ExtractToDirectory(zipFilePath, destinationFolder);
        }
    }
}
