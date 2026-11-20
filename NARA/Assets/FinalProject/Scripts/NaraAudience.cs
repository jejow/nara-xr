using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Nara.Practice
{
    [Serializable]
    public sealed class AvatarProfile
    {
        public string id;
        public string label;
        public string gender;
        public int portrait;
        public bool featured;
    }

    [Serializable]
    public sealed class AvatarCatalog
    {
        public const string Resource = "Nara/AvatarCatalog";
        public const string Models = "Nara/Avatars/";
        public const int ProfileCount = 30;
        public const int PortraitColumns = 6;
        public const int PortraitRows = 5;
        public AvatarProfile[] entries;

        public static AvatarCatalog Load()
        {
            var asset = Resources.Load<TextAsset>(Resource);
            var result = asset ? JsonUtility.FromJson<AvatarCatalog>(asset.text) : null;
            if (result?.entries == null || result.entries.Length != ProfileCount)
                throw new InvalidOperationException("The NARA avatar catalog is missing or incomplete.");
            return result;
        }
    }

    public sealed class NaraAudience : MonoBehaviour
    {
        public const int Rows = 4, SeatsPerRow = 6;
        public const int ClassroomSeatCount = Rows * SeatsPerRow;

        public Transform[] seats;
        NaraAvatarPuppet[] actors, pool;
        Bounds?[] desks;
        int[] selected, order;
        int previousAttentionSeat = -1, seatingRound, slideChange;
        float attentionChangedAt, slideChangedAt = -100;
        Vector3 slideCenter;
        public AvatarProfile[] Profiles { get; private set; }
        public int SeatCount => seats.Length;
        public Transform HeadAt(int seat) => seat >= 0 && seat < SeatCount && actors[seat] ? actors[seat].Head : null;
        public AvatarProfile ProfileAt(int seat) => Profiles[selected[seat]];
        public static string SeatLabel(int seat) => $"Baris {seat / SeatsPerRow + 1} · Kursi {seat % SeatsPerRow + 1}";

        public void Initialise()
        {
            Profiles = AvatarCatalog.Load().entries;
            if (seats == null || seats.Length < 2 || seats.Length > Profiles.Length || Array.Exists(seats, seat => !seat))
                throw new InvalidOperationException("NARA needs a marked seat for each audience member.");
            actors = new NaraAvatarPuppet[SeatCount];
            selected = new int[SeatCount];
            pool = new NaraAvatarPuppet[Profiles.Length];
            desks = FindSeatDesks();
            order = new int[Profiles.Length];
            for (int i = 0; i < order.Length; i++)
                order[i] = i;
            Shuffle();
        }

        internal Bounds?[] FindSeatDesks()
        {
            var result = new Bounds?[SeatCount];
            foreach (BoxCollider top in transform.parent.GetComponentsInChildren<BoxCollider>(true))
            {
                if (!top.name.StartsWith("Table white trapezoidal top", StringComparison.Ordinal)) continue;
                Bounds bounds = top.bounds;
                int nearest = -1;
                float distance = 1.2f * 1.2f;
                for (int i = 0; i < SeatCount; i++)
                {
                    Vector3 delta = Vector3.ProjectOnPlane(bounds.center - seats[i].position, Vector3.up);
                    if (Vector3.Dot(delta, -seats[i].forward) < .12f || delta.sqrMagnitude >= distance) continue;
                    distance = delta.sqrMagnitude;
                    nearest = i;
                }
                if (nearest >= 0) result[nearest] = bounds;
            }
            return result;
        }

        public void Shuffle()
        {
            seatingRound++;
            previousAttentionSeat = -1;
            slideChangedAt = -100;
            foreach (NaraAvatarPuppet actor in actors)
            {
                if (actor)
                    actor.gameObject.SetActive(false);
            }
            for (int seat = 0; seat < SeatCount; seat++)
            {
                int draw = Random.Range(seat, order.Length);
                (order[seat], order[draw]) = (order[draw], order[seat]);
                int profile = selected[seat] = order[seat];
                NaraAvatarPuppet actor = pool[profile];
                if (!actor)
                    actor = pool[profile] = Spawn(seat, profile);
                actor.transform.SetParent(seats[seat], false);
                actor.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                actor.gameObject.SetActive(true);
                actor.SetDesk(desks[seat]);
                actor.SetMotionProfile(profile, Time.unscaledTime, seat, seatingRound);
                actors[seat] = actor;
            }
        }

        internal (int first, int second) DrawQuestionSeats()
        {
            int first = Random.Range(0, SeatCount);
            int second = Random.Range(0, SeatCount - 1);
            return (first, second < first ? second : second + 1);
        }

        public void SlideChanged(Vector3 position, float time)
        {
            slideCenter = position;
            slideChangedAt = time;
            slideChange++;
        }

        public void Animate(float time, bool listening, float audio, float articulation, int speakerSeat,
            Vector3 presenterEyes, int focusSeat, bool raisesHand, float nod,
            AudienceLine line = AudienceLine.Notice, float lineProgress = 0)
        {
            if (!isActiveAndEnabled)
                return;
            int attentionSeat = speakerSeat >= 0 ? speakerSeat : raisesHand ? focusSeat : -1;
            if (attentionSeat != previousAttentionSeat)
            {
                previousAttentionSeat = attentionSeat;
                attentionChangedAt = time;
            }
            Transform peer = HeadAt(attentionSeat);
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < SeatCount; i++)
            {
                bool focused = i == focusSeat;
                bool speaking = i == speakerSeat;
                float reactionDelay = .17f + Mathf.Repeat(selected[i] * .367f, 1) * .64f;
                float interest = 0;
                Vector3 target = presenterEyes;
                if (peer && i != attentionSeat && CanTurnToward(i, peer.position))
                {
                    interest = Mathf.SmoothStep(0, 1, (time - attentionChangedAt - reactionDelay) / .45f);
                    target = Vector3.Lerp(presenterEyes, peer.position, interest);
                }
                else if (!peer && listening && (i + slideChange) % 4 != 0)
                {
                    float elapsed = time - slideChangedAt - 1.55f * reactionDelay;
                    float hold = 1.4f + 1.5f * Mathf.Repeat(selected[i] * .173f, 1);
                    interest = Mathf.SmoothStep(0, 1, elapsed / .45f) * (1 - Mathf.SmoothStep(0, 1, (elapsed - hold) / .7f));
                    target = Vector3.Lerp(presenterEyes, slideCenter, interest);
                }
                actors[i].Animate(time, dt, listening || interest > 0, speaking ? audio : 0, speaking, target,
                    focused && raisesHand, focused ? nod : 0, speaking ? articulation : .5f, interest, line, lineProgress);
            }
        }

        bool CanTurnToward(int seat, Vector3 point)
        {
            Transform anchor = seats[seat];
            Vector3 direction = Vector3.ProjectOnPlane(point - anchor.position, Vector3.up).normalized;
            return Vector3.Dot(-anchor.forward, direction) > -.259f;
        }

        NaraAvatarPuppet Spawn(int seat, int index)
        {
            AvatarProfile profile = Profiles[index];
            var item = Resources.Load<GameObject>(AvatarCatalog.Models + profile.id);
            var prefab = item ? item.GetComponent<NaraAvatarPuppet>() : null;
            if (!prefab)
                throw new InvalidOperationException("Avatar prefab missing for " + profile.id);
            return Instantiate(prefab, seats[seat], false);
        }

    }
}
