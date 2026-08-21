using System.Collections.Generic;

namespace InfamousModManager.Models;

public enum GameModMode
{
    LooseXpps,
    PackedPsarc
}

public sealed record GameEdition(
    string TitleId,
    string DisplayName,
    GameModMode ModMode,
    IReadOnlyList<string> RequiredRelativePaths);

public static class GameEditions
{
    public static readonly GameEdition Infamous1Psn = new(
        "NPUA80480",
        "PSN (NPUA80480)",
        GameModMode.LooseXpps,
        ["USRDIR"]);

    public static readonly GameEdition Infamous1Bcus = new(
        "BCUS98119",
        "Blu-ray / inFAMOUS Collection (BCUS98119)",
        GameModMode.PackedPsarc,
        [
            "USRDIR/cache/psarc/install1/infamous1.psarc_s",
            "USRDIR/cache/psarc/install2/infamous2.psarc_s"
        ]);

    public static readonly GameEdition Infamous2Psn = new(
        "NPUA80638",
        "PSN (NPUA80638)",
        GameModMode.LooseXpps,
        ["USRDIR"]);

    public static readonly GameEdition FestivalOfBloodPsn = new(
        "NPEA00322",
        "PSN (NPEA00322)",
        GameModMode.LooseXpps,
        ["USRDIR"]);
}
