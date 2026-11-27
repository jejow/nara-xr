package id.ac.its.nara.quest;

import android.app.Activity;
import android.content.Context;
import android.util.Base64;
import fi.iki.elonen.NanoHTTPD;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.io.PipedInputStream;
import java.io.PipedOutputStream;
import java.net.SocketTimeoutException;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.SecureRandom;
import java.util.Locale;
import java.util.concurrent.Semaphore;
import javax.net.ssl.HttpsURLConnection;
import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

public final class NaraQuest extends NanoHTTPD {
    static final int MAX_DECK = 32_000_000;
    private static final String MODEL = "gpt-6.1-sol";
    private static final String PROVIDER = "https://api.aisubs.id/v1/chat/completions";
    private static volatile NaraQuest instance;

    private final Context context;
    private final NaraCredentials credentials;
    private final NaraSpeech speech;
    private final JSONObject profiles = new JSONObject();
    private final String token, prompt;
    private final Semaphore turnSlot = new Semaphore(1);
    private final Semaphore transcribeSlot = new Semaphore(1);
    private final Semaphore voiceSlot = new Semaphore(1);
    private final Semaphore deckSlot = new Semaphore(1);

    private NaraQuest(Context application, String cast, String prompt) throws JSONException {
        super("127.0.0.1", 0);
        context = application;
        credentials = new NaraCredentials(application);
        speech = new NaraSpeech(application);
        this.prompt = prompt;
        JSONArray entries = new JSONObject(cast).getJSONArray("entries");
        for (int i = 0; i < entries.length(); i++) {
            JSONObject profile = entries.getJSONObject(i);
            profiles.put(profile.getString("id"), profile);
        }
        byte[] random = new byte[32];
        new SecureRandom().nextBytes(random);
        token = Base64.encodeToString(random, Base64.URL_SAFE | Base64.NO_PADDING | Base64.NO_WRAP);
    }

    public static synchronized String start(Activity activity, String cast, String prompt) {
        try {
            if (instance == null) {
                NaraQuest local = new NaraQuest(activity.getApplicationContext(), cast, prompt);
                local.start(12000, true);
                instance = local;
            }
            return new JSONObject().put("endpoint", "http://127.0.0.1:" + instance.getListeningPort())
                    .put("accessToken", instance.token).toString();
        } catch (Exception failure) {
            android.util.Log.e("NARA", "Quest services did not start: " + failure.getClass().getSimpleName());
            return "";
        }
    }

    public static boolean hasKey() {
        return instance != null && instance.credentials.hasKey();
    }

    public static boolean saveKey(String key) {
        return instance != null && instance.credentials.save(key == null ? "" : key.trim());
    }

    public static boolean clearKey() {
        return instance != null && instance.credentials.clear();
    }

    @Override public Response serve(IHTTPSession request) {
        String auth = request.getHeaders().get("authorization");
        if (auth == null || !MessageDigest.isEqual(bytes(auth), bytes("Bearer " + token)))
            return error(Response.Status.UNAUTHORIZED, "unauthorized");

        String route = request.getUri();
        if (request.getMethod() != Method.POST) return error(Response.Status.NOT_FOUND, "route");
        boolean deck = route.equals("/import-deck");
        int limit;
        Semaphore slot;
        switch (route) {
            case "/turn": limit = 8_000_000; slot = turnSlot; break;
            case "/transcribe": limit = 900_000; slot = transcribeSlot; break;
            case "/speech-stream": limit = 3_500; slot = voiceSlot; break;
            case "/import-deck": limit = MAX_DECK; slot = deckSlot; break;
            default: return error(Response.Status.NOT_FOUND, "route");
        }
        String type = request.getHeaders().getOrDefault("content-type", "").split(";", 2)[0].trim();
        if (!type.equals(deck ? "application/octet-stream" : "application/json"))
            return error(Response.Status.UNSUPPORTED_MEDIA_TYPE, "content_type");
        int length;
        try { length = Integer.parseInt(request.getHeaders().getOrDefault("content-length", "0")); }
        catch (NumberFormatException malformed) { length = 0; }
        if (length < 1 || length > limit) return error(Response.Status.PAYLOAD_TOO_LARGE, "size");
        if (!slot.tryAcquire()) return error(Response.Status.SERVICE_UNAVAILABLE, "busy");
        boolean handedOff = false;
        try {
            if (deck) {
                String format = request.getHeaders().getOrDefault("x-nara-deck-format", "");
                String name = request.getHeaders().getOrDefault("x-nara-deck-title", "");
                return NaraDeck.response(context, request.getInputStream(), length, format, name);
            }
            JSONObject input = new JSONObject(new String(read(request.getInputStream(), length, true),
                    StandardCharsets.UTF_8));
            String language = input.getString("language");
            if (!language.equals("id") && !language.equals("en")) throw new JSONException("Language");
            if (route.equals("/transcribe"))
                return json(Response.Status.OK, new JSONObject().put("transcript",
                        speech.recognize(input.getString("audio"), language, 20)));
            if (route.equals("/speech-stream")) {
                JSONObject profile = profiles.getJSONObject(input.getString("speaker"));
                String text = input.getString("text").trim();
                if (text.isEmpty() || text.length() > 420) throw new JSONException("Speech length");
                Response stream = speechStream(text, language, profile, slot);
                handedOff = true;
                return stream;
            }
            String key = credentials.read();
            if (key.isEmpty()) return error(Response.Status.UNAUTHORIZED, "key_missing");
            return json(Response.Status.OK, turn(input, language, key));
        } catch (NaraDeck.BadDeck failure) {
            return error(failure.status, failure.code);
        } catch (ProviderFailure failure) {
            return error(failure.status, "provider");
        } catch (SocketTimeoutException failure) {
            return error(status(504, "Gateway Timeout"), "timeout");
        } catch (IOException | JSONException failure) {
            return error(unprocessable(), "unusable");
        } catch (LinkageError | RuntimeException failure) {
            android.util.Log.e("NARA", "Quest operation failed " + route + ": " + failure.getClass().getSimpleName());
            return error(Response.Status.SERVICE_UNAVAILABLE, "unavailable");
        } finally {
            if (!handedOff) slot.release();
        }
    }

    private Response speechStream(String text, String language, JSONObject profile, Semaphore slot) throws IOException {
        PipedInputStream pipe = new PipedInputStream(32768);
        PipedOutputStream writer = new PipedOutputStream(pipe);
        Thread worker = new Thread(() -> {
            try {
                speech.write(text, language, profile, writer);
            } catch (Exception | LinkageError failure) {
                try { NaraSpeech.line(writer, new JSONObject().put("type", "error").put("error", "voice")); }
                catch (Exception ignored) { }
            } finally {
                try { writer.close(); } catch (IOException ignored) { }
                slot.release();
            }
        }, "nara-speech");
        worker.setDaemon(true);
        worker.start();
        Response response = newChunkedResponse(Response.Status.OK, "application/x-ndjson; charset=utf-8", pipe);
        response.addHeader("Cache-Control", "no-store");
        return response;
    }

    private JSONObject turn(JSONObject input, String language, String key) throws JSONException, IOException {
        String operation = input.getString("operation");
        boolean initial = operation.equals("question_1");
        boolean question = initial || operation.equals("question_2");
        if (!question && !operation.equals("after_second") && !operation.equals("feedback"))
            throw new JSONException("Operation");
        String speaker = input.getString("speaker"), lead = input.getString("lead");
        JSONObject guest = profiles.getJSONObject(speaker), host = profiles.getJSONObject(lead);
        String topic = field(input, "topic", 100);
        if (topic.isEmpty()) throw new JSONException("Topic");
        String transcript = speech.recognize(input.optString("audio"), language, initial ? 180 : 45);
        String spoken = field(input, "presentation", 7000);
        if (initial) {
            spoken = (spoken + " " + transcript).trim();
            spoken = spoken.substring(Math.max(0, spoken.length() - 7000));
            transcript = spoken;
        }
        String answer = !initial && !transcript.isEmpty() ? clip(transcript, 2000) : field(input, "answer", 2000);
        JSONArray history = new JSONArray();
        JSONArray submitted = input.optJSONArray("history");
        if (submitted != null) {
            if (submitted.length() > 3) throw new JSONException("History");
            for (int i = 0; i < submitted.length(); i++) {
                JSONObject value = submitted.getJSONObject(i);
                history.put(new JSONObject().put("question", field(value, "question", 420))
                        .put("answer", !initial && i == submitted.length() - 1 ? answer : field(value, "answer", 2000)));
            }
        }
        String[] pageText = new String[61];
        JSONArray rawSlides = input.optJSONArray("slides");
        if (rawSlides != null) {
            for (int i = 0; i < Math.min(60, rawSlides.length()); i++) {
                JSONObject raw = rawSlides.optJSONObject(i);
                if (raw == null) continue;
                int page = raw.optInt("page");
                if (page >= 1 && page <= 60 && pageText[page] == null) pageText[page] = field(raw, "text", 1500);
            }
        }
        boolean[] visited = new boolean[61];
        JSONArray rawVisited = input.optJSONArray("presentedPages");
        if (rawVisited != null) {
            for (int i = 0; i < Math.min(60, rawVisited.length()); i++) {
                int page = rawVisited.optInt(i);
                if (page >= 1 && page <= 60) visited[page] = true;
            }
        }
        int included = 0;
        for (String text : pageText) if (text != null && !text.isEmpty()) included++;
        int perPage = Math.min(1500, 15000 / Math.max(1, included));
        JSONArray slides = new JSONArray(), viewed = new JSONArray();
        for (int page = 1; page < pageText.length; page++) {
            if (pageText[page] == null) continue;
            if (visited[page]) viewed.put(page);
            if (!pageText[page].isEmpty())
                slides.put(new JSONObject().put("page", page).put("text", clip(pageText[page], perPage)));
        }
        String guestName = guest.getString("label"), hostName = host.getString("label");
        JSONObject material = new JSONObject().put("operation", operation).put("language", language)
                .put("topic", topic).put("summary", field(input, "summary", 1500))
                .put("spoken_explanation", spoken).put("written_explanation", field(input, "writtenPresentation", 7000))
                .put("slides", slides).put("presented_pages", viewed).put("latest_answer", answer)
                .put("history", history).put("questioner", guestName).put("lead", hostName);
        JSONObject reply = complete(material, key);
        String kind = reply.getString("kind"), text = reply.getString("text").trim();
        String anchor = reply.getString("anchor").trim();
        if (text.length() > 420 || anchor.length() > 100) throw new JSONException("Response length");
        int colon = text.indexOf(':');
        if (colon >= 0) {
            String prefix = text.substring(0, colon).trim();
            if (prefix.equalsIgnoreCase(guestName) || prefix.equalsIgnoreCase(hostName))
                text = text.substring(colon + 1).trim();
        }
        boolean feedback = kind.equals("feedback"), followUp = kind.equals("follow_up");
        if (text.isEmpty() || text.split("\\s+").length > 60 || text.indexOf('?') != text.lastIndexOf('?') ||
                question && !kind.equals("question") || operation.equals("feedback") && !feedback ||
                operation.equals("after_second") && !feedback && !followUp ||
                !feedback && !text.endsWith("?") || !followUp && !anchor.isEmpty() ||
                followUp && (anchor.length() < 4 || !lower(answer).contains(lower(anchor))))
            throw new JSONException("Unexpected dialogue");
        for (int i = 0; !feedback && i < history.length(); i++) {
            if (lower(text).replaceAll("\\s+", " ").equals(lower(history.getJSONObject(i).getString("question"))
                    .replaceAll("\\s+", " "))) throw new JSONException("Repeated question");
        }
        return new JSONObject().put("kind", kind).put("text", text).put("anchor", anchor)
                .put("transcript", transcript).put("source", "live").put("speaker", feedback ? lead : speaker)
                .put("audio", "").put("audioDeferred", true).put("audioStream", true);
    }

    private JSONObject complete(JSONObject material, String key) throws IOException, JSONException {
        JSONArray messages = new JSONArray()
                .put(new JSONObject().put("role", "system").put("content", prompt))
                .put(new JSONObject().put("role", "user").put("content", material.toString()));
        byte[] body = bytes(new JSONObject().put("model", MODEL).put("messages", messages)
                .put("response_format", new JSONObject().put("type", "json_object"))
                .put("max_completion_tokens", 384).put("reasoning_effort", "low").toString());
        HttpsURLConnection connection = (HttpsURLConnection) new URL(PROVIDER).openConnection();
        try {
            connection.setConnectTimeout(6000);
            connection.setReadTimeout(32000);
            connection.setInstanceFollowRedirects(false);
            connection.setRequestMethod("POST");
            connection.setDoOutput(true);
            connection.setRequestProperty("Authorization", "Bearer " + key);
            connection.setRequestProperty("Content-Type", "application/json");
            connection.setFixedLengthStreamingMode(body.length);
            try (OutputStream out = connection.getOutputStream()) { out.write(body); }
            int status = connection.getResponseCode();
            if (status != 200) {
                android.util.Log.w("NARA", "AISUBS HTTP " + status);
                throw new ProviderFailure(status == 401 ? Response.Status.UNAUTHORIZED :
                        status == 403 ? Response.Status.FORBIDDEN : status(502, "Bad Gateway"));
            }
            try (InputStream upstream = connection.getInputStream()) {
                JSONObject result = new JSONObject(new String(read(upstream, 128_000, false), StandardCharsets.UTF_8));
                JSONObject first = result.getJSONArray("choices").getJSONObject(0);
                if (!first.optString("finish_reason").equals("stop")) throw new JSONException("Incomplete dialogue");
                return new JSONObject(first.getJSONObject("message").getString("content"));
            }
        } catch (SocketTimeoutException | ProviderFailure failure) {
            throw failure;
        } catch (IOException failure) {
            throw new ProviderFailure(status(502, "Bad Gateway"));
        } finally {
            connection.disconnect();
        }
    }

    private static final class ProviderFailure extends IOException {
        final Response.IStatus status;
        ProviderFailure(Response.IStatus status) { this.status = status; }
    }

    static byte[] read(InputStream input, int limit, boolean exact) throws IOException {
        ByteArrayOutputStream output = new ByteArrayOutputStream(Math.min(32768, limit));
        byte[] buffer = new byte[Math.min(32768, Math.max(1, limit))];
        int remaining = limit;
        while (remaining > 0) {
            int count = input.read(buffer, 0, Math.min(buffer.length, remaining));
            if (count < 0) {
                if (exact) throw new IOException("Incomplete request");
                return output.toByteArray();
            }
            output.write(buffer, 0, count);
            remaining -= count;
        }
        if (!exact && input.read() != -1) throw new IOException("Response too large");
        return output.toByteArray();
    }

    static Response json(Response.Status status, JSONObject payload) {
        Response response = newFixedLengthResponse(status, "application/json; charset=utf-8", payload.toString());
        response.addHeader("Cache-Control", "no-store");
        return response;
    }

    static Response.IStatus unprocessable() { return status(422, "Unprocessable Entity"); }

    private static Response.IStatus status(int code, String title) {
        return new Response.IStatus() {
            @Override public int getRequestStatus() { return code; }
            @Override public String getDescription() { return code + " " + title; }
        };
    }

    static Response error(Response.IStatus status, String code) {
        Response response = newFixedLengthResponse(status, "application/json", "{\"error\":\"" + code + "\"}");
        response.closeConnection(true);
        return response;
    }

    private static String field(JSONObject value, String key, int limit) { return clip(value.optString(key), limit); }
    private static String clip(String value, int limit) {
        String clean = value.trim();
        return clean.substring(0, Math.min(limit, clean.length()));
    }
    private static String lower(String value) { return value.toLowerCase(Locale.ROOT); }
    private static byte[] bytes(String text) { return text.getBytes(StandardCharsets.UTF_8); }
}
