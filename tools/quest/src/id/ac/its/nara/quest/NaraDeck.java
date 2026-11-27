package id.ac.its.nara.quest;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.RectF;
import android.graphics.Typeface;
import android.graphics.pdf.PdfRenderer;
import android.os.ParcelFileDescriptor;
import android.text.Layout;
import android.text.StaticLayout;
import android.text.TextPaint;
import com.tom_roush.pdfbox.android.PDFBoxResourceLoader;
import com.tom_roush.pdfbox.pdmodel.PDDocument;
import com.tom_roush.pdfbox.text.PDFTextStripper;
import fi.iki.elonen.NanoHTTPD.Response;
import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.FilterInputStream;
import java.io.IOException;
import java.io.InputStream;
import java.net.URI;
import java.net.URLDecoder;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.zip.ZipEntry;
import java.util.zip.ZipFile;
import java.util.zip.ZipOutputStream;
import javax.xml.parsers.DocumentBuilder;
import javax.xml.parsers.DocumentBuilderFactory;
import org.json.JSONArray;
import org.json.JSONObject;
import org.w3c.dom.Document;
import org.w3c.dom.Element;
import org.w3c.dom.Node;
import org.w3c.dom.NodeList;
import org.xml.sax.SAXException;

final class NaraDeck {
    private static final int MAX_PAGES = 60, MAX_SIDE = 1440;
    private static final long MAX_ARCHIVE = 32_000_000L;
    private static final String P = "http://schemas.openxmlformats.org/presentationml/2006/main";
    private static final String A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static final String R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static final Paint PHOTO = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);

    static final class BadDeck extends Exception {
        final Response.IStatus status;
        final String code;
        BadDeck(Response.IStatus status, String code) { this.status = status; this.code = code; }
    }

    static Response response(Context context, InputStream body, int length, String format, String name)
            throws BadDeck {
        if (!format.matches("\\.(pptx|ppt|pdf|png|jpg|jpeg)"))
            throw new BadDeck(Response.Status.UNSUPPORTED_MEDIA_TYPE, "format");
        File input = null, archive = null;
        boolean streaming = false;
        try {
            input = File.createTempFile("nara-import-", format, context.getCacheDir());
            archive = File.createTempFile("nara-slides-", ".zip", context.getCacheDir());
            try (FileOutputStream out = new FileOutputStream(input)) {
                byte[] buffer = new byte[32768];
                for (int remaining = length; remaining > 0;) {
                    int read = body.read(buffer, 0, Math.min(remaining, buffer.length));
                    if (read < 0) throw new IOException("Incomplete upload");
                    out.write(buffer, 0, read);
                    remaining -= read;
                }
            }
            String title = URLDecoder.decode(name, "UTF-8").replaceFirst("\\.[a-zA-Z0-9]+$", "");
            try (NaraOcr ocr = new NaraOcr(context); DeckWriter writer = new DeckWriter(archive)) {
                switch (format) {
                    case ".pdf": pdf(context, input, writer, ocr); break;
                    case ".pptx": pptx(input, writer, ocr); break;
                    case ".ppt": NaraLegacyDeck.render(input, writer); break;
                    default: image(input, writer, ocr);
                }
                writer.finish(title);
            }
            if (archive.length() > MAX_ARCHIVE) throw new BadDeck(Response.Status.PAYLOAD_TOO_LARGE, "size");
            final File completed = archive;
            InputStream response = new FilterInputStream(new FileInputStream(completed)) {
                @Override public void close() throws IOException {
                    try { super.close(); } finally { completed.delete(); }
                }
            };
            Response result = fi.iki.elonen.NanoHTTPD.newFixedLengthResponse(
                    Response.Status.OK, "application/zip", response, completed.length());
            result.addHeader("Cache-Control", "no-store");
            streaming = true;
            return result;
        } catch (BadDeck problem) {
            throw problem;
        } catch (Exception problem) {
            android.util.Log.w("NARA", "Import " + format + ": " + problem.getClass().getSimpleName());
            throw new BadDeck(NaraQuest.unprocessable(), "import_failed");
        } finally {
            if (input != null) input.delete();
            if (!streaming && archive != null) archive.delete();
        }
    }

    static final class DeckWriter implements AutoCloseable {
        private final ZipOutputStream output;
        private final File file;
        private final JSONArray pages = new JSONArray();

        DeckWriter(File file) throws IOException {
            this.file = file;
            output = new ZipOutputStream(new FileOutputStream(file));
        }

        void write(Bitmap bitmap, String text) throws Exception {
            try {
                int page = pages.length() + 1;
                checkPages(page);
                ByteArrayOutputStream image = new ByteArrayOutputStream();
                if (!bitmap.compress(Bitmap.CompressFormat.JPEG, 82, image) || image.size() > 1_500_000)
                    throw new IOException("Slide conversion");
                put(String.format(Locale.ROOT, "slide-%03d.jpg", page), image.toByteArray());
                pages.put(new JSONObject().put("page", page).put("text", limited(text, 2500)));
                if (file.length() > MAX_ARCHIVE) throw new BadDeck(Response.Status.PAYLOAD_TOO_LARGE, "size");
            } finally {
                bitmap.recycle();
            }
        }

        void finish(String topic) throws Exception {
            checkPages(pages.length());
            JSONObject manifest = new JSONObject().put("version", 1).put("topic", limited(topic, 100)).put("pages", pages);
            put("deck.json", manifest.toString().getBytes(StandardCharsets.UTF_8));
        }

        private void put(String name, byte[] data) throws IOException {
            output.putNextEntry(new ZipEntry(name));
            output.write(data);
            output.closeEntry();
        }

        @Override public void close() throws IOException { output.close(); }
    }

    private static void pdf(Context context, File file, DeckWriter writer, NaraOcr ocr) throws Exception {
        PDFBoxResourceLoader.init(context);
        try (PdfRenderer renderer = new PdfRenderer(ParcelFileDescriptor.open(file, ParcelFileDescriptor.MODE_READ_ONLY));
             PDDocument document = PDDocument.load(file, com.tom_roush.pdfbox.io.MemoryUsageSetting
                     .setupMixed(24 * 1024 * 1024L).setTempDir(context.getCacheDir()))) {
            checkPages(renderer.getPageCount());
            PDFTextStripper stripper = new PDFTextStripper();
            for (int i = 0; i < renderer.getPageCount(); i++) {
                try (PdfRenderer.Page page = renderer.openPage(i)) {
                    Bitmap bitmap = pageBitmap(page.getWidth(), page.getHeight());
                    try {
                        page.render(bitmap, null, null, PdfRenderer.Page.RENDER_MODE_FOR_DISPLAY);
                        stripper.setStartPage(i + 1);
                        stripper.setEndPage(i + 1);
                        String text = collapsible(stripper.getText(document));
                        writer.write(bitmap, text.isEmpty() ? ocr.read(bitmap) : text);
                    } finally {
                        if (!bitmap.isRecycled()) bitmap.recycle();
                    }
                }
            }
        }
    }

    private static void image(File file, DeckWriter writer, NaraOcr ocr) throws Exception {
        Bitmap source;
        try (InputStream in = new FileInputStream(file)) {
            source = decode(NaraQuest.read(in, NaraQuest.MAX_DECK, false));
        }
        if (source == null) throw new IOException("Unsupported image");
        Bitmap bitmap = pageBitmap(source.getWidth(), source.getHeight());
        new Canvas(bitmap).drawBitmap(source, null, new RectF(0, 0, bitmap.getWidth(), bitmap.getHeight()), PHOTO);
        source.recycle();
        try { writer.write(bitmap, ocr.read(bitmap)); }
        finally { if (!bitmap.isRecycled()) bitmap.recycle(); }
    }

    private static void pptx(File file, DeckWriter writer, NaraOcr ocr) throws Exception {
        try (ZipFile zip = new ZipFile(file)) {
            if (zip.size() > 4000) throw new BadDeck(Response.Status.PAYLOAD_TOO_LARGE, "size");
            Document presentation = xml(zip, "ppt/presentation.xml");
            Element size = child(presentation.getDocumentElement(), P, "sldSz");
            float width = number(size, "cx", 12192000), height = number(size, "cy", 6858000);
            if (width < 1000 || height < 1000 || width > 20000000 || height > 20000000)
                throw new IOException("Unexpected slide dimensions");
            Element slideList = child(presentation.getDocumentElement(), P, "sldIdLst");
            List<Element> slides = children(slideList, P, "sldId");
            checkPages(slides.size());
            Map<String, String> parts = relations(zip, "ppt/presentation.xml");
            for (Element slide : slides) {
                String part = parts.get(slide.getAttributeNS(R, "id"));
                if (part == null || !part.startsWith("ppt/slides/") || !part.endsWith(".xml"))
                    throw new IOException("Missing slide part");
                Bitmap bitmap = pageBitmap(width, height);
                try {
                    Canvas canvas = new Canvas(bitmap);
                    canvas.scale(bitmap.getWidth() / width, bitmap.getHeight() / height);
                    Element common = child(xml(zip, part).getDocumentElement(), P, "cSld");
                    Element background = child(child(common, P, "bg"), P, "bgPr");
                    int bg = color(child(background, A, "solidFill"), Color.WHITE);
                    canvas.drawColor(bg);
                    List<String> strings = new ArrayList<>();
                    Element tree = child(common, P, "spTree");
                    Map<String, String> images = relations(zip, part);
                    int fallback = 0;
                    for (Element shape : children(tree, P, null)) {
                        if (fallback >= 180) break;
                        if (shape.getLocalName().equals("pic")) {
                            drawPicture(zip, canvas, shape, images);
                        } else if (shape.getLocalName().equals("sp")) {
                            String text = paragraphs(child(shape, P, "txBody"));
                            if (!text.isEmpty()) strings.add(text);
                            drawShape(canvas, shape, text, width, height, fallback, bg);
                            if (!text.isEmpty()) fallback++;
                        } else {
                            String text = descendants(shape, A, "t");
                            if (!text.isEmpty()) {
                                strings.add(text);
                                drawText(canvas, text, fallbackBox(width, height, fallback++), 200000,
                                        foreground(bg), false, Layout.Alignment.ALIGN_NORMAL);
                            }
                        }
                    }
                    String text = String.join("\n", strings);
                    writer.write(bitmap, text.isEmpty() ? ocr.read(bitmap) : text);
                } finally {
                    if (!bitmap.isRecycled()) bitmap.recycle();
                }
            }
        }
    }

    private static void drawShape(Canvas canvas, Element shape, String text, float width, float height, int order, int background) {
        Element properties = child(shape, P, "spPr");
        RectF box = location(properties);
        boolean useFallback = box == null;
        if (useFallback && text.isEmpty()) return;
        if (useFallback) box = fallbackBox(width, height, order);
        int fill = color(child(properties, A, "solidFill"), Color.TRANSPARENT);
        if (fill != Color.TRANSPARENT) {
            Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
            paint.setColor(fill);
            canvas.drawRect(box, paint);
            background = fill;
        }
        if (text.isEmpty()) return;
        Element body = child(shape, P, "txBody");
        Element paragraph = child(body, A, "p");
        Element propertiesOfParagraph = child(paragraph, A, "pPr");
        Element style = child(child(paragraph, A, "r"), A, "rPr");
        if (style == null) style = child(propertiesOfParagraph, A, "defRPr");
        float font = number(style, "sz", order == 0 && useFallback ? 2700 : 1700) * 127f;
        int foreground = color(child(style, A, "solidFill"), foreground(background));
        String align = attribute(propertiesOfParagraph, "algn");
        drawText(canvas, text, box, font, foreground, "1".equals(attribute(style, "b")) || order == 0 && useFallback,
                align.equals("ctr") ? Layout.Alignment.ALIGN_CENTER :
                align.equals("r") ? Layout.Alignment.ALIGN_OPPOSITE : Layout.Alignment.ALIGN_NORMAL);
    }

    private static int foreground(int background) {
        return Color.red(background) * 3 + Color.green(background) * 6 + Color.blue(background) < 1200
                ? Color.WHITE : 0xff152438;
    }

    static void drawText(Canvas canvas, String text, RectF box, float size, int color, boolean bold, Layout.Alignment alignment) {
        // Units in PowerPoint are large. Drawing in a local 1/1000 scale keeps Android's text layout within range.
        float unit = Math.max(1, size / 36);
        int width = Math.round(box.width() / unit), height = Math.round(box.height() / unit);
        if (width < 4 || height < 4) return;
        TextPaint paint = new TextPaint(Paint.ANTI_ALIAS_FLAG);
        paint.setTextSize(size / unit);
        paint.setColor(color);
        if (bold) paint.setTypeface(Typeface.DEFAULT_BOLD);
        int lines = Math.max(1, height / Math.max(1, (int)paint.getFontSpacing()));
        StaticLayout layout = StaticLayout.Builder.obtain(text, 0, text.length(), paint, width)
                .setIncludePad(false).setAlignment(alignment).setMaxLines(lines)
                .setEllipsize(android.text.TextUtils.TruncateAt.END).build();
        canvas.save();
        canvas.clipRect(box);
        canvas.translate(box.left, box.top);
        canvas.scale(unit, unit);
        layout.draw(canvas);
        canvas.restore();
    }

    private static RectF fallbackBox(float width, float height, int index) {
        float top = height * (.09f + Math.min(index, 12) * .06f);
        return new RectF(width * .08f, top, width * .92f, Math.min(height * .97f, top + height * (index == 0 ? .10f : .38f)));
    }

    private static void drawPicture(ZipFile zip, Canvas canvas, Element shape, Map<String, String> images) throws IOException {
        RectF box = location(child(shape, P, "spPr"));
        Element blip = child(child(shape, P, "blipFill"), A, "blip");
        if (box == null || blip == null) return;
        String path = images.get(blip.getAttributeNS(R, "embed"));
        ZipEntry entry = path == null || !path.startsWith("ppt/media/") ? null : zip.getEntry(path);
        if (entry == null || entry.getSize() > 9_000_000) return;
        try (InputStream in = zip.getInputStream(entry)) {
            Bitmap image = decode(NaraQuest.read(in, 9_000_000, false));
            if (image != null) {
                try { canvas.drawBitmap(image, null, box, PHOTO); }
                finally { image.recycle(); }
            }
        }
    }

    private static RectF location(Element properties) {
        Element transform = child(properties, A, "xfrm");
        Element at = child(transform, A, "off"), size = child(transform, A, "ext");
        float x = number(at, "x", 0), y = number(at, "y", 0);
        float w = number(size, "cx", 0), h = number(size, "cy", 0);
        return w > 0 && h > 0 ? new RectF(x, y, x + w, y + h) : null;
    }

    private static Bitmap decode(byte[] bytes) {
        BitmapFactory.Options options = new BitmapFactory.Options();
        options.inJustDecodeBounds = true;
        BitmapFactory.decodeByteArray(bytes, 0, bytes.length, options);
        if (options.outWidth < 1 || options.outHeight < 1 ||
                (long) options.outWidth * options.outHeight > 160_000_000) return null;
        options.inJustDecodeBounds = false;
        options.inSampleSize = 1;
        while (Math.max(options.outWidth, options.outHeight) > 2 * MAX_SIDE * options.inSampleSize)
            options.inSampleSize *= 2;
        return BitmapFactory.decodeByteArray(bytes, 0, bytes.length, options);
    }

    static Bitmap pageBitmap(float width, float height) {
        float scale = MAX_SIDE / Math.max(width, height);
        Bitmap bitmap = Bitmap.createBitmap(Math.max(1, Math.round(width * scale)), Math.max(1, Math.round(height * scale)),
                Bitmap.Config.ARGB_8888);
        bitmap.eraseColor(Color.WHITE);
        return bitmap;
    }

    static void checkPages(int count) throws BadDeck {
        if (count > MAX_PAGES) throw new BadDeck(Response.Status.PAYLOAD_TOO_LARGE, "size");
        if (count < 1) throw new BadDeck(NaraQuest.unprocessable(), "empty");
    }

    private static Map<String, String> relations(ZipFile zip, String part) throws Exception {
        int slash = part.lastIndexOf('/');
        String directory = part.substring(0, slash + 1);
        String filename = part.substring(slash + 1);
        String path = directory + "_rels/" + filename + ".rels";
        Map<String, String> result = new LinkedHashMap<>();
        if (zip.getEntry(path) == null) return result;
        for (Element item : children(xml(zip, path).getDocumentElement(), null, "Relationship")) {
            if ("External".equalsIgnoreCase(item.getAttribute("TargetMode"))) continue;
            URI resolved = URI.create("/" + part).resolve(item.getAttribute("Target")).normalize();
            String internal = resolved.getPath();
            if (!resolved.isAbsolute() && resolved.getAuthority() == null && internal != null && internal.startsWith("/"))
                result.put(item.getAttribute("Id"), internal.substring(1));
        }
        return result;
    }

    private static Document xml(ZipFile zip, String name) throws Exception {
        ZipEntry entry = zip.getEntry(name);
        if (entry == null || entry.getSize() > 2_000_000) throw new IOException("Missing or oversized part");
        byte[] bytes;
        try (InputStream in = zip.getInputStream(entry)) { bytes = NaraQuest.read(in, 2_000_000, false); }
        String xml = new String(bytes, StandardCharsets.UTF_8);
        if (xml.contains("<!DOCTYPE") || xml.indexOf('\0') >= 0) throw new IOException("Invalid slide XML");
        DocumentBuilderFactory factory = DocumentBuilderFactory.newInstance();
        factory.setNamespaceAware(true);
        factory.setExpandEntityReferences(false);
        DocumentBuilder parser = factory.newDocumentBuilder();
        parser.setEntityResolver((publicId, systemId) -> { throw new SAXException("External entity"); });
        return parser.parse(new ByteArrayInputStream(bytes));
    }

    private static List<Element> children(Element parent, String namespace, String name) {
        List<Element> result = new ArrayList<>();
        if (parent != null) {
            for (Node current = parent.getFirstChild(); current != null; current = current.getNextSibling())
                if (current instanceof Element && (name == null || name.equals(current.getLocalName())) &&
                        (namespace == null || namespace.equals(current.getNamespaceURI())))
                    result.add((Element)current);
        }
        return result;
    }

    private static Element child(Element parent, String namespace, String name) {
        if (parent == null) return null;
        for (Node node = parent.getFirstChild(); node != null; node = node.getNextSibling())
            if (node instanceof Element && name.equals(node.getLocalName()) && namespace.equals(node.getNamespaceURI()))
                return (Element)node;
        return null;
    }

    private static String paragraphs(Element shape) {
        List<String> lines = new ArrayList<>();
        for (Element paragraph : children(shape, A, "p")) {
            String text = descendants(paragraph, A, "t");
            if (!text.isEmpty()) lines.add(text);
        }
        return limited(String.join("\n", lines), 2500);
    }

    private static String descendants(Element shape, String namespace, String type) {
        NodeList nodes = shape.getElementsByTagNameNS(namespace, type);
        StringBuilder text = new StringBuilder();
        for (int i = 0; i < nodes.getLength() && text.length() < 2500; i++)
            text.append(nodes.item(i).getTextContent());
        return limited(text.toString(), 2500);
    }

    private static int color(Element fill, int fallback) {
        String hex = attribute(child(fill, A, "srgbClr"), "val");
        try { return hex.length() == 6 ? Color.parseColor("#" + hex) : fallback; }
        catch (IllegalArgumentException invalid) { return fallback; }
    }

    private static float number(Element element, String name, float fallback) {
        try {
            float value = Float.parseFloat(attribute(element, name));
            return Float.isFinite(value) && Math.abs(value) <= 40_000_000 ? value : fallback;
        } catch (NumberFormatException bad) { return fallback; }
    }

    private static String attribute(Element element, String name) {
        return element == null ? "" : element.getAttribute(name);
    }

    static String collapsible(String text) { return limited(text.replaceAll("[\\p{Cntrl}\\s]+", " "), 2500); }
    static String limited(String text, int max) {
        String clean = text.trim();
        return clean.substring(0, Math.min(max, clean.length()));
    }
}
