package id.ac.its.nara.quest;

import android.content.Context;
import android.content.SharedPreferences;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.Base64;
import java.nio.charset.StandardCharsets;
import java.security.KeyStore;
import java.util.Arrays;
import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

final class NaraCredentials {
    private static final String ALIAS = "nara-aisubs-v1";
    private final SharedPreferences preferences;

    NaraCredentials(Context context) {
        preferences = context.getSharedPreferences("nara-private", Context.MODE_PRIVATE);
    }

    private SecretKey deviceKey(boolean create) throws Exception {
        KeyStore store = KeyStore.getInstance("AndroidKeyStore");
        store.load(null);
        SecretKey saved = (SecretKey) store.getKey(ALIAS, null);
        if (saved != null || !create) return saved;
        KeyGenerator generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore");
        generator.init(new KeyGenParameterSpec.Builder(ALIAS, KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE).build());
        return generator.generateKey();
    }

    synchronized boolean save(String text) {
        String key = text.trim();
        if (!key.matches("sk-[A-Za-z0-9_-]{12,180}")) return false;
        try {
            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.ENCRYPT_MODE, deviceKey(true));
            byte[] iv = cipher.getIV();
            byte[] encrypted = cipher.doFinal(key.getBytes(StandardCharsets.UTF_8));
            if (iv.length != 12) return false;
            byte[] packed = Arrays.copyOf(iv, iv.length + encrypted.length);
            System.arraycopy(encrypted, 0, packed, iv.length, encrypted.length);
            return preferences.edit().putString("credential", Base64.encodeToString(packed, Base64.NO_WRAP)).commit();
        } catch (Exception failure) {
            return false;
        }
    }

    synchronized boolean hasKey() {
        return !read().isEmpty();
    }

    synchronized String read() {
        try {
            String saved = preferences.getString("credential", "");
            if (saved.isEmpty()) return "";
            SecretKey key = deviceKey(false);
            if (key == null) return "";
            byte[] encrypted = Base64.decode(saved, Base64.DEFAULT);
            if (encrypted.length < 28) return "";
            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.DECRYPT_MODE, key, new GCMParameterSpec(128, encrypted, 0, 12));
            return new String(cipher.doFinal(encrypted, 12, encrypted.length - 12), StandardCharsets.UTF_8);
        } catch (Exception failure) {
            return "";
        }
    }

    synchronized boolean clear() {
        return preferences.edit().remove("credential").commit();
    }
}
