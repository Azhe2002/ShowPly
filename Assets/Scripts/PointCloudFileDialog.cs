using System;
using System.IO;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>File selection in the editor and Windows player, without external plugins.</summary>
public static class PointCloudFileDialog
{
    /// <returns>The selected absolute path, or null/empty when cancelled.</returns>
    public static string SelectFile(string currentPath)
    {
        string directory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            // A partially typed or invalid path should not prevent browsing.
            try
            {
                string candidate = currentPath.Trim().Trim('"');
                candidate = Directory.Exists(candidate) ? candidate : Path.GetDirectoryName(candidate);
                if (!string.IsNullOrEmpty(candidate) && Directory.Exists(candidate))
                    directory = Path.GetFullPath(candidate);
            }
            catch (ArgumentException) { }
            catch (NotSupportedException) { }
            catch (PathTooLongException) { }
        }
#if UNITY_EDITOR
        return UnityEditor.EditorUtility.OpenFilePanelWithFilters("Import point cloud", directory,
            new[] { "Point cloud", "dat,ply", "DAT", "dat", "PLY", "ply" });
#elif UNITY_STANDALONE_WIN
        var dialog = new OpenFileName
        {
            StructSize = Marshal.SizeOf(typeof(OpenFileName)),
            Owner = GetActiveWindow(),
            Filter = "Point cloud (*.dat;*.ply)\0*.dat;*.ply\0DAT (*.dat)\0*.dat\0PLY (*.ply)\0*.ply\0\0",
            FilterIndex = 1,
            MaxFile = 32768,
            InitialDirectory = directory,
            Title = "Import point cloud",
            // Explorer style, existing file/path, preserve working directory, no recent list entry.
            Flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008 | 0x02000000
        };
        dialog.File = Marshal.AllocHGlobal(dialog.MaxFile * sizeof(char));
        try
        {
            Marshal.WriteInt16(dialog.File, 0); // Empty initial file name.
            if (GetOpenFileName(dialog)) return Marshal.PtrToStringUni(dialog.File);
            uint error = CommDlgExtendedError();
            if (error != 0) throw new InvalidOperationException($"Windows file dialog error: 0x{error:X}");
            return null;
        }
        finally { Marshal.FreeHGlobal(dialog.File); }
#else
        throw new PlatformNotSupportedException("File browsing is available in the Unity Editor and Windows player.");
#endif
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    // OPENFILENAMEW layout, including pointer-sized fields for a 64-bit player.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class OpenFileName
    {
        public int StructSize;
        public IntPtr Owner;
        public IntPtr Instance;
        public string Filter;
        public IntPtr CustomFilter;
        public int MaxCustomFilter;
        public int FilterIndex;
        public IntPtr File;
        public int MaxFile;
        public IntPtr FileTitle;
        public int MaxFileTitle;
        public string InitialDirectory;
        public string Title;
        public int Flags;
        public ushort FileOffset;
        public ushort FileExtension;
        public string DefaultExtension;
        public IntPtr CustomData;
        public IntPtr Hook;
        public IntPtr TemplateName;
        public IntPtr Reserved;
        public int ReservedValue;
        public int FlagsEx;
    }

    [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOpenFileName([In, Out] OpenFileName dialog);

    [DllImport("comdlg32.dll", ExactSpelling = true)]
    private static extern uint CommDlgExtendedError();

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr GetActiveWindow();
#endif
}
