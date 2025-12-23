using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using PDFiumCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace PDFiumCoreDemo
{
    public static class fpdftext
    {
        [DllImport("pdfium")]
        public static extern IntPtr FPDFText_LoadPage(IntPtr page);

        [DllImport("pdfium")]
        public static extern void FPDFText_ClosePage(IntPtr text_page);

        [DllImport("pdfium")]
        public static extern int FPDFText_CountChars(IntPtr text_page);

        [DllImport("pdfium")]
        public static extern int FPDFText_GetText(IntPtr text_page, int start_index, int count, [Out] ushort[] result);
    }

    /// <summary>
    /// A MemoryManager over a raw pointer
    /// </summary>
    /// <remarks>The pointer is assumed to be fully unmanaged, or externally pinned - no attempt will be made to pin this data</remarks>
    public sealed unsafe class UnmanagedMemoryManager<T> : MemoryManager<T>
        where T : unmanaged
    {
        private readonly T* _pointer;
        private readonly int _length;

        /// <summary>
        /// Create a new UnmanagedMemoryManager instance at the given pointer and size
        /// </summary>
        /// <remarks>It is assumed that the span provided is already unmanaged or externally pinned</remarks>
        public UnmanagedMemoryManager(Span<T> span)
        {
            fixed (T* ptr = &MemoryMarshal.GetReference(span))
            {
                _pointer = ptr;
                _length = span.Length;
            }
        }
        /// <summary>
        /// Create a new UnmanagedMemoryManager instance at the given pointer and size
        /// </summary>
        public UnmanagedMemoryManager(T* pointer, int length)
        {
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            _pointer = pointer;
            _length = length;
        }
        /// <summary>
        /// Obtains a span that represents the region
        /// </summary>
        public override Span<T> GetSpan() => new Span<T>(_pointer, _length);

        /// <summary>
        /// Provides access to a pointer that represents the data (note: no actual pin occurs)
        /// </summary>
        public override MemoryHandle Pin(int elementIndex = 0)
        {
            if (elementIndex < 0 || elementIndex >= _length)
                throw new ArgumentOutOfRangeException(nameof(elementIndex));
            return new MemoryHandle(_pointer + elementIndex);
        }
        /// <summary>
        /// Has no effect
        /// </summary>
        public override void Unpin() { }

        /// <summary>
        /// Releases all resources associated with this object
        /// </summary>
        protected override void Dispose(bool disposing) { }
    }
    [Flags]
    public enum FPDFBitmapFormat
    {
        /// <summary>
        /// Unknown or unsupported format.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Gray scale bitmap, one byte per pixel.
        /// </summary>
        Gray = 1,

        /// <summary>
        /// 3 bytes per pixel, byte order: blue, green, red.
        /// </summary>
        BGR = 2,

        /// <summary>
        /// 4 bytes per pixel, byte order: blue, green, red, unused.
        /// </summary>
        BGRx = 3,

        /// <summary>
        /// 4 bytes per pixel, byte order: blue, green, red, alpha.
        /// </summary>
        BGRA = 4
    }

    struct Rectangle
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    public unsafe class PdfImage : IDisposable
    {
        private readonly FpdfBitmapT _pdfBitmap;
        private readonly UnmanagedMemoryManager<byte> _mgr;

        public int Width { get; }

        public int Height { get; }

        public int Stride { get; }

        public Image<Bgra32> ImageData { get; }

        internal PdfImage(
            FpdfBitmapT pdfBitmap,
            int width,
            int height)
        {
            _pdfBitmap = pdfBitmap;
            var scan0 = fpdfview.FPDFBitmapGetBuffer(pdfBitmap);
            Stride = fpdfview.FPDFBitmapGetStride(pdfBitmap);
            Height = height;
            Width = width;
            _mgr = new UnmanagedMemoryManager<byte>((byte*)scan0, Stride * Height);

            ImageData = Image.WrapMemory<Bgra32>(Configuration.Default, _mgr.Memory, width, height);
        }

        public void Dispose()
        {
            ImageData.Dispose();
            fpdfview.FPDFBitmapDestroy(_pdfBitmap);
        }
    }

    class PdfToImage
    {
        public static async Task ExtractTextFromPdf(string filePath, string textFilePath)
        {
            fpdfview.FPDF_InitLibrary();

            var document = fpdfview.FPDF_LoadDocument(filePath, null);
            if (document == null)
                throw new Exception("Failed to load PDF document.");

            int pageCount = fpdfview.FPDF_GetPageCount(document);
            var sb = new StringBuilder();

            for (int i = 0; i < pageCount; i++)
            {
                var page = fpdfview.FPDF_LoadPage(document, i);
                if (page == null) continue;

                try
                {
                    // Load text page
                    var textPage = fpdftext.FPDFText_LoadPage(page.__Instance);
                    if (textPage == IntPtr.Zero)
                    {
                        sb.AppendLine($"Page {i + 1}: Failed to load text.");
                        continue;
                    }

                    int charCount = fpdftext.FPDFText_CountChars(textPage);
                    var buffer = new StringBuilder(charCount);

                    for (int j = 0; j < charCount;)
                    {
                        // Determine the number of chars to get in this batch (optional chunking)
                        int count = Math.Min(1024, charCount - j);
                        var temp = new ushort[count];
                        int copied = fpdftext.FPDFText_GetText(textPage, j, count, temp);

                        if (copied > 0)
                            buffer.Append(new string(temp.Take(copied).Select(c => (char)c).ToArray()));

                        j += count;
                    }

                    fpdftext.FPDFText_ClosePage(textPage);
                    sb.AppendLine($"Page {i + 1}:\n{buffer.ToString().Trim()}\n");

                }
                finally
                {
                    fpdfview.FPDF_ClosePage(page);
                }
            }

            fpdfview.FPDF_CloseDocument(document);
            fpdfview.FPDF_DestroyLibrary();

            await File.WriteAllTextAsync(textFilePath, sb.ToString());
        }



        public static int RenderPageToImage(string filePath)
        {
            fpdfview.FPDF_InitLibrary();

            double pageWidth = 0;
            double pageHeight = 0;
            float scale = 1;
            // White color.
            uint color = uint.MaxValue;
            // Load the document.
            var document = fpdfview.FPDF_LoadDocument(filePath, null);

            var pages = fpdfview.FPDF_GetPageCount(document);

            for (var i = 0; i < pages; i++)
            {
                var page = fpdfview.FPDF_LoadPage(document, i);
                fpdfview.FPDF_GetPageSizeByIndex(document, i, ref pageWidth, ref pageHeight);

                var viewport = new Rectangle()
                {
                    X = 0,
                    Y = 0,
                    Width = pageWidth,
                    Height = pageHeight,
                };
                var bitmap = fpdfview.FPDFBitmapCreateEx(
                        (int)viewport.Width,
                        (int)viewport.Height,
                        (int)FPDFBitmapFormat.BGRA,
                        IntPtr.Zero,
                        0);

                if (bitmap == null)
                    throw new Exception("failed to create a bitmap object");

                // Leave out if you want to make the background transparent.
                fpdfview.FPDFBitmapFillRect(bitmap, 0, 0, (int)viewport.Width, (int)viewport.Height, color);

                // |          | a b 0 |
                // | matrix = | c d 0 |
                // |          | e f 1 |
                using var matrix = new FS_MATRIX_();
                using var clipping = new FS_RECTF_();

                matrix.A = scale;
                matrix.B = 0;
                matrix.C = 0;
                matrix.D = scale;
                matrix.E = (float)-viewport.X;
                matrix.F = (float)-viewport.Y;

                clipping.Left = 0;
                clipping.Right = (float)viewport.Width;
                clipping.Bottom = 0;
                clipping.Top = (float)viewport.Height;

                fpdfview.FPDF_RenderPageBitmapWithMatrix(bitmap, page, matrix, clipping, (int)RenderFlags.RenderAnnotations);


                var image = new PdfImage(
                    bitmap,
                    (int)pageWidth,
                    (int)pageHeight);

                image.ImageData.SaveAsPng(filePath + "_" + i + ".png");
            }

            return pages;
        }
    }
}