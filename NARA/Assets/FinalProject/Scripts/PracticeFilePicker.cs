using System;
using System.Runtime.InteropServices;

namespace Nara.Practice
{
    internal static class PracticeFilePicker
    {
        public static string SelectPresentation()
        {
#if UNITY_EDITOR_WIN
            return UnityEditor.EditorUtility.OpenFilePanelWithFilters("Pilih berkas presentasi", "",
                new[] { "Materi presentasi", "pptx,ppt,pdf,png,jpg,jpeg" });
#elif UNITY_STANDALONE_WIN
            const int capacity = 32768;
            IntPtr buffer = Marshal.AllocHGlobal(capacity * sizeof(char));
            try
            {
                Marshal.WriteInt16(buffer, 0);
                var dialog = new OpenFile
                {
                    size = Marshal.SizeOf<OpenFile>(),
                    owner = GetActiveWindow(),
                    filter = "Materi presentasi\0*.pptx;*.ppt;*.pdf;*.png;*.jpg;*.jpeg\0PowerPoint\0*.pptx;*.ppt\0PDF\0*.pdf\0Gambar\0*.png;*.jpg;*.jpeg\0\0",
                    filterIndex = 1,
                    file = buffer,
                    maxFile = capacity,
                    title = "Pilih berkas presentasi",
                    flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008 | 0x00000004
                };
                return GetOpenFileNameW(ref dialog) ? Marshal.PtrToStringUni(buffer) : "";
            }
            finally { Marshal.FreeHGlobal(buffer); }
#else
            return "";
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR_WIN
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct OpenFile
        {
            public int size;
            public IntPtr owner, module;
            public string filter;
            public IntPtr customFilter;
            public int maxCustomFilter, filterIndex;
            public IntPtr file;
            public int maxFile;
            public IntPtr fileTitle;
            public int maxFileTitle;
            public string initialDirectory, title;
            public int flags;
            public short fileOffset, extensionOffset;
            public string defaultExtension;
            public IntPtr data, hook, template, reserved;
            public int reservedFlags, extendedFlags;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetOpenFileNameW(ref OpenFile parameters);

        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();
#endif
    }
}
