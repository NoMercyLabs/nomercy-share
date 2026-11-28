using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Tests;

public sealed class WebCaptureViewModelTests
{
    private static WebCaptureViewModel Model(FakeWebPageCapture? capture = null, string? clipboard = null) =>
        new(capture ?? new FakeWebPageCapture(), new WebCaptureSettings(), clipboard);

    [Fact]
    public void AnHttpsAddressOnTheClipboardIsPrefilled()
    {
        Assert.Equal("https://example.test/page", Model(clipboard: " https://example.test/page ").Address);
    }

    [Theory]
    [InlineData("http://example.test/page")]
    [InlineData("just some text")]
    [InlineData("javascript:alert(1)")]
    [InlineData(null)]
    public void AnythingElseOnTheClipboardLeavesTheAddressEmpty(string? clipboard)
    {
        Assert.Equal(string.Empty, Model(clipboard: clipboard).Address);
    }

    [Fact]
    public void SixWidthsInOrderWithDesktopPicked()
    {
        WebCaptureViewModel model = Model();

        Assert.Equal(
            [("Phone", 390), ("Tablet", 834), ("Desktop", 1280), ("FullHd", 1920), ("QuadHd", 2560), ("UltraHd", 3840)],
            model.Devices.Select(device => (device.Device.ToString(), device.Width)));
        Assert.Equal(("Desktop", 1280), (model.SelectedDevice.Device.ToString(), model.SelectedDevice.Width));
    }

    [Fact]
    public async Task EveryChoicePassesItsDeviceToTheCapture()
    {
        FakeWebPageCapture capture = new();
        WebCaptureViewModel model = Model(capture);
        model.Address = "https://example.test/";

        foreach (WebDeviceChoice choice in model.Devices)
        {
            model.SelectedDevice = choice;
            await model.CaptureAsync();
        }

        Assert.Equal(6, capture.Requests.Count);
        Assert.Equal(model.Devices.Select(device => device.Device), capture.Requests.Select(request => request.Device));
    }

    [Fact]
    public void TheSelectorShowsOnlyForOneElement()
    {
        WebCaptureViewModel model = Model();
        Assert.Equal(WebCaptureKind.FullPage, model.SelectedMode.Kind);
        Assert.False(model.IsElementMode);

        model.SelectedMode = model.Modes.Single(mode => mode.Kind == WebCaptureKind.Element);

        Assert.True(model.IsElementMode);
    }

    [Fact]
    public async Task ABadAddressIsAFieldErrorAndNothingIsCaptured()
    {
        FakeWebPageCapture capture = new();
        WebCaptureViewModel model = Model(capture);
        model.Address = "file:///C:/made-up/page.html";

        await model.CaptureAsync();

        Assert.Equal("Enter a full https:// address.", model.AddressError);
        Assert.Null(model.SelectorError);
        Assert.Empty(capture.Requests);
        Assert.Null(model.Result);
    }

    [Fact]
    public async Task TheSelectorIsRequiredOnlyForOneElement()
    {
        FakeWebPageCapture capture = new();
        WebCaptureViewModel model = Model(capture);
        model.Address = "https://example.test/";

        await model.CaptureAsync();
        Assert.Null(model.SelectorError);
        Assert.Single(capture.Requests);

        model.SelectedMode = model.Modes.Single(mode => mode.Kind == WebCaptureKind.Element);
        await model.CaptureAsync();
        Assert.Equal("Enter a CSS selector.", model.SelectorError);
        Assert.Single(capture.Requests);
    }

    [Fact]
    public async Task TheRequestCarriesTheChosenModeSelectorAndDevice()
    {
        FakeWebPageCapture capture = new();
        WebCaptureViewModel model = Model(capture);
        model.Address = "https://example.test/";
        model.SelectedMode = model.Modes.Single(mode => mode.Kind == WebCaptureKind.Element);
        model.Selector = "  #card ";
        model.SelectedDevice = model.Devices.Single(device => device.Device == WebCaptureDevice.Phone);

        await model.CaptureAsync();

        WebCaptureRequest request = Assert.Single(capture.Requests);
        Assert.Equal(new Uri("https://example.test/"), request.Page);
        Assert.Equal(WebCaptureKind.Element, request.Kind);
        Assert.Equal("#card", request.ElementSelector);
        Assert.Equal(WebCaptureDevice.Phone, request.Device);
    }

    [Fact]
    public async Task ASuccessKeepsTheResultAndClosesTheWindow()
    {
        WebCaptureViewModel model = Model();
        model.Address = "https://example.test/";
        int closed = 0;
        model.CloseRequested += (_, _) => closed++;

        await model.CaptureAsync();

        Assert.Equal("/tmp/page.png", model.Result?.FilePath);
        Assert.Equal(1, closed);
        Assert.False(model.IsBusy);
    }

    [Theory]
    [InlineData(WebCaptureField.Address, "The page did not load in time.")]
    [InlineData(WebCaptureField.Selector, "No element matches that selector.")]
    public async Task AFailureOnAFieldShowsUnderThatFieldAndKeepsTheWindowOpen(WebCaptureField field, string text)
    {
        WebCaptureViewModel model = Model(new FakeWebPageCapture { Throws = new WebCaptureException(field, text) });
        model.Address = "https://example.test/";
        model.SelectedMode = model.Modes.Single(mode => mode.Kind == WebCaptureKind.Element);
        model.Selector = "#missing";
        int closed = 0;
        model.CloseRequested += (_, _) => closed++;

        await model.CaptureAsync();

        Assert.Equal(field == WebCaptureField.Address ? text : null, model.AddressError);
        Assert.Equal(field == WebCaptureField.Selector ? text : null, model.SelectorError);
        Assert.Null(model.FailureText);
        Assert.Equal(0, closed);
        Assert.Null(model.Result);
        Assert.False(model.IsBusy);
    }

    [Fact]
    public async Task AFailureOnNoFieldShowsAsOneLine()
    {
        WebCaptureViewModel model = Model(new FakeWebPageCapture { Throws = new WebCaptureException(WebCaptureField.None, "No Edge or Chrome was found.") });
        model.Address = "https://example.test/";

        await model.CaptureAsync();

        Assert.Equal("No Edge or Chrome was found.", model.FailureText);
        Assert.Null(model.AddressError);
    }

    [Fact]
    public async Task AnEarlierErrorClearsOnTheNextTry()
    {
        FakeWebPageCapture capture = new() { Throws = new WebCaptureException(WebCaptureField.Address, "The page did not load in time.") };
        WebCaptureViewModel model = Model(capture);
        model.Address = "https://example.test/";
        await model.CaptureAsync();
        Assert.NotNull(model.AddressError);

        capture.Throws = null;
        await model.CaptureAsync();

        Assert.Null(model.AddressError);
        Assert.NotNull(model.Result);
    }

    [Fact]
    public void CancelClosesWithoutAResult()
    {
        WebCaptureViewModel model = Model();
        int closed = 0;
        model.CloseRequested += (_, _) => closed++;

        model.Cancel();

        Assert.Equal(1, closed);
        Assert.Null(model.Result);
    }

    [Fact]
    public void TheModeAndDeviceLabelsComeFromTheStrings()
    {
        WebCaptureViewModel model = Model();

        Assert.Equal(["Full page", "Visible area", "One element"], model.Modes.Select(mode => mode.Label));
        Assert.Equal(["Phone", "Tablet", "Desktop", "1080p", "1440p", "4K"], model.Devices.Select(device => device.Label));
        Assert.Equal(
            ["Phone (390 px)", "Tablet (834 px)", "Desktop (1280 px)", "1080p (1920 px)", "1440p (2560 px)", "4K (3840 px)"],
            model.Devices.Select(device => device.Description));
    }
}
