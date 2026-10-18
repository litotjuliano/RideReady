using System.Security.Cryptography;

namespace RideReady.Services
{
    /// <summary>
    /// The secret that signs the sign-in links. It is kept next to the app's other saved keys, so links survive a restart,
    /// and deleting the file (or the keys volume) makes every link already sent stop working.
    /// </summary>
    public sealed class LinkSigningKey
    {
        private const string FileName = "link-signing.key";
        private const int Size = 32;

        public LinkSigningKey(byte[] value)
        {
            if (value is null || value.Length < 16)
            {
                throw new ArgumentException("The signing key must be at least 16 bytes.", nameof(value));
            }

            Value = value;
        }

        public byte[] Value { get; }

        public static LinkSigningKey NewRandom() => new(RandomNumberGenerator.GetBytes(Size));

        /// <summary>Reads the key from the given folder, creating it on first use. With no folder the key lives only as long as the process.</summary>
        public static LinkSigningKey LoadOrCreate(string? directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                return NewRandom();
            }

            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, FileName);

            if (File.Exists(path))
            {
                try
                {
                    var existing = Convert.FromBase64String(File.ReadAllText(path).Trim());
                    if (existing.Length >= 16)
                    {
                        return new LinkSigningKey(existing);
                    }
                }
                catch (FormatException)
                {
                    // Damaged file: fall through and make a new key (links already sent stop working).
                }
            }

            var created = NewRandom();
            File.WriteAllText(path, Convert.ToBase64String(created.Value));
            return created;
        }
    }
}
