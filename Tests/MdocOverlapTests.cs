using System;
using System.IO;
using System.Linq;
using Warp.Tools;
using Xunit;

namespace Tests;

/// <summary>
/// ts_import must refuse to import several tilt series backed by the same tilt images, which
/// happens when acquisition software writes more than one MDOC per series.
/// </summary>
public class MdocOverlapTests : IDisposable
{
    private readonly string Folder;

    public MdocOverlapTests()
    {
        Folder = Path.Combine(Path.GetTempPath(), "warp_mdoc_overlap_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Folder);
    }

    public void Dispose()
    {
        try { Directory.Delete(Folder, true); } catch { }
    }

    private string WriteMdoc(string fileName, params string[] movieNames)
    {
        string Path0 = Path.Combine(Folder, fileName);
        using (StreamWriter Writer = new StreamWriter(Path0))
        {
            Writer.WriteLine("PixelSpacing = 1.0");
            Writer.WriteLine("Voltage = 300");

            for (int i = 0; i < movieNames.Length; i++)
            {
                Writer.WriteLine();
                Writer.WriteLine($"[ZValue = {i}]");
                Writer.WriteLine($"TiltAngle = {i * 3}.00");
                // Windows-style path on purpose: that is what the microscope writes
                Writer.WriteLine($"SubFramePath = D:\\data\\{movieNames[i]}");
                Writer.WriteLine($"DateTime = 01-Jan-24 00:0{i}:00");
            }
        }

        return Path0;
    }

    [Fact]
    public void ExtractsMovieNameFromWindowsAndUnixPaths()
    {
        Assert.Equal("a.eer", Mdoc.ExtractMovieName(@"D:\data\sub\a.eer"));
        Assert.Equal("a.eer", Mdoc.ExtractMovieName("/mnt/data/sub/a.eer"));
        Assert.Equal("a.eer", Mdoc.ExtractMovieName("a.eer"));
    }

    [Fact]
    public void NoOverlapReportedForDistinctTiltSeries()
    {
        string A = WriteMdoc("ts_001.mrc.mdoc", "ts_001_0.eer", "ts_001_1.eer");
        string B = WriteMdoc("ts_002.mrc.mdoc", "ts_002_0.eer", "ts_002_1.eer");

        Assert.Empty(Mdoc.FindOverlappingMdocs(new[] { A, B }));
    }

    [Fact]
    public void DetectsSortedAndUnsortedMdocPairReferencingTheSameTilts()
    {
        // Exactly the PACE case: an "_unsorted" twin listing the identical tilt movies.
        string A = WriteMdoc("ts_001.mrc.mdoc", "ts_001_0.eer", "ts_001_1.eer");
        string B = WriteMdoc("ts_001_unsorted.mrc.mdoc", "ts_001_0.eer", "ts_001_1.eer");

        var Overlaps = Mdoc.FindOverlappingMdocs(new[] { A, B });

        var Overlap = Assert.Single(Overlaps);
        Assert.Equal("ts_001.mrc.mdoc, ts_001_unsorted.mrc.mdoc", Overlap.MdocNames);
        Assert.Equal(new[] { "ts_001_0.eer", "ts_001_1.eer" }, Overlap.SharedMovieNames);
    }

    [Fact]
    public void GroupsOneEntryPerMdocPairRatherThanPerSharedTilt()
    {
        // 2 pairs sharing 3 tilts each must read as 2 conflicts, not 6.
        string A1 = WriteMdoc("ts_001.mrc.mdoc", "a0.eer", "a1.eer", "a2.eer");
        string A2 = WriteMdoc("ts_001_unsorted.mrc.mdoc", "a0.eer", "a1.eer", "a2.eer");
        string B1 = WriteMdoc("ts_002.mrc.mdoc", "b0.eer", "b1.eer", "b2.eer");
        string B2 = WriteMdoc("ts_002_unsorted.mrc.mdoc", "b0.eer", "b1.eer", "b2.eer");

        var Overlaps = Mdoc.FindOverlappingMdocs(new[] { A1, A2, B1, B2 });

        Assert.Equal(2, Overlaps.Count);
        Assert.All(Overlaps, overlap => Assert.Equal(3, overlap.SharedMovieNames.Length));
    }

    [Fact]
    public void DetectsPartialOverlapBetweenOtherwiseDistinctSeries()
    {
        // A single stray shared tilt is still a conflict.
        string A = WriteMdoc("ts_001.mrc.mdoc", "shared.eer", "a1.eer");
        string B = WriteMdoc("ts_002.mrc.mdoc", "shared.eer", "b1.eer");

        var Overlap = Assert.Single(Mdoc.FindOverlappingMdocs(new[] { A, B }));

        Assert.Equal(new[] { "shared.eer" }, Overlap.SharedMovieNames);
    }

    [Fact]
    public void UnreadableMdocIsSkippedRatherThanThrowing()
    {
        string A = WriteMdoc("ts_001.mrc.mdoc", "a0.eer");
        string Missing = Path.Combine(Folder, "does_not_exist.mdoc");

        Assert.Empty(Mdoc.FindOverlappingMdocs(new[] { A, Missing }));
    }

    [Fact]
    public void RepeatedTiltWithinASingleMdocIsNotAnOverlap()
    {
        // Only cross-MDOC sharing matters here; a duplicate ZValue is caught by the importer.
        string A = WriteMdoc("ts_001.mrc.mdoc", "a0.eer", "a0.eer");

        Assert.Empty(Mdoc.FindOverlappingMdocs(new[] { A }));
    }
}
