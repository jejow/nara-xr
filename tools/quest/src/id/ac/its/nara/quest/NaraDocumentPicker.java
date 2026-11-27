package id.ac.its.nara.quest;

import android.app.Activity;
import android.content.Intent;
import android.database.Cursor;
import android.net.Uri;
import android.os.Bundle;
import android.provider.OpenableColumns;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.util.Locale;

public final class NaraDocumentPicker extends Activity {
    private static final int CHOOSE = 417;
    private static volatile String picked;
    private static volatile boolean waiting;

    public static boolean open(Activity activity) {
        if (waiting) return false;
        picked = null;
        waiting = true;
        activity.runOnUiThread(() -> {
            try {
                activity.startActivity(new Intent(activity, NaraDocumentPicker.class));
            } catch (RuntimeException failure) {
                picked = "!picker";
                waiting = false;
            }
        });
        return true;
    }

    public static String takeResult() {
        if (waiting || picked == null) return "";
        String result = picked;
        picked = null;
        return result;
    }

    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        if (state != null) return;
        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        // Several Android file providers declare presentations as application/octet-stream.
        intent.setType("*/*");
        try {
            startActivityForResult(intent, CHOOSE);
        } catch (RuntimeException failure) {
            result("!picker");
        }
    }

    @Override public void onActivityResult(int request, int status, Intent data) {
        super.onActivityResult(request, status, data);
        if (request != CHOOSE) return;
        if (status != RESULT_OK || data == null || data.getData() == null) {
            result("!cancel");
            return;
        }
        Uri uri = data.getData();
        new Thread(() -> copy(uri), "nara-document").start();
    }

    private void copy(Uri uri) {
        File destination = null;
        try {
            String name = null;
            try (Cursor cursor = getContentResolver().query(uri, new String[]{OpenableColumns.DISPLAY_NAME},
                    null, null, null)) {
                if (cursor != null && cursor.moveToFirst()) name = cursor.getString(0);
            }
            if (name == null || name.length() > 200) throw new IOException();
            name = name.replaceAll("[/\\\\\\p{Cntrl}]", "_");
            String lower = name.toLowerCase(Locale.ROOT);
            if (!lower.matches(".+\\.(pptx|ppt|pdf|png|jpg|jpeg)$")) {
                result("!format");
                return;
            }
            File selection = new File(getCacheDir(), "selection-" + java.util.UUID.randomUUID());
            if (!selection.mkdir()) throw new IOException();
            destination = new File(selection, name);
            try (InputStream input = getContentResolver().openInputStream(uri);
                 FileOutputStream output = new FileOutputStream(destination)) {
                if (input == null) throw new IOException();
                byte[] chunk = new byte[32768];
                int total = 0;
                for (int read; (read = input.read(chunk)) >= 0;) {
                    total += read;
                    if (total > NaraQuest.MAX_DECK) throw new OversizedFile();
                    output.write(chunk, 0, read);
                }
                if (total == 0) throw new IOException();
            }
            result(destination.getAbsolutePath());
        } catch (Exception failure) {
            if (destination != null) {
                destination.delete();
                destination.getParentFile().delete();
            }
            result(failure instanceof OversizedFile ? "!size" : "!read");
        }
    }

    private static final class OversizedFile extends IOException { }

    private void result(String value) {
        picked = value;
        waiting = false;
        runOnUiThread(this::finish);
    }
}
