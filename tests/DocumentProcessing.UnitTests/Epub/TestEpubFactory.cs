using System.IO.Compression;
using System.Text;

namespace DocumentProcessing.UnitTests.Epub;

internal static class TestEpubFactory
{
    public static byte[] Create(
        bool includeUnsafeEntry = false,
        bool includeVisuals = false,
        bool includeFootnotes = false)
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
                    <item id="front" href="front.xhtml" media-type="application/xhtml+xml" />
                    <item id="navigation" href="navigation.xhtml" media-type="application/xhtml+xml" properties="nav" />
                    <item id="chapter-1" href="chapter1.xhtml" media-type="application/xhtml+xml" />
                    <item id="chapter-2" href="chapter2.xhtml" media-type="application/xhtml+xml" />
                    <item id="cover-image" href="images/cover.png" media-type="image/png" properties="cover-image" />
                    <item id="diagram" href="images/diagram.png" media-type="image/png" />
                    <item id="decoration" href="images/decoration.png" media-type="image/png" />
                    <item id="auxiliary" href="images/auxiliary.png" media-type="image/png" />
                    <item id="unused" href="images/unused.png" media-type="image/png" />
                    <item id="front-image" href="images/front.png" media-type="image/png" />
                    <item id="separator" href="images/separator.png" media-type="image/png" />
                  </manifest>
                  <spine>
                    <itemref idref="front" />
                    <itemref idref="chapter-1" />
                    <itemref idref="chapter-2" linear="no" />
                  </spine>
                  <guide>
                    <reference type="text" title="Legacy beginning" href="front.xhtml" />
                  </guide>
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
                      <img id="separator-use-1" src="images/separator.png" alt="" />
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

            if (includeVisuals)
            {
                Write(
                    archive,
                    "OEBPS/navigation.xhtml",
                    """
                    <?xml version="1.0" encoding="utf-8"?>
                    <html xmlns="http://www.w3.org/1999/xhtml"
                          xmlns:epub="http://www.idpf.org/2007/ops">
                      <head><title>Navigation</title></head>
                      <body>
                        <nav epub:type="toc">
                          <ol>
                            <li><a href="chapter1.xhtml#heading-1">Chapter one</a></li>
                            <li><a href="chapter2.xhtml#styled-heading">Chapter two</a></li>
                          </ol>
                        </nav>
                        <nav epub:type="landmarks">
                          <ol><li><a epub:type="bodymatter" href="chapter1.xhtml#body">Beginning</a></li></ol>
                        </nav>
                      </body>
                    </html>
                    """);

                Write(
                    archive,
                    "OEBPS/front.xhtml",
                    """
                    <?xml version="1.0" encoding="utf-8"?>
                    <html xmlns="http://www.w3.org/1999/xhtml">
                      <head><title>Front matter</title></head>
                      <body><img id="front-use" src="images/front.png" alt="Title page" /></body>
                    </html>
                    """);
            }

            Write(
                archive,
                "OEBPS/chapter2.xhtml",
                includeVisuals
                    ? """
                <?xml version="1.0" encoding="utf-8"?>
                <html xmlns="http://www.w3.org/1999/xhtml">
                  <head><title>Chapter 2</title></head>
                  <body><p id="styled-heading">Styled chapter heading</p><p id="paragraph-2">Second paragraph.</p><img id="separator-use-2" src="images/separator.png" alt="" /><img id="aux-use" src="images/auxiliary.png" alt="Appendix diagram" /></body>
                </html>
                """
                    : includeFootnotes
                        ? """
                <?xml version="1.0" encoding="utf-8"?>
                <html xmlns="http://www.w3.org/1999/xhtml"
                      xmlns:epub="http://www.idpf.org/2007/ops">
                  <head><title>Notes</title></head>
                  <body>
                    <p id="before-note">Before note.<a id="inline-note-ref" epub:type="noteref" href="#inline-note">1</a></p>
                    <aside id="inline-note" epub:type="footnote"><span>1</span> Inline <em>footnote</em> content.<a epub:type="backlink" href="#inline-note-ref">↩</a></aside>
                    <aside id="nested-note" epub:type="footnote"><p>Nested footnote paragraph.</p></aside>
                    <p id="after-note">After note.</p>
                  </body>
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

                Write(
                    archive,
                    "OEBPS/images/front.png",
                    [120, 130, 140]);

                Write(
                    archive,
                    "OEBPS/images/separator.png",
                    Convert.FromBase64String(
                        "iVBORw0KGgoAAAANSUhEUgAAACAAAAACCAIAAAC2fEmeAAAADElEQVQI12NgGOoAAADCAAHhxfJhAAAAAElFTkSuQmCC"));
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

    public static byte[] CreateWithTerminalPresentation(
        bool promotional)
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
                promotional
                    ? """
                <?xml version="1.0" encoding="utf-8"?>
                <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="book-id">
                  <metadata xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:identifier id="book-id">urn:test:terminal</dc:identifier><dc:title>Terminal test</dc:title><dc:language>en</dc:language></metadata>
                  <manifest>
                    <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav" />
                    <item id="chapter" href="chapter.xhtml" media-type="application/xhtml+xml" />
                    <item id="terminal" href="terminal.xhtml" media-type="application/xhtml+xml" />
                    <item id="ad-1" href="images/ad-1.jpg" media-type="image/jpeg" />
                    <item id="ad-2" href="images/ad-2.jpg" media-type="image/jpeg" />
                  </manifest>
                  <spine><itemref idref="chapter" /><itemref idref="terminal" /></spine>
                </package>
                """
                    : """
                <?xml version="1.0" encoding="utf-8"?>
                <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="book-id">
                  <metadata xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:identifier id="book-id">urn:test:terminal</dc:identifier><dc:title>Terminal test</dc:title><dc:language>en</dc:language></metadata>
                  <manifest>
                    <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav" />
                    <item id="chapter" href="chapter.xhtml" media-type="application/xhtml+xml" />
                    <item id="terminal" href="terminal.xhtml" media-type="application/xhtml+xml" />
                    <item id="back" href="images/back.jpg" media-type="image/jpeg" />
                  </manifest>
                  <spine><itemref idref="chapter" /><itemref idref="terminal" /></spine>
                </package>
                """);

            Write(
                archive,
                "OEBPS/nav.xhtml",
                """
                <?xml version="1.0" encoding="utf-8"?>
                <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops"><head><title>Navigation</title></head><body><nav epub:type="toc"><ol><li><a href="chapter.xhtml#chapter-title">Chapter</a></li></ol></nav></body></html>
                """);

            Write(
                archive,
                "OEBPS/chapter.xhtml",
                """
                <?xml version="1.0" encoding="utf-8"?>
                <html xmlns="http://www.w3.org/1999/xhtml"><head><title>Chapter</title></head><body><p id="chapter-title">Styled chapter title</p><p>Body text.</p></body></html>
                """);

            Write(
                archive,
                "OEBPS/terminal.xhtml",
                promotional
                    ? """
                <?xml version="1.0" encoding="utf-8"?>
                <html xmlns="http://www.w3.org/1999/xhtml"><head><title>Recommendations</title></head><body><img src="images/ad-1.jpg" alt="Recommended publication"/><h2>First promotion</h2><a href="https://example.test/first">External offer</a><img src="images/ad-2.jpg" alt="Recommended publication"/><h2>Second promotion</h2><a href="https://example.test/second">External offer</a></body></html>
                """
                    : """
                <?xml version="1.0" encoding="utf-8"?>
                <html xmlns="http://www.w3.org/1999/xhtml"><head><title>Back</title></head><body><div><img src="images/back.jpg" alt=""/></div></body></html>
                """);

            if (promotional)
            {
                Write(
                    archive,
                    "OEBPS/images/ad-1.jpg",
                    [1, 2, 3]);

                Write(
                    archive,
                    "OEBPS/images/ad-2.jpg",
                    [4, 5, 6]);
            }
            else
            {
                Write(
                    archive,
                    "OEBPS/images/back.jpg",
                    [7, 8, 9]);
            }
        }

        return output.ToArray();
    }

    public static byte[] CreateNotes(
        string chapterOneBody,
        string chapterTwoBody)
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
                  <rootfiles><rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml" /></rootfiles>
                </container>
                """);

            Write(
                archive,
                "OEBPS/content.opf",
                """
                <?xml version="1.0" encoding="utf-8"?>
                <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="book-id">
                  <metadata xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:identifier id="book-id">urn:test:notes</dc:identifier><dc:title>Notes</dc:title><dc:language>en</dc:language></metadata>
                  <manifest><item id="chapter-1" href="chapter1.xhtml" media-type="application/xhtml+xml" /><item id="chapter-2" href="chapter2.xhtml" media-type="application/xhtml+xml" /></manifest>
                  <spine><itemref idref="chapter-1" /><itemref idref="chapter-2" /></spine>
                </package>
                """);

            Write(
                archive,
                "OEBPS/chapter1.xhtml",
                $$"""
                <?xml version="1.0" encoding="utf-8"?>
                <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops"><head><title>Chapter 1</title></head><body>{{chapterOneBody}}</body></html>
                """);

            Write(
                archive,
                "OEBPS/chapter2.xhtml",
                $$"""
                <?xml version="1.0" encoding="utf-8"?>
                <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops"><head><title>Chapter 2</title></head><body>{{chapterTwoBody}}</body></html>
                """);
        }

        return output.ToArray();
    }

    public static byte[] CreateNavigationFixture(
        bool useNcx = false,
        bool duplicateSpineTarget = false,
        bool unresolvedTarget = false)
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
                  <rootfiles><rootfile full-path="OPS/package.opf" media-type="application/oebps-package+xml" /></rootfiles>
                </container>
                """);

            Write(
                archive,
                "OPS/package.opf",
                useNcx
                    ? """
                <?xml version="1.0" encoding="utf-8"?>
                <package xmlns="http://www.idpf.org/2007/opf" version="2.0" unique-identifier="book-id">
                  <metadata xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:identifier id="book-id">urn:test:ncx</dc:identifier><dc:title>NCX fixture</dc:title><dc:language>en</dc:language></metadata>
                  <manifest>
                    <item id="toc" href="toc.ncx" media-type="application/x-dtbncx+xml" />
                    <item id="front" href="text/front.xhtml" media-type="application/xhtml+xml" />
                    <item id="chapter-1" href="text/chapter1.xhtml" media-type="application/xhtml+xml" />
                    <item id="chapter-2" href="text/chapter2.xhtml" media-type="application/xhtml+xml" />
                  </manifest>
                  <spine toc="toc"><itemref idref="front" /><itemref idref="chapter-1" /><itemref idref="chapter-2" /></spine>
                </package>
                """
                    : """
                <?xml version="1.0" encoding="utf-8"?>
                <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="book-id">
                  <metadata xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:identifier id="book-id">urn:test:nav</dc:identifier><dc:title>Navigation fixture</dc:title><dc:language>en</dc:language></metadata>
                  <manifest>
                    <item id="navigation" href="navigation.xhtml" media-type="application/xhtml+xml" properties="nav" />
                    <item id="front" href="text/front.xhtml" media-type="application/xhtml+xml" />
                    <item id="chapter-1" href="text/chapter1.xhtml" media-type="application/xhtml+xml" />
                    <item id="chapter-2" href="text/chapter2.xhtml" media-type="application/xhtml+xml" />
                  </manifest>
                  <spine><itemref idref="front" /><itemref idref="chapter-1" /><itemref idref="chapter-2" /></spine>
                </package>
                """);

            if (useNcx)
            {
                Write(
                    archive,
                    "OPS/toc.ncx",
                    """
                    <?xml version="1.0" encoding="utf-8"?>
                    <ncx xmlns="http://www.daisy.org/z3986/2005/ncx/" version="2005-1">
                      <head><meta name="dtb:uid" content="urn:test:ncx" /></head>
                      <docTitle><text>NCX fixture</text></docTitle>
                      <navMap>
                        <navPoint id="chapter-1" playOrder="1"><navLabel><text> Chapter   One </text></navLabel><content src="text/chapter1.xhtml#title" /></navPoint>
                        <navPoint id="chapter-2" playOrder="2"><navLabel><text>Chapter Two</text></navLabel><content src="text/chapter2.xhtml#title" /></navPoint>
                      </navMap>
                    </ncx>
                    """);
            }
            else
            {
                var secondTarget =
                    duplicateSpineTarget
                        ? "text/chapter1.xhtml#second-title"
                        : unresolvedTarget
                            ? "text/missing.xhtml#title"
                            : "text/chapter2.xhtml#title";

                Write(
                    archive,
                    "OPS/navigation.xhtml",
                    $$"""
                    <?xml version="1.0" encoding="utf-8"?>
                    <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops">
                      <head><title>Navigation</title></head>
                      <body><nav epub:type="toc"><ol>
                        <li><a href="text/chapter1.xhtml#title"> Chapter   One </a></li>
                        <li><a href="{{secondTarget}}">Chapter Two</a></li>
                      </ol></nav></body>
                    </html>
                    """);
            }

            Write(
                archive,
                "OPS/text/front.xhtml",
                "<html xmlns=\"http://www.w3.org/1999/xhtml\"><head><title>Front</title></head><body><p>Front matter.</p></body></html>");

            Write(
                archive,
                "OPS/text/chapter1.xhtml",
                "<html xmlns=\"http://www.w3.org/1999/xhtml\"><head><title>One</title></head><body><h1 id=\"title\">One</h1><h2 id=\"second-title\">Second heading</h2></body></html>");

            Write(
                archive,
                "OPS/text/chapter2.xhtml",
                "<html xmlns=\"http://www.w3.org/1999/xhtml\"><head><title>Two</title></head><body><h1 id=\"title\">Two</h1></body></html>");
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
