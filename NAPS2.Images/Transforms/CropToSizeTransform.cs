// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace NAPS2.Images.Transforms;

/// <summary>
/// Crops the page to a target physical size (e.g. A4). The crop is calculated from each image's own size and
/// resolution, so the same transform can be applied to pages of different sizes. The page is rotated to match the
/// image's orientation (portrait/landscape), and a dimension that's already smaller than the page is left unchanged.
/// </summary>
public record CropToSizeTransform : Transform
{
    private const float DEFAULT_DPI = 96;

    public CropToSizeTransform()
    {
    }

    public CropToSizeTransform(double widthInInches, double heightInInches, double horizontalOffset = 0.5,
        double verticalOffset = 0.5)
    {
        WidthInInches = widthInInches;
        HeightInInches = heightInInches;
        HorizontalOffset = horizontalOffset;
        VerticalOffset = verticalOffset;
    }

    public double WidthInInches { get; private set; }
    public double HeightInInches { get; private set; }

    /// <summary>
    /// The position of the cropped area in the image, from 0 (left) to 1 (right).
    /// </summary>
    public double HorizontalOffset { get; private set; }

    /// <summary>
    /// The position of the cropped area in the image, from 0 (top) to 1 (bottom).
    /// </summary>
    public double VerticalOffset { get; private set; }

    /// <summary>
    /// Gets the equivalent crop in pixels for an image with the given size and resolution.
    /// </summary>
    public CropTransform GetCropTransform(int width, int height, float xDpi, float yDpi)
    {
        if (xDpi <= 0) xDpi = DEFAULT_DPI;
        if (yDpi <= 0) yDpi = xDpi;
        var (pageWidth, pageHeight) = (WidthInInches, HeightInInches);
        if ((pageWidth > pageHeight) != (width / (double) xDpi > height / (double) yDpi))
        {
            (pageWidth, pageHeight) = (pageHeight, pageWidth);
        }
        int extraWidth = Math.Max(width - (int) Math.Round(pageWidth * xDpi), 0);
        int extraHeight = Math.Max(height - (int) Math.Round(pageHeight * yDpi), 0);
        int left = (int) Math.Round(extraWidth * Math.Clamp(HorizontalOffset, 0, 1));
        int top = (int) Math.Round(extraHeight * Math.Clamp(VerticalOffset, 0, 1));
        return new CropTransform(left, extraWidth - left, top, extraHeight - top);
    }

    public override bool IsNull => WidthInInches <= 0 || HeightInInches <= 0;
}
