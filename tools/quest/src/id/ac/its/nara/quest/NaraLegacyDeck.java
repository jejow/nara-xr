package id.ac.its.nara.quest;

import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.RectF;
import android.text.Layout;
import java.io.File;
import java.io.IOException;
import java.io.InputStream;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.nio.charset.Charset;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import org.apache.poi.poifs.filesystem.POIFSFileSystem;

final class NaraLegacyDeck {
    private static final int DOCUMENT = 1000, SLIDE = 1006, SLIDE_PERSIST = 1011;
    private static final int TEXT_UNICODE = 4000, TEXT_ANSI = 4008, SLIDE_LIST = 4080, USER_EDIT = 4085;
    private static final int PERSIST = 6002, FULL_PERSIST = 6001, OFFICE_ART_TEXT = 0xf00d;

    static void render(File file, NaraDeck.DeckWriter writer) throws Exception {
        try (POIFSFileSystem container = new POIFSFileSystem(file, true);
             InputStream main = container.createDocumentInputStream("PowerPoint Document");
             InputStream user = container.createDocumentInputStream("Current User")) {
            ByteBuffer data = little(NaraQuest.read(main, NaraQuest.MAX_DECK, false));
            ByteBuffer current = little(NaraQuest.read(user, 100_000, false));
            if (current.limit() < 20) throw new IOException("Current User stream");
            int cursor = current.getInt(16);
            int documentId = -1;
            Map<Integer, Integer> locations = new HashMap<>();
            for (int edits = 0; cursor > 0; edits++) {
                if (edits >= 256) throw new IOException("Edit chain");
                Record edit = record(data, cursor, data.limit());
                if (edit.type != USER_EDIT || edit.end - edit.body < 28 || edit.end - edit.body >= 32)
                    throw new IOException("Unexpected or protected presentation");
                if (documentId < 0) documentId = data.getInt(edit.body + 16);
                Record persist = record(data, data.getInt(edit.body + 12), data.limit());
                if (persist.type != PERSIST && persist.type != FULL_PERSIST) throw new IOException("Persist directory");
                for (int j = persist.body; j < persist.end;) {
                    if (persist.end - j < 4) throw new IOException("Persist directory entry");
                    int group = data.getInt(j);
                    j += 4;
                    int count = group >>> 20, id = group & 0xfffff;
                    if (count == 0 || count > (persist.end - j) / 4) throw new IOException("Persist group");
                    for (int i = 0; i < count; i++, j += 4) locations.putIfAbsent(id + i, data.getInt(j));
                }
                int previous = data.getInt(edit.body + 8);
                if (previous >= cursor || previous < 0) throw new IOException("Edit chain order");
                cursor = previous;
            }
            Record document = record(data, locations.getOrDefault(documentId, -1), data.limit());
            if (document.type != DOCUMENT) throw new IOException("Document record");
            List<String> slides = new ArrayList<>();
            for (int pos = document.body; pos < document.end;) {
                Record list = record(data, pos, document.end);
                if (list.type == SLIDE_LIST && list.instance == 0) readSlides(data, list, locations, slides);
                pos = list.end;
            }
            NaraDeck.checkPages(slides.size());
            for (String text : slides) {
                Bitmap preview = NaraDeck.pageBitmap(1440, 810);
                Canvas canvas = new Canvas(preview);
                NaraDeck.drawText(canvas, text.isEmpty() ? "Slide PowerPoint" : text,
                        new RectF(90, 90, 1350, 720), 44, 0xff152438, false, Layout.Alignment.ALIGN_NORMAL);
                writer.write(preview, text);
            }
        }
    }

    private static void readSlides(ByteBuffer data, Record container, Map<Integer, Integer> locations,
            List<String> slides) throws Exception {
        StringBuilder text = null;
        int slide = -1;
        for (int pos = container.body; pos < container.end;) {
            Record entry = record(data, pos, container.end);
            if (entry.type == SLIDE_PERSIST) {
                if (text != null) add(data, text, slide, slides);
                if (entry.end - entry.body < 4) throw new IOException("Slide persist");
                text = new StringBuilder();
                slide = locations.getOrDefault(data.getInt(entry.body), -1);
            } else if (text != null) {
                append(data, entry, text);
            }
            pos = entry.end;
        }
        if (text != null) add(data, text, slide, slides);
    }

    private static void add(ByteBuffer data, StringBuilder text, int slide, List<String> slides) throws Exception {
        if (slide >= 0) {
            Record content = record(data, slide, data.limit());
            if (content.type == SLIDE) searchText(data, content, text, 0);
        }
        slides.add(NaraDeck.limited(text.toString(), 2500));
        NaraDeck.checkPages(slides.size());
    }

    private static void searchText(ByteBuffer data, Record parent, StringBuilder text, int depth) throws IOException {
        if (depth > 22) return;
        for (int offset = parent.body; offset < parent.end && text.length() < 2500;) {
            Record child = record(data, offset, parent.end);
            append(data, child, text);
            if (child.container || child.type == OFFICE_ART_TEXT) searchText(data, child, text, depth + 1);
            offset = child.end;
        }
    }

    private static void append(ByteBuffer data, Record record, StringBuilder text) {
        if ((record.type != TEXT_UNICODE && record.type != TEXT_ANSI) || text.length() >= 2500) return;
        int count = Math.min(record.end - record.body, 5000);
        if (record.type == TEXT_UNICODE) count &= ~1;
        Charset encoding = record.type == TEXT_UNICODE ? StandardCharsets.UTF_16LE : Charset.forName("windows-1252");
        String part = new String(data.array(), record.body, count, encoding).replace('\r', '\n').trim();
        if (!part.isEmpty() && text.indexOf(part) < 0) {
            if (text.length() > 0) text.append('\n');
            text.append(part);
        }
    }

    private static ByteBuffer little(byte[] bytes) { return ByteBuffer.wrap(bytes).order(ByteOrder.LITTLE_ENDIAN); }

    private static Record record(ByteBuffer data, int position, int end) throws IOException {
        if (position < 0 || end - position < 8) throw new IOException("Unexpected record header");
        int options = Short.toUnsignedInt(data.getShort(position)), length = data.getInt(position + 4);
        int type = Short.toUnsignedInt(data.getShort(position + 2));
        if (length < 0 || length > end - position - 8) throw new IOException("Unexpected record length");
        return new Record(type, options >>> 4, (options & 15) == 15, position + 8, position + 8 + length);
    }

    private static final class Record {
        final int type, instance, body, end;
        final boolean container;
        Record(int type, int instance, boolean container, int body, int end) {
            this.type = type;
            this.instance = instance;
            this.container = container;
            this.body = body;
            this.end = end;
        }
    }
}
