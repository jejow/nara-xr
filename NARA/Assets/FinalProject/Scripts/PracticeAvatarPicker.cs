using UnityEngine;
using UnityEngine.UI;

namespace Nara.Practice
{
    public sealed partial class PracticeView
    {
        const int AvatarsPerPage = 12;
        static readonly string[] SeatNames = { "Penanya NARA", "Audiens kiri", "Audiens kanan" };
        readonly Button[] avatarTiles = new Button[AvatarsPerPage];
        readonly RawImage[] portraitTiles = new RawImage[AvatarsPerPage];
        readonly Button[] seatButtons = new Button[NaraAudience.SeatCount];
        RectTransform avatarPanel;
        Text avatarStatus, pageStatus;
        Button earlierPage, laterPage;
        bool avatarPickerOpen;
        int avatarSeat, avatarPage;

        public bool PickingAvatars => avatarPickerOpen && app.Session.Phase == PracticePhase.Setup;

        void BuildAvatarPicker(RectTransform root)
        {
            avatarPanel = Card(root, "Pilihan karakter", Dark, 160, 118, 1120, 730);
            Label(avatarPanel, "Karakter audiens", 40, FontStyle.Bold, White, 32, 20, 857, 55);
            Control(avatarPanel, "Tutup", 917, 25, 171, 50, ToggleAvatarPicker);
            for (int i = 0; i < SeatNames.Length; i++)
            {
                int seat = i;
                seatButtons[i] = Control(avatarPanel, SeatNames[i], 32 + i * 356, 92, 344, 52, () => SetAvatarSeat(seat));
            }
            avatarStatus = Label(avatarPanel, "", 20, FontStyle.Bold, Tint, 32, 152, 1056, 39);
            Texture2D portraits = Resources.Load<Texture2D>("Nara/AvatarPortraits");
            for (int i = 0; i < AvatarsPerPage; i++)
            {
                int tile = i;
                float x = 32 + 177 * (i % 6);
                float y = 202 + 195 * (i / 6);
                var box = Panel(avatarPanel, "Avatar", Raised, x, y, 165, 184);
                avatarTiles[i] = MakeButton(box, () => ApplyAvatarChoice(avatarPage * AvatarsPerPage + tile));
                var photo = Group(box, "Potret", 12, 9, 141, 141).gameObject.AddComponent<RawImage>();
                photo.texture = portraits;
                photo.raycastTarget = false;
                portraitTiles[i] = photo;
                Label(box, "", 19, FontStyle.Bold, White, 7, 149, 151, 33, TextAnchor.MiddleCenter, 17);
            }
            earlierPage = Control(avatarPanel, "Sebelumnya", 32, 603, 237, 53, () => BrowseAvatars(-1));
            pageStatus = Label(avatarPanel, "", 19, FontStyle.Bold, Tint, 444, 603, 230, 53, TextAnchor.MiddleCenter);
            laterPage = Control(avatarPanel, "Berikutnya", 851, 603, 237, 53, () => BrowseAvatars(1));
            Label(avatarPanel, "Pilih potret untuk mengisi kursi  ·  Esc kembali", 19, FontStyle.Normal, Muted,
                32, 674, 1056, 34, TextAnchor.MiddleCenter);
            Show(avatarPanel, false);
        }

        public void ToggleAvatarPicker()
        {
            if (app.Session.Phase != PracticePhase.Setup)
                return;
            avatarPickerOpen = !avatarPickerOpen;
            ClearFocus();
            if (avatarPickerOpen)
                SetAvatarSeat(avatarSeat);
            Refresh();
        }

        void SetAvatarSeat(int seat)
        {
            avatarSeat = seat;
            avatarPage = app.audience.Selection(seat) / AvatarsPerPage;
            RefreshAvatarChoices();
        }

        public void NextAvatarSeat()
        {
            if (PickingAvatars)
                SetAvatarSeat((avatarSeat + 1) % NaraAudience.SeatCount);
        }

        public void StepAvatar(int delta)
        {
            if (!PickingAvatars)
                return;
            int count = app.audience.Profiles.Length;
            int index = (app.audience.Selection(avatarSeat) + count + delta) % count;
            avatarPage = index / AvatarsPerPage;
            ApplyAvatarChoice(index);
        }

        void BrowseAvatars(int delta)
        {
            int last = (app.audience.Profiles.Length - 1) / AvatarsPerPage;
            avatarPage = Mathf.Clamp(avatarPage + delta, 0, last);
            RefreshAvatarChoices();
        }

        void ApplyAvatarChoice(int index)
        {
            if (!PickingAvatars || index >= app.audience.Profiles.Length)
                return;
            app.audience.Select(avatarSeat, index);
            RefreshAvatarChoices();
        }

        void RefreshAvatarChoices()
        {
            AvatarProfile[] profiles = app.audience.Profiles;
            int selection = app.audience.Selection(avatarSeat);
            for (int i = 0; i < seatButtons.Length; i++)
                Choice(seatButtons[i], avatarSeat == i);
            Set(avatarStatus, $"Avatar saat ini · {profiles[selection].label}");
            int first = avatarPage * AvatarsPerPage;
            for (int i = 0; i < avatarTiles.Length; i++)
            {
                int index = first + i;
                bool visible = index < profiles.Length;
                avatarTiles[i].gameObject.SetActive(visible);
                if (!visible)
                    continue;
                portraitTiles[i].uvRect = PortraitUv(index);
                Choice(avatarTiles[i], index == selection);
                SetButton(avatarTiles[i], index == selection ? "✓ " + profiles[index].label : profiles[index].label);
            }
            int last = Mathf.Min(first + AvatarsPerPage, profiles.Length);
            Set(pageStatus, $"{first + 1}–{last} dari {profiles.Length}");
            earlierPage.interactable = avatarPage > 0;
            laterPage.interactable = last < profiles.Length;
        }

        static Rect PortraitUv(int index) => new((index % 6) / 6f, 1 - (index / 6 + 1) / 4f, 1 / 6f, 1 / 4f);
    }
}
