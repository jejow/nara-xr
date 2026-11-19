using System;
using System.Collections.Generic;
using UnityEngine;

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
        public const int SeatCount = 3;
        public const int LeadSeat = 0;
        const string Preference = "Nara.Audience.Seat.";
        static readonly string[] Defaults = { "Female_Adult_13", "Male_Adult_04", "Female_Adult_06" };

        public Transform[] seats;
        readonly NaraAvatarPuppet[] actors = new NaraAvatarPuppet[SeatCount];
        readonly int[] selected = new int[SeatCount];
        public AvatarProfile[] Profiles { get; private set; }
        public Transform HeadAt(int seat) => seat >= 0 && seat < SeatCount && actors[seat] ? actors[seat].Head : null;
        public int Selection(int seat) => selected[seat];
        public AvatarProfile ProfileAt(int seat) => Profiles[selected[seat]];
        public int SeatOf(int profile) => Array.IndexOf(selected, profile);

        public void Initialise()
        {
            Profiles = AvatarCatalog.Load().entries;
            if (seats == null || seats.Length != SeatCount)
                throw new InvalidOperationException("NARA needs three marked audience seats.");
            var occupied = new HashSet<int>();
            for (int seat = 0; seat < SeatCount; seat++)
            {
                string id = PlayerPrefs.GetString(Preference + seat, Defaults[seat]);
                int index = Array.FindIndex(Profiles, item => item.id == id);
                if (index < 0 || occupied.Contains(index))
                    index = Array.FindIndex(Profiles, item => item.id == Defaults[seat]);
                if (index < 0 || occupied.Contains(index))
                {
                    index = 0;
                    while (occupied.Contains(index))
                        index++;
                }
                occupied.Add(index);
                actors[seat] = Spawn(seat, index);
                selected[seat] = index;
            }
        }

        public void Select(int seat, int index, bool save = true)
        {
            if (seat < 0 || seat >= SeatCount || index < 0 || index >= Profiles.Length || index == selected[seat])
                return;
            int other = Array.IndexOf(selected, index);
            if (other >= 0)
            {
                (actors[seat], actors[other]) = (actors[other], actors[seat]);
                (selected[seat], selected[other]) = (selected[other], selected[seat]);
                MoveToSeat(actors[seat], seat);
                MoveToSeat(actors[other], other);
            }
            else
            {
                NaraAvatarPuppet next = Spawn(seat, index);
                actors[seat].gameObject.SetActive(false);
                Destroy(actors[seat].gameObject);
                actors[seat] = next;
                selected[seat] = index;
            }
            if (save)
            {
                for (int i = 0; i < SeatCount; i++)
                    PlayerPrefs.SetString(Preference + i, Profiles[selected[i]].id);
                PlayerPrefs.Save();
            }
        }

        public void Animate(float time, float elapsed, bool listening, float audio, int speakerSeat,
            Vector3 presenterEyes, int focusSeat, bool raisesHand, float nod)
        {
            Transform speaker = HeadAt(speakerSeat);
            Vector3 speakerEyes = speaker ? speaker.position : presenterEyes;
            for (int i = 0; i < SeatCount; i++)
            {
                bool focused = i == focusSeat;
                bool speaking = i == speakerSeat;
                actors[i].Animate(time + i * 1.7f, elapsed, listening, speaking ? audio : 0, speaking,
                    (speaking || (focused && !speaker)) ? presenterEyes : speakerEyes,
                    focused && raisesHand, focused ? nod : 0);
            }
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

        void MoveToSeat(NaraAvatarPuppet actor, int seat)
        {
            actor.transform.SetParent(seats[seat], false);
            actor.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }
    }
}
