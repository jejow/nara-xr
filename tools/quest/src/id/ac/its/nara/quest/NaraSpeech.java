package id.ac.its.nara.quest;

import android.content.Context;
import android.content.res.AssetManager;
import android.util.Base64;
import com.k2fsa.sherpa.onnx.*;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.nio.charset.StandardCharsets;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import org.json.JSONObject;

final class NaraSpeech {
    private static final int RECORD_RATE = 16000;
    private static final int PLAY_RATE = 24000;
    private static final int MAX_FRAMES = PLAY_RATE * 60;
    private static final int PCM_PACKET = PLAY_RATE / 10;
    private static final Pattern PAUSES = Pattern.compile("(?<=[.!?;])\\s+(?=\\p{L})");

    private final AssetManager assets;
    private final File root;
    private final Object asrLock = new Object();
    private final Object ttsLock = new Object();
    private OfflineRecognizer recognizer;
    private OfflineTts voice;
    private String recognitionLanguage, voiceName;

    NaraSpeech(Context context) {
        assets = context.getAssets();
        root = new File(context.getFilesDir(), "nara-speech-v1");
    }

    private File extract(String relative) throws IOException {
        File local = new File(root, relative);
        if (local.isFile() || new File(local, ".ready").isFile()) return local;
        String asset = "nara-speech/" + relative;
        String[] children = assets.list(asset);
        if (children != null && children.length > 0) {
            if (!local.mkdirs() && !local.isDirectory()) throw new IOException();
            for (String child : children) extract(relative + "/" + child);
            if (!new File(local, ".ready").createNewFile()) throw new IOException();
        } else {
            File parent = local.getParentFile();
            if (!parent.mkdirs() && !parent.isDirectory()) throw new IOException();
            File partial = File.createTempFile("nara-model-", ".tmp", parent);
            try {
                try (InputStream input = assets.open(asset); FileOutputStream output = new FileOutputStream(partial)) {
                    byte[] buffer = new byte[32768];
                    for (int read; (read = input.read(buffer)) != -1;) output.write(buffer, 0, read);
                }
                if (!partial.renameTo(local)) throw new IOException();
            } finally {
                partial.delete();
            }
        }
        return local;
    }

    private File dataDirectory() throws IOException {
        File data = new File(root, "espeak-ng-data");
        if (!new File(data, ".ready").isFile()) {
            String[] files = assets.list("nara-speech/espeak-ng-data");
            if (files == null || files.length == 0) throw new IOException();
            for (String path : files) extract("espeak-ng-data/" + path);
            if (!new File(data, ".ready").createNewFile()) throw new IOException();
        }
        return data;
    }

    private OfflineRecognizer recognizer(String language) throws IOException {
        if (recognizer != null && language.equals(recognitionLanguage)) return recognizer;
        if (recognizer != null) {
            recognizer.release();
            recognizer = null;
        }
        OfflineWhisperModelConfig whisper = new OfflineWhisperModelConfig();
        whisper.setEncoder(extract("asr/tiny-encoder.int8.onnx").getPath());
        whisper.setDecoder(extract("asr/tiny-decoder.int8.onnx").getPath());
        whisper.setLanguage(language);
        whisper.setTask("transcribe");
        OfflineModelConfig model = new OfflineModelConfig();
        model.setWhisper(whisper);
        model.setTokens(extract("asr/tiny-tokens.txt").getPath());
        model.setModelType("whisper");
        model.setNumThreads(2);
        OfflineRecognizerConfig config = new OfflineRecognizerConfig();
        config.setModelConfig(model);
        config.setDecodingMethod("greedy_search");
        recognizer = new OfflineRecognizer(null, config);
        recognitionLanguage = language;
        return recognizer;
    }

    String recognize(String encoded, String language, int seconds) throws IOException {
        if (encoded.isEmpty()) return "";
        byte[] wav;
        try { wav = Base64.decode(encoded, Base64.DEFAULT); }
        catch (IllegalArgumentException error) { throw new IOException(error); }
        if (wav.length < 44 || wav.length > 44 + seconds * RECORD_RATE * 2L || ((wav.length - 44) & 1) != 0)
            throw new IOException("Invalid recording size.");
        ByteBuffer input = ByteBuffer.wrap(wav).order(ByteOrder.LITTLE_ENDIAN);
        if (input.getInt(0) != 0x46464952 || input.getInt(8) != 0x45564157 ||
            input.getInt(12) != 0x20746d66 || input.getInt(16) != 16 ||
            input.getShort(20) != 1 || input.getShort(22) != 1 || input.getInt(24) != RECORD_RATE ||
            input.getShort(34) != 16 || input.getInt(36) != 0x61746164 || input.getInt(40) != wav.length - 44)
            throw new IOException("Unexpected recording format.");
        input.position(44);
        StringBuilder text = new StringBuilder();
        synchronized (asrLock) {
            while (input.hasRemaining()) {
                float[] samples = new float[Math.min(24 * RECORD_RATE, input.remaining() / 2)];
                double energy = 0;
                for (int i = 0; i < samples.length; i++) {
                    samples[i] = input.getShort() / 32768f;
                    energy += samples[i] * samples[i];
                }
                if (samples.length < 3200 || energy / samples.length < .000001) continue;
                OfflineRecognizer engine = recognizer(language);
                OfflineStream stream = engine.createStream();
                try {
                    stream.acceptWaveform(samples, RECORD_RATE);
                    engine.decode(stream);
                    String part = engine.getResult(stream).getText().trim();
                    if (!part.isEmpty()) {
                        if (text.length() > 0) text.append(' ');
                        text.append(part);
                    }
                } finally {
                    stream.release();
                }
                if (text.length() >= 7000) break;
            }
        }
        return text.substring(0, Math.min(7000, text.length())).trim();
    }

    private OfflineTts voice(String name) throws IOException {
        if (voice != null && name.equals(voiceName)) return voice;
        if (voice != null) {
            voice.release();
            voice = null;
        }
        OfflineTtsVitsModelConfig vits = new OfflineTtsVitsModelConfig();
        vits.setModel(extract(name + "/voice.onnx").getPath());
        vits.setTokens(extract(name + "/tokens.txt").getPath());
        vits.setDataDir(dataDirectory().getPath());
        OfflineTtsModelConfig model = new OfflineTtsModelConfig();
        model.setVits(vits);
        model.setNumThreads(2);
        OfflineTtsConfig config = new OfflineTtsConfig();
        config.setModel(model);
        config.setMaxNumSentences(1);
        voice = new OfflineTts(null, config);
        voiceName = name;
        return voice;
    }

    void write(String text, String language, JSONObject profile, OutputStream output) throws Exception {
        boolean feminine = profile.optString("voiceStyle").startsWith("F");
        String selected = language.equals("id") ? "id" : feminine ? "en-amy" : "en-ryan";
        float speed = (float)Math.max(.85, Math.min(1.2, profile.optDouble("voiceRate", 1)));
        synchronized (ttsLock) {
            OfflineTts engine = voice(selected);
            Matcher pauses = PAUSES.matcher(text);
            int cursor = 0, frame = 0;
            while (cursor < text.length()) {
                boolean split = pauses.find();
                int end = split ? pauses.start() : text.length();
                if (split && end - cursor < 10) continue;
                GeneratedAudio audio = engine.generate(text.substring(cursor, end), 0, speed);
                float[] samples = audio.getSamples();
                int rate = audio.getSampleRate();
                int count = rate > 0 ? (int)Math.round(samples.length * (double) PLAY_RATE / rate) : 0;
                if (count <= 0 || frame + count > MAX_FRAMES) throw new IOException("Unexpected voice duration.");
                if (frame == 0)
                    line(output, new JSONObject().put("type", "start").put("provider", "piper").put("sampleRate", PLAY_RATE));
                cue(output, frame, cursor);
                cue(output, frame + count, end);
                for (int offset = 0; offset < count; offset += PCM_PACKET) {
                    int n = Math.min(PCM_PACKET, count - offset);
                    ByteBuffer pcm = ByteBuffer.allocate(2 * n).order(ByteOrder.LITTLE_ENDIAN);
                    for (int i = 0; i < n; i++) {
                        double position = (offset + i) * (double) rate / PLAY_RATE;
                        int left = Math.min(samples.length - 1, (int) position);
                        float sample = samples[left] + (float)(position - left) * (samples[Math.min(left + 1, samples.length - 1)] - samples[left]);
                        pcm.putShort((short)Math.round(Math.max(-.96f, Math.min(.96f, sample)) * 32767));
                    }
                    line(output, new JSONObject().put("type", "audio").put("data",
                            Base64.encodeToString(pcm.array(), Base64.NO_WRAP)));
                }
                frame += count;
                cursor = split ? pauses.end() : text.length();
            }
            line(output, new JSONObject().put("type", "end").put("frame", frame).put("chars", text.length()));
        }
    }

    private static void cue(OutputStream output, int frame, int chars) throws Exception {
        line(output, new JSONObject().put("type", "cue").put("frame", frame).put("chars", chars));
    }

    static void line(OutputStream output, JSONObject line) throws IOException {
        output.write(line.toString().getBytes(StandardCharsets.UTF_8));
        output.write('\n');
        output.flush();
    }
}
