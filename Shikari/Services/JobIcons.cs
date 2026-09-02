namespace Shikari.Services;

/// <summary>Icon ids for the job symbols.</summary>
public static class JobIcons
{
    // Job symbols sit at 62100 + the ClassJob row id.
    private const uint JobBase = 62100;

    // Framed versions, used where a token needs a bit more contrast.
    private const uint FramedBase = 62000;

    public static uint For(uint jobId) => jobId == 0 ? 0 : JobBase + jobId;

    public static uint Framed(uint jobId) => jobId == 0 ? 0 : FramedBase + jobId;
}
