using System.IO.Compression;
using System.Text;

namespace DocumentProcessing.UnitTests.Epub;

internal static class TestEpubFactory
{
    public static byte[] Create(
        bool includeUnsafeEntry = false)
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
                """
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
                """
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
                """
                <?xml version="1.0" encoding="utf-8"?>
                <html xmlns="http://www.w3.org/1999/xhtml">
                  <head><title>Chapter 2</title></head>
                  <body><p id="paragraph-2">Second paragraph.</p></body>
                </html>
                """);

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
}
