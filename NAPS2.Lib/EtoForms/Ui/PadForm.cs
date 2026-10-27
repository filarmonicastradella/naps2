using Eto.Drawing;
using Eto.Forms;
using NAPS2.EtoForms.Layout;
using NAPS2.EtoForms.Widgets;
using NAPS2.Scan;

namespace NAPS2.EtoForms.Ui;

/// <summary>
/// The opposite of cropping: enlarges the page by adding white margins, either to reach a target paper size (e.g. A4)
/// or by custom amounts on each side.
/// </summary>
public class PadForm : UnaryImageFormBase
{
    private const float DEFAULT_DPI = 96;

    private static LastSettings? _lastSettings;

    private readonly ColorScheme _colorScheme;
    private readonly EnumDropDownWidget<ScanPageSize> _pageSize = new(scale: false);
    private readonly UndoableNumericMaskedTextBox<decimal> _top = new();
    private readonly UndoableNumericMaskedTextBox<decimal> _bottom = new();
    private readonly UndoableNumericMaskedTextBox<decimal> _left = new();
    private readonly UndoableNumericMaskedTextBox<decimal> _right = new();
    private readonly EnumDropDownWidget<LocalizedPageSizeUnit> _unit = new(scale: false);
    private readonly Label _resultSize = new();

    private float _dpiX, _dpiY;

    // Margins in inches
    private double _marginT, _marginB, _marginL, _marginR;

    // Position of the original image on the enlarged page (0 = left/top, 0.5 = centered, 1 = right/bottom)
    private double _hOffset = 0.5;
    private double _vOffset = 0.5;

    private bool _updatingFields;

    public PadForm(Naps2Config config, UiImageList imageList, ThumbnailController thumbnailController,
        ColorScheme colorScheme) :
        base(config, imageList, thumbnailController)
    {
        IconName = "transform_pad";
        Title = UiStrings.ExpandPage;
        _colorScheme = colorScheme;

        _pageSize.Items = [ScanPageSize.Custom, ..EnumDropDownWidget<ScanPageSize>.DefaultItems
            .Where(x => x.PageDimensions() != null)];
        _pageSize.SelectedItem = ScanPageSize.Custom;
        _pageSize.SelectedItemChanged += (_, _) => PageSizeChanged();
        _top.TextChanged += (_, _) => MarginChanged(_top, ref _marginT);
        _bottom.TextChanged += (_, _) => MarginChanged(_bottom, ref _marginB);
        _left.TextChanged += (_, _) => MarginChanged(_left, ref _marginL);
        _right.TextChanged += (_, _) => MarginChanged(_right, ref _marginR);
        _unit.SelectedItemChanged += (_, _) => UpdateFields();
    }

    private double ImageWidthInInches => RealImageWidth / (double) _dpiX;

    private double ImageHeightInInches => RealImageHeight / (double) _dpiY;

    protected override List<Transform> Transforms =>
    [
        new PadTransform(
            (int) Math.Round(_marginL * _dpiX),
            (int) Math.Round(_marginR * _dpiX),
            (int) Math.Round(_marginT * _dpiY),
            (int) Math.Round(_marginB * _dpiY),
            RealImageWidth,
            RealImageHeight)
    ];

    protected override void OnFullSizeImageRendered(IMemoryImage realImage)
    {
        _dpiX = realImage.HorizontalResolution > 0 ? realImage.HorizontalResolution : DEFAULT_DPI;
        _dpiY = realImage.VerticalResolution > 0 ? realImage.VerticalResolution : _dpiX;
        _unit.SelectedItem = (LocalizedPageSizeUnit) PageSize.SystemDefault.Unit;
        if (_lastSettings != null)
        {
            (_hOffset, _vOffset) = (_lastSettings.HOffset, _lastSettings.VOffset);
            _unit.SelectedItem = _lastSettings.Unit;
            if (_lastSettings.PageSize != ScanPageSize.Custom)
            {
                // Recalculate the margins so that this image (which may have a different size) fills the same paper
                _pageSize.SelectedItem = _lastSettings.PageSize;
                PageSizeChanged();
                return;
            }
            (_marginT, _marginB, _marginL, _marginR) = _lastSettings.Margins;
        }
        UpdateFields();
    }

    protected override LayoutElement CreateControls()
    {
        return L.Column(
            L.Row(
                C.Label(UiStrings.PageSizeLabel).AlignCenter(),
                L.Row(
                    _pageSize.AsControl().AlignCenter(),
                    C.Filler()
                ).Scale()
            ).Aligned(),
            L.Row(
                C.Label(UiStrings.PositionLabel).AlignCenter(),
                L.Row(
                    C.IconButton("shape_align_left", UiStrings.AlignLeft, () => SetHOffset(0)),
                    C.IconButton("shape_align_center", UiStrings.AlignCenter, () => SetHOffset(0.5)),
                    C.IconButton("shape_align_right", UiStrings.AlignRight, () => SetHOffset(1)),
                    C.Spacer().Width(14),
                    C.IconButton("shape_align_top", UiStrings.AlignTop, () => SetVOffset(0)),
                    C.IconButton("shape_align_middle", UiStrings.AlignMiddle, () => SetVOffset(0.5)),
                    C.IconButton("shape_align_bottom", UiStrings.AlignBottom, () => SetVOffset(1)),
                    C.Filler()
                ).Scale()
            ).Aligned(),
            L.Row(
                C.Label(UiStrings.MarginsLabel).AlignCenter(),
                L.Row(
                    C.Label(UiStrings.MarginTopLabel).AlignCenter(),
                    _top.Width(50).AlignCenter(),
                    C.Label(UiStrings.MarginBottomLabel).AlignCenter(),
                    _bottom.Width(50).AlignCenter(),
                    C.Label(UiStrings.MarginLeftLabel).AlignCenter(),
                    _left.Width(50).AlignCenter(),
                    C.Label(UiStrings.MarginRightLabel).AlignCenter(),
                    _right.Width(50).AlignCenter(),
                    _unit.AsControl().AlignCenter(),
                    C.Filler()
                ).Scale()
            ).Aligned(),
            L.Row(
                C.Label(UiStrings.DimensionsLabel).AlignCenter(),
                L.Row(
                    _resultSize.AlignCenter(),
                    C.Filler()
                ).Scale()
            ).Aligned(),
            ApplyToSelectedControl
        );
    }

    protected override ListMutation<UiImage> CreateMutation()
    {
        var dimens = _pageSize.SelectedItem.PageDimensions();
        if (dimens == null)
        {
            return base.CreateMutation();
        }
        // With a paper size, the margins are calculated for each image so that pages of different sizes all end up
        // the same size
        var pageSize = dimens.ToPageSize();
        return new ImageListMutation.AddTransforms(
        [
            new PadToSizeTransform((double) pageSize.WidthInInches, (double) pageSize.HeightInInches, _hOffset,
                _vOffset)
        ]);
    }

    protected override void Apply()
    {
        base.Apply();
        _lastSettings = new LastSettings(_pageSize.SelectedItem, _unit.SelectedItem, _hOffset, _vOffset,
            (_marginT, _marginB, _marginL, _marginR));
    }

    protected override void Revert()
    {
        _marginT = _marginB = _marginL = _marginR = 0;
        _hOffset = _vOffset = 0.5;
        _updatingFields = true;
        _pageSize.SelectedItem = ScanPageSize.Custom;
        _updatingFields = false;
        UpdateFields();
    }

    // Choosing a paper size sets the margins so the image fills that paper (in its current position)
    private void PageSizeChanged()
    {
        if (_updatingFields) return;
        var dimens = _pageSize.SelectedItem.PageDimensions();
        if (dimens == null) return;
        var pageSize = dimens.ToPageSize();
        var (pageWidth, pageHeight) = ((double) pageSize.WidthInInches, (double) pageSize.HeightInInches);
        // Use the paper's orientation that matches the image
        if ((pageWidth > pageHeight) != (ImageWidthInInches > ImageHeightInInches))
        {
            (pageWidth, pageHeight) = (pageHeight, pageWidth);
        }
        var extraWidth = Math.Max(pageWidth - ImageWidthInInches, 0);
        var extraHeight = Math.Max(pageHeight - ImageHeightInInches, 0);
        _marginL = extraWidth * _hOffset;
        _marginR = extraWidth - _marginL;
        _marginT = extraHeight * _vOffset;
        _marginB = extraHeight - _marginT;
        UpdateFields();
    }

    // Changing the position keeps the total margin and redistributes it between the two sides
    private void SetHOffset(double value)
    {
        _hOffset = value;
        var total = _marginL + _marginR;
        _marginL = total * _hOffset;
        _marginR = total - _marginL;
        UpdateFields();
    }

    private void SetVOffset(double value)
    {
        _vOffset = value;
        var total = _marginT + _marginB;
        _marginT = total * _vOffset;
        _marginB = total - _marginT;
        UpdateFields();
    }

    private void MarginChanged(NumericMaskedTextBox<decimal> textBox, ref double margin)
    {
        // Empty or invalid text has a value of 0
        if (_updatingFields || textBox.Value < 0) return;
        margin = (double) new PageSize(textBox.Value, 0, (PageSizeUnit) _unit.SelectedItem).WidthInInches;
        // Custom margins no longer match a paper size
        _updatingFields = true;
        _pageSize.SelectedItem = ScanPageSize.Custom;
        _updatingFields = false;
        UpdateFields(skip: textBox);
    }

    private void UpdateFields(TextControl? skip = null)
    {
        var unit = (PageSizeUnit) _unit.SelectedItem;
        int decimals = unit == PageSizeUnit.Millimetre ? 1 : 2;
        // Round as a double so there are no trailing zeros (e.g. 8.5 rather than 8.50)
        decimal ToUnit(double inches) =>
            (decimal) Math.Round((double) new PageSize((decimal) inches, 0, PageSizeUnit.Inch).ConvertTo(unit).Width,
                decimals);

        _updatingFields = true;
        try
        {
            if (skip != _top) _top.Value = ToUnit(_marginT);
            if (skip != _bottom) _bottom.Value = ToUnit(_marginB);
            if (skip != _left) _left.Value = ToUnit(_marginL);
            if (skip != _right) _right.Value = ToUnit(_marginR);
            var width = ToUnit(ImageWidthInInches + _marginL + _marginR);
            var height = ToUnit(ImageHeightInInches + _marginT + _marginB);
            _resultSize.Text = $"{width} X {height} {_unit.SelectedItem.Description()}";
        }
        finally
        {
            _updatingFields = false;
        }
        if (Loaded)
        {
            UpdatePreviewBox();
        }
    }

    protected override void PaintOverlay(object? sender, PaintEventArgs e)
    {
        base.PaintOverlay(sender, e);
        if (_overlayW < 1 || _overlayH < 1) return;

        // Outline the page edge (the added margins are white and may not stand out from the background) and the
        // original image within it
        var pen = new Pen(_colorScheme.CropColor);
        e.Graphics.DrawRectangle(pen, _overlayL, _overlayT, _overlayW - 1, _overlayH - 1);
        var pageWidth = ImageWidthInInches + _marginL + _marginR;
        var pageHeight = ImageHeightInInches + _marginT + _marginB;
        var dashed = new Pen(_colorScheme.CropColor) { DashStyle = DashStyles.Dash };
        e.Graphics.DrawRectangle(dashed,
            _overlayL + (float) (_marginL / pageWidth) * _overlayW,
            _overlayT + (float) (_marginT / pageHeight) * _overlayH,
            (float) (ImageWidthInInches / pageWidth) * _overlayW - 1,
            (float) (ImageHeightInInches / pageHeight) * _overlayH - 1);
    }

    private record LastSettings(
        ScanPageSize PageSize,
        LocalizedPageSizeUnit Unit,
        double HOffset,
        double VOffset,
        (double T, double B, double L, double R) Margins);
}
