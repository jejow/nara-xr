using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Nara.Practice
{
    public sealed partial class PracticeView
    {
        const float SpeechWidth = 456f, Tail = 12f;
        const int LinesPerPage = 4;
        readonly TextGenerator speechLayout = new();
        readonly List<(int start, string text)> speechPages = new();
        readonly Vector3[] speechCorners = new Vector3[4];
        RectTransform speechCanvas, speechWorld, speechCard, speechPointer;
        Text speechName, speechCaption;
        string measuredSpeech;
        float measuredWidth, measuredDensity;
        int readingPage;
        internal bool SpeechBubbleVisible => speechCanvas && speechCanvas.gameObject.activeInHierarchy;
        string SpokenPage => speechCaption.text;
        bool CanPageQuestion => app.Session.Phase is (PracticePhase.Question or PracticePhase.Asking) &&
            app.SpeakingSeat < 0 && measuredSpeech == app.Session.Question && speechPages.Count > 1;

        internal bool TurnQuestionPage(int delta)
        {
            if (!CanPageQuestion)
                return false;
            readingPage = Mathf.Clamp(readingPage + delta, 0, speechPages.Count - 1);
            Refresh();
            return true;
        }

        void BuildSpeechBubble()
        {
            var host = new GameObject("Dialog ruang XR", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            speechWorld = (RectTransform)host.transform;
            speechWorld.SetParent(app.room.transform, false);
            speechWorld.sizeDelta = Vector2.zero;
            var world = host.GetComponent<Canvas>();
            world.renderMode = RenderMode.WorldSpace;
            world.worldCamera = app.practiceCamera;
            host.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 2;

            speechCanvas = Group((RectTransform)canvas.transform, "Dialog NPC", 0, 0, SpeechWidth, 100);
            speechCanvas.anchorMin = speechCanvas.anchorMax = Vector2.zero;
            speechCanvas.pivot = new Vector2(.5f, 0);
            speechPointer = Panel(speechCanvas, "Penunjuk pembicara", Navy, 0, 0, 16, 16, false);
            speechPointer.anchorMin = speechPointer.anchorMax = new Vector2(.5f, 0);
            speechPointer.pivot = new Vector2(.5f, .5f);
            speechPointer.localRotation = Quaternion.Euler(0, 0, 45);
            speechCard = Panel(speechCanvas, "Ucapan", Navy, 0, 0, SpeechWidth, 100);
            speechName = Label(speechCard, "", 19, FontStyle.Bold, Tint, 16, 8, SpeechWidth - 32, 27);
            speechCaption = Label(speechCard, "", 24, FontStyle.Normal, White, 16, 37, SpeechWidth - 32, 54,
                TextAnchor.UpperLeft);
            Show(speechWorld, false);
            Show(speechCanvas, false);
        }

        void SetSpeechMode()
        {
            speechCanvas.SetParent(xrView ? speechWorld : (RectTransform)canvas.transform, false);
            speechCanvas.localScale = Vector3.one;
            speechCanvas.localRotation = Quaternion.identity;
            Show(speechWorld, xrView);
            measuredSpeech = null;
        }

        internal void SetSpeechPage(string line, float width, float progress, int manualPage = -1)
        {
            float contentWidth = width - 32;
            float density = speechCaption.pixelsPerUnit;
            bool reflow = line != measuredSpeech || width != measuredWidth || !Mathf.Approximately(density, measuredDensity);
            if (reflow)
            {
                measuredSpeech = line;
                measuredWidth = width;
                measuredDensity = density;
                speechPages.Clear();
                var settings = speechCaption.GetGenerationSettings(new Vector2(contentWidth, 0));
                settings.verticalOverflow = VerticalWrapMode.Overflow;
                speechLayout.Populate(line, settings);
                var lines = speechLayout.lines;
                int start = 0;
                for (int i = LinesPerPage; i < lines.Count; i += LinesPerPage)
                {
                    int end = Mathf.Min(line.Length, lines[i].startCharIdx);
                    if (end > start)
                        speechPages.Add((start, line.Substring(start, end - start).TrimEnd()));
                    start = end;
                }
                if (start < line.Length)
                    speechPages.Add((start, line.Substring(start)));
                speechCaption.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contentWidth);
                speechName.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contentWidth);
            }
            int page = Mathf.Clamp(manualPage, 0, Mathf.Max(0, speechPages.Count - 1));
            while (manualPage < 0 && page + 1 < speechPages.Count && speechPages[page + 1].start <= progress * line.Length)
                page++;
            string current = speechPages.Count > 0 ? speechPages[page].text : line;
            if (!reflow && speechCaption.text == current)
                return;
            Set(speechCaption, current);
            float height = Mathf.Ceil(Mathf.Max(30, speechCaption.preferredHeight)) + 4;
            speechCaption.rectTransform.sizeDelta = new Vector2(contentWidth, height);
            speechCanvas.sizeDelta = speechCard.sizeDelta = new Vector2(width, 47 + height);
        }

        void RefreshSpeechBubble(PracticePhase phase)
        {
            int seat = app.SpeakingSeat;
            Transform head = app.audience.HeadAt(seat);
            bool manual = !head && phase is (PracticePhase.Question or PracticePhase.Asking) &&
                !string.IsNullOrWhiteSpace(app.Session.Question);
            if (!manual && (!head || string.IsNullOrWhiteSpace(app.SpeechText)))
            {
                Show(speechCanvas, false);
                return;
            }
            var camera = app.practiceCamera;
            var desktop = (RectTransform)canvas.transform;
            Vector2 units = xrView ? new Vector2(1100f * camera.aspect, 1100f) :
                Vector2.Scale(desktop.rect.size, camera.rect.size);
            float width = Mathf.Min(SpeechWidth, units.x * .72f);
            if (manual)
            {
                SetSpeechPage(app.Session.Question, width, 0, readingPage);
                readingPage = Mathf.Min(readingPage, Mathf.Max(0, speechPages.Count - 1));
                Show(speechCanvas, false);
                return;
            }
            SetSpeechPage(app.SpeechText, width, app.SubtitleProgress);
            if (app.Baseline || app.ViewingProjection || phase is PracticePhase.Setup or PracticePhase.Review ||
                !PlaceSpeech(camera, head, units))
            {
                Show(speechCanvas, false);
                return;
            }
            Set(speechName, app.audience.ProfileAt(seat).label);
            Show(speechCanvas, true);
        }

        bool PlaceSpeech(Camera camera, Transform head, Vector2 units)
        {
            Vector3 face = head.position + Vector3.up * .07f;
            Vector3 faceView = camera.WorldToViewportPoint(face);
            if (faceView.z < .6f || faceView.x is < 0 or > 1 || faceView.y is < 0 or > 1 ||
                Blocked(camera.transform.position, face))
                return false;
            Rect safe = Rect.MinMaxRect(.025f, .15f, .975f, .97f);
            if (!xrView && !camera.targetTexture)
            {
                Rect display = Screen.safeArea, pixels = camera.pixelRect;
                safe = Rect.MinMaxRect(Mathf.Max(safe.xMin, (display.xMin - pixels.xMin) / pixels.width),
                    Mathf.Max(safe.yMin, (display.yMin - pixels.yMin) / pixels.height),
                    Mathf.Min(safe.xMax, (display.xMax - pixels.xMin) / pixels.width),
                    Mathf.Min(safe.yMax, (display.yMax - pixels.yMin) / pixels.height));
            }

            Vector2 size = speechCanvas.rect.size;
            float half = size.x / (2 * units.x), height = size.y / units.y, tail = Tail / units.y;
            if (2 * half > safe.width || height + tail > safe.height)
                return false;
            float x = Mathf.Clamp(faceView.x, safe.xMin + half, safe.xMax - half);
            float above = camera.WorldToViewportPoint(head.position + Vector3.up * .245f).y + tail;
            float below = camera.WorldToViewportPoint(head.position - Vector3.up * .14f).y - tail - height;
            bool under = above + height > safe.yMax;
            float y = under ? below : Mathf.Max(above, safe.yMin + tail);
            if (y < safe.yMin || y + height + (under ? tail : 0) > safe.yMax ||
                Mathf.Abs(faceView.x - x) > half + .012f)
                return false;
            speechPointer.anchoredPosition = new Vector2(
                Mathf.Clamp((faceView.x - x) * units.x, 18 - size.x / 2, size.x / 2 - 18), under ? size.y - 1 : 1);

            if (!xrView)
            {
                speechCanvas.anchoredPosition = Vector2.Scale(new Vector2(x, y) * camera.rect.size + camera.rect.min,
                    ((RectTransform)canvas.transform).rect.size);
                return true;
            }

            // Keep text at a readable binocular distance. Check the whole panel swept
            // from the viewer so even a wall edge between the centre and a corner counts.
            float depth = Mathf.Min(2.25f, faceView.z - .22f);
            for (int attempt = 0; attempt < 3 && depth >= .72f; attempt++)
            {
                var center = camera.ViewportToWorldPoint(new Vector3(x, y + height * .5f, depth));
                float span = Vector3.Distance(camera.ViewportToWorldPoint(new Vector3(.5f, 1, depth)),
                    camera.ViewportToWorldPoint(new Vector3(.5f, 0, depth)));
                float scale = span / units.y;
                Vector2 extent = new(size.x * scale / 2, (size.y * .5f + Tail) * scale);
                Quaternion facing = camera.transform.rotation;
                float clear = ClearFraction(camera.transform.position, center, facing, extent);
                if (clear < 1)
                {
                    depth = depth * clear - .055f;
                    continue;
                }
                speechWorld.SetPositionAndRotation(camera.ViewportToWorldPoint(new Vector3(x, y, depth)), facing);
                speechWorld.localScale = Vector3.one * scale;
                speechCanvas.anchoredPosition = Vector2.zero;
                if (camera.stereoEnabled)
                {
                    speechCanvas.GetWorldCorners(speechCorners);
                    foreach (Vector3 corner in speechCorners)
                        if (!InEye(camera, corner, Camera.MonoOrStereoscopicEye.Left) ||
                            !InEye(camera, corner, Camera.MonoOrStereoscopicEye.Right))
                            return false;
                }
                return true;
            }
            return false;
        }

        // A full-size box at the eye can graze a wall *behind* a presenter who
        // turns their head. Enclose the widening view volume in four short sweeps.
        static float ClearFraction(Vector3 eyes, Vector3 center, Quaternion facing, Vector2 extent)
        {
            Vector3 travel = center - eyes;
            float distance = travel.magnitude, from = 0;
            Vector3 direction = travel / distance;
            for (int i = 1; i <= 4; i++)
            {
                float to = i * i / 16f;
                Vector3 half = new(extent.x * to + .034f, extent.y * to + .005f, .006f);
                Vector3 origin = eyes + travel * from;
                if (Physics.CheckBox(origin, half, facing, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    return from;
                if (Physics.BoxCast(origin, half, direction, out RaycastHit hit, facing,
                    distance * (to - from) + .012f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    return from + hit.distance / distance;
                from = to;
            }
            return 1;
        }

        static bool Blocked(Vector3 viewer, Vector3 face) =>
            Physics.Linecast(viewer, face, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
            Physics.Linecast(face, viewer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        static bool InEye(Camera camera, Vector3 point, Camera.MonoOrStereoscopicEye eye)
        {
            Vector3 p = camera.WorldToViewportPoint(point, eye);
            return p.z > camera.nearClipPlane && p.x is > .025f and < .975f && p.y is > .03f and < .97f;
        }
    }
}
