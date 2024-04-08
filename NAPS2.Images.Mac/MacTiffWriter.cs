using NAPS2.Util;

namespace NAPS2.Images.Mac;

internal class MacTiffWriter : ITiffWriter
{
    public bool SaveTiff(IList<IMemoryImage> images, string path,
        TiffCompressionType compression = TiffCompressionType.Auto, ProgressHandler progress = default)
    {
        var data = GetTiffData(images, compression);
        if (!data.Save(path, false, out var error))
        {
            throw new IOException(error!.Description);
        }
        return true;
    }

    public bool SaveTiff(IList<IMemoryImage> images, Stream stream,
        TiffCompressionType compression = TiffCompressionType.Auto, ProgressHandler progress = default)
    {
        var data = GetTiffData(images, compression);
        data.AsStream().CopyTo(stream);
        return true;
    }

    private static NSData GetTiffData(IList<IMemoryImage> images, TiffCompressionType compression)
    {
        NSMutableData data;
        CGImageDestination dest;
        lock (MacImageContext.ConstructorLock)
        {
            data = new NSMutableData();
            // TODO: We get a warning for UTType
#pragma warning disable CA1416,CA1422
#if MONOMAC
            dest = CGImageDestination.FromData(
#else
            dest = CGImageDestination.Create(
#endif
                data, UTType.TIFF, images.Count)!;
        }
        foreach (var image in images)
        {
            var comp = compression switch
            {
                TiffCompressionType.None => NSTiffCompression.None,
                TiffCompressionType.Ccitt4 => NSTiffCompression.CcittFax4,
                TiffCompressionType.Lzw => NSTiffCompression.Lzw,
                _ => image.PixelFormat == ImagePixelFormat.BW1
                    ? NSTiffCompression.CcittFax4
                    : NSTiffCompression.Lzw
            };
            var imageToWrite = (MacImage) image;
            if (comp == NSTiffCompression.CcittFax4 && image.PixelFormat != ImagePixelFormat.BW1)
            {
                imageToWrite = (MacImage) image.Clone().PerformTransform(new BlackWhiteTransform());
            }
            try
            {
                NSDictionary props;
                lock (MacImageContext.ConstructorLock)
                {
                    props = NSDictionary.FromObjectAndKey(
                        new NSNumber((int) comp),
                        NSBitmapImageRep.CompressionMethod);
                }
                dest.AddImage(imageToWrite.Rep.CGImage, props);
            }
            finally
            {
                if (imageToWrite != image)
                {
                    imageToWrite.Dispose();
                }
            }
        }
        if (!dest.Close())
        {
            throw new IOException("Error finalizing CGImageDestination");
        }
        return data;
    }
}