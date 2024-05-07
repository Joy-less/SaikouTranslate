using System;
using Godot;

public static class FileUtility {
    public static void Extract(string Path, string Directory, bool OverwriteFiles = true) {
        // Open zip file
        using ZipReader ZipReader = new();
        if (ZipReader.Open(Path) is not Error.Ok) {
            throw new Exception($"File not found: {Path}");
        }

        // Create target directory
        DirAccess.MakeDirRecursiveAbsolute(Directory);

        // Get each nested zipped file
        foreach (string FilePath in ZipReader.GetFiles()) {
            // Get target path for file
            string TargetFilePath = Directory.PathJoin(FilePath);
            // Ensure file doesn't already exist
            if (!OverwriteFiles && FileAccess.FileExists(TargetFilePath)) {
                continue;
            }
            // Create target sub directory
            DirAccess.Open(Directory).MakeDirRecursive(FilePath.GetBaseDir());
            // Create target file
            FileAccess TargetFile = FileAccess.Open(TargetFilePath, FileAccess.ModeFlags.Write);
            // Extract zipped file to target file
            TargetFile?.StoreBuffer(ZipReader.ReadFile(FilePath));
        }
    }
}