using System;
using System.IO;
using Luban;
using UnityEngine;

namespace ElementalBackHero
{
    public sealed class StreamingAssetsGameDataByteLoader : IGameDataByteLoader
    {
        private const string GameDataDirectoryName = "GameData";
        private const string BytesDirectoryName = "bytes";
        private const string BytesExtension = ".bytes";

        public ByteBuf Load(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("GameData bytes file name must not be empty.", nameof(fileName));
            }

            if (Path.GetFileName(fileName) != fileName || fileName.IndexOfAny(new[] { '/', '\\' }) >= 0)
            {
                throw new ArgumentException(
                    $"GameData bytes file name must not contain path separators: {fileName}",
                    nameof(fileName));
            }

            string fileNameWithExtension = fileName.EndsWith(BytesExtension, StringComparison.OrdinalIgnoreCase)
                ? fileName
                : fileName + BytesExtension;
            string path = Path.Combine(
                Application.streamingAssetsPath,
                GameDataDirectoryName,
                BytesDirectoryName,
                fileNameWithExtension);

            if (!File.Exists(path))
            {
                string message = $"GameData bytes file was not found: {path}";
                Debug.LogError(message);
                throw new FileNotFoundException(message, path);
            }

            return ByteBuf.Wrap(File.ReadAllBytes(path));
        }
    }
}
