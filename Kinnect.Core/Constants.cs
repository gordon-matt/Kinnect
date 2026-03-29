namespace Kinnect;

public static class Constants
{
    private static bool useKeyCloakHasBeenSet = false;

    public static bool UseKeyCloak { get; private set; }

    public static void SetUseKeyCloak(bool value)
    {
        if (useKeyCloakHasBeenSet)
        {
            throw new InvalidOperationException("UseKeyCloak value has already been set and cannot be changed.");
        }

        UseKeyCloak = value;
        useKeyCloakHasBeenSet = true;
    }

    public const long MaxUploadBytes = 524_288_000;

    public static class Chat
    {
        public const string AnnouncementsRoomName = "Announcements";
    }

    public static class Roles
    {
        public const string Administrator = "Administrator";
        public const string Editor = "Editor";
        public const string User = "User";

        /// <summary>Comma-separated value suitable for use in <c>[Authorize(Roles = ...)]</c>.</summary>
        public const string AdministratorOrEditor = Administrator + "," + Editor;
    }

    public static class FileStorage
    {
        public const string Photos = "photos";
        public const string Videos = "videos";
        public const string Documents = "documents";

        /// <summary>Person tree JSON backups live under &lt;FileStorage:BasePath&gt;/backup.</summary>
        public const string PersonTreeBackups = "backup";
    }
}