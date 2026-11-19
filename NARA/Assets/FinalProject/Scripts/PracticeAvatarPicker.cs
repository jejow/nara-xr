using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Nara.Practice
{
    public sealed partial class PracticeView
    {
        enum AvatarGender { All, Female, Male }

        const int PortraitColumns = AvatarCatalog.PortraitColumns, PortraitRows = AvatarCatalog.PortraitRows;
        const int AvatarsPerPage = PortraitColumns * 2;
        static readonly string[] SeatNames = { "Karakter utama", "Audiens kiri", "Audiens kanan" };
        static readonly string[] GenderNames = { "Semua", "Perempuan", "Laki-laki" };
        readonly Button[] avatarTiles = new Button[AvatarsPerPage];
        readonly RawImage[] portraitTiles = new RawImage[AvatarsPerPage];
        readonly RectTransform[] featuredTiles = new RectTransform[AvatarsPerPage];
        readonly Button[] seatButtons = new Button[NaraAudience.SeatCount];
        readonly Button[] genderButtons = new Button[GenderNames.Length];
        readonly List<int> avatarChoices = new();
        RectTransform avatarPanel;
        Text avatarStatus, pageStatus;
        Button earlierPage, laterPage;
        AvatarGender avatarGender;
        bool avatarPickerOpen;
        int avatarSeat, avatarPage;

        public bool PickingAvatars => avatarPickerOpen && app.Session.Phase == PracticePhase.Setup;
        string LeadName => app.audience.ProfileAt(NaraAudience.LeadSeat).label;

        void BuildAvatarPicker(RectTransform root)
        {
            avatarPanel = Card(root, "Pilihan karakter", Dark, 160, 92, 1120, 744);
            Label(avatarPanel, "Pilih karakter", 40, FontStyle.Bold, White, 32, 20, 837, 55);
            Control(avatarPanel, "Tutup", 917, 25, 171, 50, ToggleAvatarPicker);
            for (int i = 0; i < SeatNames.Length; i++)
            {
                int seat = i;
                seatButtons[i] = Control(avatarPanel, SeatNames[i], 32 + i * 356, 92, 344, 52, () => SetAvatarSeat(seat));
            }

            avatarStatus = Label(avatarPanel, "", 22, FontStyle.Bold, Tint, 32, 157, 437, 46, TextAnchor.MiddleLeft, 16);
            for (int i = 0; i < GenderNames.Length; i++)
            {
                var filter = (AvatarGender)i;
                genderButtons[i] = Control(avatarPanel, GenderNames[i], 480 + i * 132, 157, 127, 46,
                    () => SetAvatarGender(filter), false, 16);
            }
            Control(avatarPanel, "Dengar suara", 879, 157, 209, 46, PreviewSelectedAvatar, false, 17);

            Texture2D portraits = Resources.Load<Texture2D>("Nara/AvatarPortraits");
            for (int i = 0; i < AvatarsPerPage; i++)
            {
                int tile = i;
                float x = 32 + 177 * (i % PortraitColumns);
                float y = 220 + 192 * (i / PortraitColumns);
                var box = Panel(avatarPanel, "Avatar", Raised, x, y, 165, 181);
                avatarTiles[i] = MakeButton(box, () => SelectAvatarTile(tile));
                var photo = Group(box, "Potret", 12, 9, 141, 141).gameObject.AddComponent<RawImage>();
                photo.texture = portraits;
                photo.raycastTarget = false;
                portraitTiles[i] = photo;
                Label(box, "", 19, FontStyle.Bold, White, 7, 149, 151, 31, TextAnchor.MiddleCenter, 16);
                var featured = Panel(box, "Opsi utama", Ink, 19, 16, 86, 24);
                Label(featured, "Opsi utama", 12, FontStyle.Bold, Tint, 0, 0, 86, 24, TextAnchor.MiddleCenter);
                featuredTiles[i] = featured;
            }
            earlierPage = Control(avatarPanel, "Sebelumnya", 32, 617, 237, 52, () => BrowseAvatars(-1));
            pageStatus = Label(avatarPanel, "", 19, FontStyle.Bold, Tint, 424, 617, 272, 52, TextAnchor.MiddleCenter);
            laterPage = Control(avatarPanel, "Berikutnya", 851, 617, 237, 52, () => BrowseAvatars(1));
            Label(avatarPanel, "← / → Ganti karakter   ·   Tab Pindah fokus   ·   Esc Kembali", 19, FontStyle.Normal, Muted,
                32, 687, 1056, 34, TextAnchor.MiddleCenter);
            Show(avatarPanel, false);
        }

        public void ToggleAvatarPicker()
        {
            if (app.Session.Phase != PracticePhase.Setup)
                return;
            avatarPickerOpen = !avatarPickerOpen;
            ClearFocus();
            if (avatarPickerOpen)
                SetAvatarSeat(NaraAudience.LeadSeat);
            else
                app.StopAvatarPreview();
            Refresh();
            if (!xrView)
            {
                if (avatarPickerOpen)
                    seatButtons[NaraAudience.LeadSeat].Select();
                else if (leadChoice.gameObject.activeInHierarchy)
                    leadChoice.Select();
            }
        }

        void SetAvatarSeat(int seat)
        {
            app.StopAvatarPreview();
            avatarSeat = seat;
            SetAvatarGender(AvatarGender.All);
        }

        void SetAvatarGender(AvatarGender gender)
        {
            avatarGender = gender;
            avatarChoices.Clear();
            AvatarProfile[] profiles = app.audience.Profiles;
            string filter = gender == AvatarGender.Female ? "female" : "male";
            int featuredCount = 0;
            for (int i = 0; i < profiles.Length; i++)
            {
                if (gender != AvatarGender.All && profiles[i].gender != filter)
                    continue;
                if (avatarSeat == NaraAudience.LeadSeat && profiles[i].featured)
                    avatarChoices.Insert(featuredCount++, i);
                else
                    avatarChoices.Add(i);
            }
            int position = avatarChoices.IndexOf(app.audience.Selection(avatarSeat));
            avatarPage = position < 0 ? 0 : position / AvatarsPerPage;
            RefreshAvatarChoices();
        }

        public void CycleAvatarGender()
        {
            if (PickingAvatars)
                SetAvatarGender((AvatarGender)(((int)avatarGender + 1) % GenderNames.Length));
        }

        public void NextAvatarSeat()
        {
            if (PickingAvatars)
                SetAvatarSeat((avatarSeat + 1) % NaraAudience.SeatCount);
        }

        public void PreviewSelectedAvatar()
        {
            if (PickingAvatars)
                app.PreviewAvatar(app.audience.Selection(avatarSeat), english);
        }

        public void StepAvatar(int delta)
        {
            if (!PickingAvatars || avatarChoices.Count == 0)
                return;
            int position = avatarChoices.IndexOf(app.audience.Selection(avatarSeat));
            if (position < 0)
                position = delta > 0 ? -1 : 0;
            position = (position + avatarChoices.Count + delta) % avatarChoices.Count;
            avatarPage = position / AvatarsPerPage;
            ApplyAvatarChoice(avatarChoices[position]);
        }

        void BrowseAvatars(int delta)
        {
            int last = (avatarChoices.Count - 1) / AvatarsPerPage;
            avatarPage = Mathf.Clamp(avatarPage + delta, 0, last);
            RefreshAvatarChoices();
        }

        void SelectAvatarTile(int tile)
        {
            int position = avatarPage * AvatarsPerPage + tile;
            if (position < avatarChoices.Count)
                ApplyAvatarChoice(avatarChoices[position]);
        }

        void ApplyAvatarChoice(int index)
        {
            if (!PickingAvatars || index == app.audience.Selection(avatarSeat))
                return;
            app.StopAvatarPreview();
            app.audience.Select(avatarSeat, index);
            RefreshAvatarChoices();
        }

        void RefreshAvatarChoices()
        {
            AvatarProfile[] profiles = app.audience.Profiles;
            int selection = app.audience.Selection(avatarSeat);
            for (int i = 0; i < seatButtons.Length; i++)
            {
                bool active = avatarSeat == i;
                Choice(seatButtons[i], active);
                SetButton(seatButtons[i], active ? "✓ " + SeatNames[i] : SeatNames[i]);
            }
            for (int i = 0; i < genderButtons.Length; i++)
            {
                bool active = i == (int)avatarGender;
                Choice(genderButtons[i], active);
                SetButton(genderButtons[i], active ? "✓ " + GenderNames[i] : GenderNames[i]);
            }
            Set(avatarStatus, $"{SeatNames[avatarSeat]} · {profiles[selection].label}");
            int first = avatarPage * AvatarsPerPage;
            for (int i = 0; i < avatarTiles.Length; i++)
            {
                int position = first + i;
                bool visible = position < avatarChoices.Count;
                Show(avatarTiles[i], visible);
                if (!visible)
                    continue;
                int index = avatarChoices[position];
                AvatarProfile profile = profiles[index];
                portraitTiles[i].uvRect = PortraitUv(profile.portrait);
                Show(featuredTiles[i], avatarSeat == NaraAudience.LeadSeat && profile.featured);
                Choice(avatarTiles[i], index == selection);
                SetButton(avatarTiles[i], index == selection ? "✓ " + profile.label : profile.label);
            }
            int last = Mathf.Min(first + AvatarsPerPage, avatarChoices.Count);
            Set(pageStatus, last == 0 ? "Pilihan tidak tersedia" : $"{first + 1}–{last} dari {avatarChoices.Count}");
            earlierPage.interactable = avatarPage > 0;
            laterPage.interactable = last < avatarChoices.Count;
        }

        static Rect PortraitUv(int index) =>
            new((index % PortraitColumns) / (float)PortraitColumns, 1 - (index / PortraitColumns + 1) / (float)PortraitRows,
                1f / PortraitColumns, 1f / PortraitRows);
    }
}
