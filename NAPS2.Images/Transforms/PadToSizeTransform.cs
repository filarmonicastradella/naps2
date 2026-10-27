// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace NAPS2.Images.Transforms;

/// <summary>
/// Enlarges the page to a target physical size (e.g. A4) by adding white margins. The margins are calculated from each
/// image's own size and resolution, so the same transform can be applied to pages of different sizes. The page is
/// rotated to match the image's orientation (portrait/landscape), and a dimension that's already larger than the page is
/// left unchanged.
/// </summary>
public record PadToSizeTransform : Transform
{
    private const float DEFAULT_DPI = 96;

    public PadToSizeTransform()
    {
    }

    public PadToSizeTransform(double widthInInches, double heightInInches, double horizontalOffset = 0.5,
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
    /// The position of the image on the page, from 0 (left) to 1 (right).
    /// </summary>
    public double HorizontalOffset { get; private set; }

    /// <summary>
    /// The position of the image on the page, from 0 (top) to 1 (bottom).
    /// </summary>
    public double VerticalOffset { get; private set; }

    /// <summary>
    /// Gets the equivalent margins in pixels for an image with the given size and resolution.
    /// </summary>
    public PadTransform GetPadTransform(int width, int height, float xDpi, float yDpi)
    {
        if (xDpi <= 0) xDpi = DEFAULT_DPI;
        if (yDpi <= 0) yDpi = xDpi;
        var (pageWidth, pageHeight) = (WidthInInches, HeightInInches);
        if ((pageWidth > pageHeight) != (width / (double) xDpi > height / (double) yDpi))
        {
            (pageWidth, pageHeight) = (pageHeight, pageWidth);
        }
        int extraWidth = Math.Max((int) Math.Round(pageWidth * xDpi) - width, 0);
        int extraHeight = Math.Max((int) Math.Round(pageHeight * yDpi) - height, 0);
        int left = (int) Math.Round(extraWidth * Math.Clamp(HorizontalOffset, 0, 1));
        int top = (int) Math.Round(extraHeight * Math.Clamp(VerticalOffset, 0, 1));
        return new PadTransform(left, extraWidth - left, top, extraHeight - top);
    }

    public override bool IsNull => WidthInInches <= 0 || HeightInInches <= 0;
}
