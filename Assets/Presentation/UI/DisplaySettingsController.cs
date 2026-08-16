using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

internal readonly struct DisplayResolutionOption : IEquatable<DisplayResolutionOption>
{
    internal int Width { get; }
    internal int Height { get; }
    internal uint RefreshNumerator { get; }
    internal uint RefreshDenominator { get; }

    internal double RefreshRate => RefreshDenominator == 0
        ? 0d
        : (double)RefreshNumerator / RefreshDenominator;

    internal UnityEngine.RefreshRate UnityRefreshRate => new UnityEngine.RefreshRate
    {
        numerator = RefreshNumerator,
        denominator = RefreshDenominator == 0 ? 1u : RefreshDenominator
    };

    internal DisplayResolutionOption(
        int width,
        int height,
        uint refreshNumerator,
        uint refreshDenominator)
    {
        Width = width;
        Height = height;
        RefreshNumerator = refreshNumerator;
        RefreshDenominator = refreshDenominator == 0 ? 1u : refreshDenominator;
    }

    internal static DisplayResolutionOption FromResolution(Resolution resolution)
    {
        UnityEngine.RefreshRate refreshRate = resolution.refreshRateRatio;
        return new DisplayResolutionOption(
            resolution.width,
            resolution.height,
            refreshRate.numerator,
            refreshRate.denominator);
    }

    public bool Equals(DisplayResolutionOption other)
    {
        return Width == other.Width
            && Height == other.Height
            && RefreshNumerator == other.RefreshNumerator
            && RefreshDenominator == other.RefreshDenominator;
    }

    public override bool Equals(object obj)
    {
        return obj is DisplayResolutionOption other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = Width;
            hashCode = (hashCode * 397) ^ Height;
            hashCode = (hashCode * 397) ^ (int)RefreshNumerator;
            hashCode = (hashCode * 397) ^ (int)RefreshDenominator;
            return hashCode;
        }
    }
}

public sealed class DisplaySettingsController : MonoBehaviour
{
    private const string HasSettingsKey = "DemonBlackjack.Display.HasSettings";
    private const string FullscreenKey = "DemonBlackjack.Display.Fullscreen";
    private const string WidthKey = "DemonBlackjack.Display.Width";
    private const string HeightKey = "DemonBlackjack.Display.Height";
    private const string RefreshNumeratorKey = "DemonBlackjack.Display.RefreshNumerator";
    private const string RefreshDenominatorKey = "DemonBlackjack.Display.RefreshDenominator";

    [SerializeField] private TMP_Dropdown screenModeDropdown;
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    private readonly List<DisplayResolutionOption> resolutionOptions = new();
    private bool listenersRegistered;
    private bool refreshingControls;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedSettingsOnStartup()
    {
        if (!TryLoadSettings(out bool fullscreen, out DisplayResolutionOption savedOption))
            return;

        List<DisplayResolutionOption> available = GetSupportedResolutionOptions();
        DisplayResolutionOption current = GetCurrentResolutionOption();
        DisplayResolutionOption resolved = ResolveResolutionOption(available, savedOption, current);
        FullScreenMode mode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        Screen.SetResolution(
            resolved.Width,
            resolved.Height,
            mode,
            resolved.UnityRefreshRate);
        SaveSettings(fullscreen, resolved);
    }

    private void OnEnable()
    {
        if (screenModeDropdown == null || resolutionDropdown == null)
        {
            Debug.LogError(
                "DisplaySettingsController requires screen-mode and resolution dropdowns.",
                this);
            return;
        }

        PopulateControls();
        AddListeners();
    }

    private void OnDisable()
    {
        RemoveListeners();
    }

    private void PopulateControls()
    {
        refreshingControls = true;

        screenModeDropdown.ClearOptions();
        screenModeDropdown.AddOptions(new List<string> { "전체 화면", "창 모드" });

        resolutionOptions.Clear();
        resolutionOptions.AddRange(GetSupportedResolutionOptions());

        var labels = new List<string>(resolutionOptions.Count);
        for (int i = 0; i < resolutionOptions.Count; i++)
            labels.Add(FormatResolutionOption(resolutionOptions[i]));

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(labels);

        bool hasSavedSettings = TryLoadSettings(
            out bool savedFullscreen,
            out DisplayResolutionOption savedOption);
        bool fullscreen = hasSavedSettings
            ? savedFullscreen
            : Screen.fullScreenMode != FullScreenMode.Windowed;

        DisplayResolutionOption current = GetCurrentResolutionOption();
        DisplayResolutionOption desired = hasSavedSettings ? savedOption : current;
        DisplayResolutionOption resolved = ResolveResolutionOption(
            resolutionOptions,
            desired,
            current);

        screenModeDropdown.SetValueWithoutNotify(fullscreen ? 0 : 1);
        resolutionDropdown.SetValueWithoutNotify(Mathf.Max(0, IndexOf(resolutionOptions, resolved)));
        screenModeDropdown.RefreshShownValue();
        resolutionDropdown.RefreshShownValue();

        refreshingControls = false;
    }

    private void AddListeners()
    {
        if (listenersRegistered)
            return;

        screenModeDropdown.onValueChanged.AddListener(OnScreenModeChanged);
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        listenersRegistered = true;
    }

    private void RemoveListeners()
    {
        if (!listenersRegistered)
            return;

        screenModeDropdown.onValueChanged.RemoveListener(OnScreenModeChanged);
        resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);
        listenersRegistered = false;
    }

    private void OnScreenModeChanged(int _)
    {
        ApplySelectedSettings();
    }

    private void OnResolutionChanged(int _)
    {
        ApplySelectedSettings();
    }

    private void ApplySelectedSettings()
    {
        if (refreshingControls || resolutionOptions.Count == 0)
            return;

        int index = Mathf.Clamp(resolutionDropdown.value, 0, resolutionOptions.Count - 1);
        DisplayResolutionOption option = resolutionOptions[index];
        bool fullscreen = screenModeDropdown.value == 0;
        FullScreenMode mode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        Screen.SetResolution(
            option.Width,
            option.Height,
            mode,
            option.UnityRefreshRate);
        SaveSettings(fullscreen, option);
    }

    internal static List<DisplayResolutionOption> NormalizeResolutionOptions(
        IEnumerable<DisplayResolutionOption> source)
    {
        var unique = new HashSet<DisplayResolutionOption>();
        if (source != null)
        {
            foreach (DisplayResolutionOption option in source)
            {
                if (option.Width > 0 && option.Height > 0)
                    unique.Add(option);
            }
        }

        var result = new List<DisplayResolutionOption>(unique);
        result.Sort(CompareResolutionOptions);
        return result;
    }

    internal static DisplayResolutionOption ResolveResolutionOption(
        IReadOnlyList<DisplayResolutionOption> available,
        DisplayResolutionOption desired,
        DisplayResolutionOption current)
    {
        if (available == null || available.Count == 0)
            return current;

        int exactDesiredIndex = IndexOf(available, desired);
        if (exactDesiredIndex >= 0)
            return available[exactDesiredIndex];

        for (int i = 0; i < available.Count; i++)
        {
            if (available[i].Width == desired.Width && available[i].Height == desired.Height)
                return available[i];
        }

        int exactCurrentIndex = IndexOf(available, current);
        if (exactCurrentIndex >= 0)
            return available[exactCurrentIndex];

        for (int i = 0; i < available.Count; i++)
        {
            if (available[i].Width == current.Width && available[i].Height == current.Height)
                return available[i];
        }

        return available[0];
    }

    internal static string FormatResolutionOption(DisplayResolutionOption option)
    {
        double refreshRate = option.RefreshRate;
        string refreshText = Math.Abs(refreshRate - Math.Round(refreshRate)) < 0.005d
            ? Math.Round(refreshRate).ToString("0", CultureInfo.InvariantCulture)
            : refreshRate.ToString("0.##", CultureInfo.InvariantCulture);

        return $"{option.Width} × {option.Height} @ {refreshText} Hz";
    }

    private static List<DisplayResolutionOption> GetSupportedResolutionOptions()
    {
        Resolution[] supported = Screen.resolutions;
        var rawOptions = new List<DisplayResolutionOption>(supported?.Length ?? 1);

        if (supported != null)
        {
            for (int i = 0; i < supported.Length; i++)
                rawOptions.Add(DisplayResolutionOption.FromResolution(supported[i]));
        }

        if (rawOptions.Count == 0)
            rawOptions.Add(GetCurrentResolutionOption());

        return NormalizeResolutionOptions(rawOptions);
    }

    private static DisplayResolutionOption GetCurrentResolutionOption()
    {
        UnityEngine.RefreshRate refreshRate = Screen.currentResolution.refreshRateRatio;
        int width = Screen.width > 0 ? Screen.width : Screen.currentResolution.width;
        int height = Screen.height > 0 ? Screen.height : Screen.currentResolution.height;

        return new DisplayResolutionOption(
            Mathf.Max(1, width),
            Mathf.Max(1, height),
            refreshRate.numerator,
            refreshRate.denominator);
    }

    private static int CompareResolutionOptions(
        DisplayResolutionOption left,
        DisplayResolutionOption right)
    {
        int result = right.Width.CompareTo(left.Width);
        if (result != 0)
            return result;

        result = right.Height.CompareTo(left.Height);
        if (result != 0)
            return result;

        result = right.RefreshRate.CompareTo(left.RefreshRate);
        if (result != 0)
            return result;

        result = right.RefreshNumerator.CompareTo(left.RefreshNumerator);
        return result != 0
            ? result
            : right.RefreshDenominator.CompareTo(left.RefreshDenominator);
    }

    private static int IndexOf(
        IReadOnlyList<DisplayResolutionOption> options,
        DisplayResolutionOption target)
    {
        if (options == null)
            return -1;

        for (int i = 0; i < options.Count; i++)
        {
            if (options[i].Equals(target))
                return i;
        }

        return -1;
    }

    private static bool TryLoadSettings(
        out bool fullscreen,
        out DisplayResolutionOption option)
    {
        fullscreen = PlayerPrefs.GetInt(FullscreenKey, 1) != 0;
        option = default;

        if (PlayerPrefs.GetInt(HasSettingsKey, 0) == 0)
            return false;

        int width = PlayerPrefs.GetInt(WidthKey, 0);
        int height = PlayerPrefs.GetInt(HeightKey, 0);
        bool numeratorValid = uint.TryParse(
            PlayerPrefs.GetString(RefreshNumeratorKey, string.Empty),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out uint numerator);
        bool denominatorValid = uint.TryParse(
            PlayerPrefs.GetString(RefreshDenominatorKey, string.Empty),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out uint denominator);

        if (width <= 0 || height <= 0 || !numeratorValid || !denominatorValid || denominator == 0)
            return false;

        option = new DisplayResolutionOption(width, height, numerator, denominator);
        return true;
    }

    private static void SaveSettings(bool fullscreen, DisplayResolutionOption option)
    {
        PlayerPrefs.SetInt(HasSettingsKey, 1);
        PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
        PlayerPrefs.SetInt(WidthKey, option.Width);
        PlayerPrefs.SetInt(HeightKey, option.Height);
        PlayerPrefs.SetString(
            RefreshNumeratorKey,
            option.RefreshNumerator.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.SetString(
            RefreshDenominatorKey,
            option.RefreshDenominator.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }
}
