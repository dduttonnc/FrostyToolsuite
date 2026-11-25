namespace FrostySdk
{
    // Raise a game's number in any release that changes what Frosty writes for that game; its older cache or ModData is then rebuilt once.
    public static class DataVersions
    {
        public static uint CacheRevision
        {
            get
            {
                switch ((ProfileVersion)ProfilesLibrary.DataVersion)
                {
                    case ProfileVersion.DragonAgeInquisition: return 0;
                    case ProfileVersion.MassEffectAndromeda: return 1;
                    case ProfileVersion.DragonAgeTheVeilguard: return 0;
                    default: return 0;
                }
            }
        }

        public static int ModDataVersion
        {
            get
            {
                switch ((ProfileVersion)ProfilesLibrary.DataVersion)
                {
                    case ProfileVersion.DragonAgeInquisition: return 2;
                    case ProfileVersion.MassEffectAndromeda: return 2;
                    case ProfileVersion.DragonAgeTheVeilguard: return 1;
                    default: return 1;
                }
            }
        }
    }
}
