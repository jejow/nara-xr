package id.ac.its.nara.quest;

import android.content.Context;
import android.graphics.Bitmap;
import com.googlecode.tesseract.android.TessBaseAPI;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;

final class NaraOcr implements AutoCloseable {
    private final Context context;
    private TessBaseAPI reader;
    private boolean attempted;

    NaraOcr(Context context) { this.context = context; }

    private void prepare() throws IOException {
        File data = new File(context.getFilesDir(), "nara-ocr-v1");
        File directory = new File(data, "tessdata");
        if (!directory.mkdirs() && !directory.isDirectory()) throw new IOException("OCR data directory");
        for (String name : new String[]{"ind.traineddata", "eng.traineddata"}) {
            File model = new File(directory, name);
            if (model.isFile()) continue;
            File temporary = File.createTempFile("language-", ".tmp", directory);
            try {
                try (InputStream in = context.getAssets().open("nara-ocr/" + name);
                     FileOutputStream out = new FileOutputStream(temporary)) {
                    byte[] buffer = new byte[32768];
                    for (int length; (length = in.read(buffer)) != -1;) out.write(buffer, 0, length);
                }
                if (!temporary.renameTo(model)) throw new IOException("OCR model could not be saved");
            } finally {
                temporary.delete();
            }
        }
        reader = new TessBaseAPI();
        if (!reader.init(data.getPath(), "ind+eng", 1)) close();
    }

    String read(Bitmap page) {
        try {
            if (!attempted) {
                attempted = true;
                prepare();
            }
            if (reader == null) return "";
            reader.setPageSegMode(3);
            reader.setImage(page);
            try {
                String text = reader.getUTF8Text();
                return text == null ? "" : NaraDeck.collapsible(text);
            } finally {
                reader.clear();
            }
        } catch (RuntimeException | LinkageError | IOException unavailable) {
            android.util.Log.w("NARA", "Local OCR unavailable: " + unavailable.getClass().getSimpleName());
            return "";
        }
    }

    @Override public void close() {
        if (reader != null) {
            reader.recycle();
            reader = null;
        }
    }
}
