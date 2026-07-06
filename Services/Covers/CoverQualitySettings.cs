using Avalonia.Media.Imaging;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Services.Covers;

internal sealed record CoverQualityProfile(
    bool ShowLibraryCovers,
    bool ShowDetailCover,
    int GridDecodeWidth,
    int ListDecodeWidth,
    int DetailDecodeWidth,
    int BackgroundMaxCovers,
    int PageSize,
    BitmapInterpolationMode Interpolation);

internal static class CoverQualitySettings
{
    public static CoverQualityProfile Get(CoverQualityMode mode) => mode switch
    {
        CoverQualityMode.High => High,
        CoverQualityMode.None => None,
        _ => Low
    };

    public static CoverQualityProfile Low { get; } = new(
        ShowLibraryCovers: true,
        ShowDetailCover: true,
        GridDecodeWidth: 96,
        ListDecodeWidth: 64,
        DetailDecodeWidth: 160,
        BackgroundMaxCovers: 8,
        PageSize: 16,
        Interpolation: BitmapInterpolationMode.LowQuality);

    public static CoverQualityProfile High { get; } = new(
        ShowLibraryCovers: true,
        ShowDetailCover: true,
        GridDecodeWidth: 180,
        ListDecodeWidth: 96,
        DetailDecodeWidth: 280,
        BackgroundMaxCovers: 16,
        PageSize: 20,
        Interpolation: BitmapInterpolationMode.MediumQuality);

    public static CoverQualityProfile None { get; } = new(
        ShowLibraryCovers: false,
        ShowDetailCover: false,
        GridDecodeWidth: 0,
        ListDecodeWidth: 0,
        DetailDecodeWidth: 0,
        BackgroundMaxCovers: 0,
        PageSize: 24,
        Interpolation: BitmapInterpolationMode.LowQuality);
}
