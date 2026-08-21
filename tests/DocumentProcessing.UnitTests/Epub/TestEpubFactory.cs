using System.IO.Compression;
using System.Text;

namespace DocumentProcessing.UnitTests.Epub;

internal static class TestEpubFactory
{
    public static byte[] Create(
        bool includeUnsafeEntry = false,
        bool includeVisuals = false)
    {
        using var output =
            new MemoryStream();

        using (var archive =
               new ZipArchive(
                   output,
                   ZipArchiveMode.Create,
                   leaveOpen:
                       true))
        {
            Write(
                archive,
                "mimetype",
                "application/epub+zip",
                CompressionLevel.NoCompression);

            Write(
                archive,
                "META-INF/container.xml",
                """
                <?xml version="1.0"?>
                <container xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                  <rootfiles>
                    <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml" />
                  </rootfiles>
                </container>
                """);

            Write(
                archive,
                "OEBPS/content.opf",
                includeVisuals
                    ? """
                <?xml version="1.0" encoding="utf-8"?>
                <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="book-id">
                  <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:identifier id="book-id">urn:test:book</dc:identifier>
                    <dc:title>Test Book</dc:title>
                    <dc:language>en</dc:language>
                  </metadata>
                  <manifest>
                    <item id="chapter-1" href="chapter1.xhtml" media-type="application/xhtml+xml" />
                    <item id="chapter-2" href="chapter2.xhtml" media-type="application/xhtml+xml" />
                    <item id="cover-image" href="images/cover.png" media-type="image/png" properties="cover-image" />
                    <item id="diagram" href="images/diagram.png" media-type="image/png" />
                    <item id="decoration" href="images/decoration.png" media-type="image/png" />
                    <item id="auxiliary" href="images/auxiliary.png" media-type="image/png" />
                    <item id="unused" href="images/unused.png" media-type="image/png" />
                  </manifest>
                  <spine>
                    <itemref idref="chapter-1" />
                    <itemref idref="chapter-2" linear="no" />
                  </spine>
                </package>
                """
                    : """
                <?xml version="1.0" encoding="utf-8"?>
                <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="book-id">
                  <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:identifier id="book-id">urn:test:book</dc:identifier>
                    <dc:title>Test Book</dc:title>
                    <dc:language>en</dc:language>
                  </metadata>
                  <manifest>
                    <item id="chapter-1" href="chapter1.xhtml" media-type="application/xhtml+xml" />
                    <item id="chapter-2" href="chapter2.xhtml" media-type="application/xhtml+xml" />
                  </manifest>
                  <spine>
                    <itemref idref="chapter-1" />
                    <itemref idref="chapter-2" />
                  </spine>
                </package>
                """);

            Write(
                archive,
                "OEBPS/chapter1.xhtml",
                includeVisuals
                    ? """
                <?xml version="1.0" encoding="utf-8"?>
                <html xmlns="http://www.w3.org/1999/xhtml">
                  <head><title>Chapter 1</title></head>
                  <body>
                    <section>
                      <h1 id="heading-1">  Chapter   One  </h1>
                      <p id="paragraph-1">First <em>native</em> paragraph.</p>
                      <img id="cover-use" src="images/cover.png" alt="Book cover" />
                      <figure><img id="diagram-use" src="images/diagram.png" alt="System diagram" /><figcaption id="caption-1">A caption.</figcaption></figure>
                      <img id="decorative-use" src="images/decoration.png" alt="" role="presentation" />
                    </section>
                  </body>
                </html>
                """
                    : """
                <?xml version="1.0" encoding="utf-8"?>
                <html xmlns="http://www.w3.org/1999/xhtml">
                  <head><title>Chapter 1</title></head>
                  <body>
                    <section>
                      <h1 id="heading-1">  Chapter   One  </h1>
                      <p id="paragraph-1">First <em>native</em>
                      paragraph.</p>
                      <figure><figcaption id="caption-1">A caption.</figcaption></figure>
                    </section>
                  </body>
                </html>
                """);

            Write(
                archive,
                "OEBPS/chapter2.xhtml",
                includeVisuals
                    ? """
                <?xml version="1.0" encoding="utf-8"?>
                <html xmlns="http://www.w3.org/1999/xhtml">
                  <head><title>Chapter 2</title></head>
                  <body><p id="paragraph-2">Second paragraph.</p><img id="aux-use" src="images/auxiliary.png" alt="Appendix diagram" /></body>
                </html>
                """
                    : """
                <?xml version="1.0" encoding="utf-8"?>
                <html xmlns="http://www.w3.org/1999/xhtml">
                  <head><title>Chapter 2</title></head>
                  <body><p id="paragraph-2">Second paragraph.</p></body>
                </html>
                """);

            if (includeVisuals)
            {
                Write(
                    archive,
                    "OEBPS/images/cover.png",
                    [1, 2, 3, 4]);

                Write(
                    archive,
                    "OEBPS/images/diagram.png",
                    [10, 20, 30, 40, 50]);

                Write(
                    archive,
                    "OEBPS/images/decoration.png",
                    [6, 7, 8]);

                Write(
                    archive,
                    "OEBPS/images/auxiliary.png",
                    [60, 70, 80, 90]);

                Write(
                    archive,
                    "OEBPS/images/unused.png",
                    [100, 110]);
            }

            if (includeUnsafeEntry)
            {
                Write(
                    archive,
                    "../outside.txt",
                    "unsafe");
            }
        }

        return output.ToArray();
    }

    private static void Write(
        ZipArchive archive,
        string path,
        string content,
        CompressionLevel compressionLevel = CompressionLevel.Optimal)
    {
        var entry =
            archive.CreateEntry(
                path,
                compressionLevel);

        using var stream =
            entry.Open();

        using var writer =
            new StreamWriter(
                stream,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier:
                        false));

        writer.Write(
            content);
    }

    private static void Write(
        ZipArchive archive,
        string path,
        byte[] content)
    {
        var entry =
            archive.CreateEntry(
                path,
                CompressionLevel.Optimal);

        using var stream =
            entry.Open();

        stream.Write(
            content);
    }
}
