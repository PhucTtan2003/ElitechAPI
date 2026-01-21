using System.Data;
using System.Globalization;

using System.Text.RegularExpressions;

using Elitech.Models.AlertRule;

namespace Elitech.Services;

public class ElitechAlertEngine
{

    private static readonly Regex NumRx = new(@"-?\\d+(\\.\\d+)?", RegexOptions.Compiled);
    public static double? ParseNumber(object? x)

    {

        if (x == null) return null;

        var s = x.ToString();

        if (string.IsNullOrWhiteSpace(s)) return null;

        var m = NumRx.Match(s);

        if (!m.Success) return null;

        if (double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))

            return v;

        // fallback nếu API trả dấu phẩy

        if (double.TryParse(m.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v))

            return v;

        return null;

    }


    public static (bool isBad, string reasons) Evaluate(

        ElitechAlertRuleViewModel rule,

        double?[] temps,  // 4

        double?[] hums)   // 2

    {

        var reasons = new List<string>();


        // TMP1..TMP4

        for (int i = 0; i < 4; i++)

        {

            var v = i < temps.Length ? temps[i] : null;

            if (!v.HasValue) continue;

            var rg = (rule.TempRanges != null &&i < rule.TempRanges.Length) ? rule.TempRanges[i] : null;

        if (rg?.Min != null &&v.Value < rg.Min.Value) reasons.Add($"TMP{i + 1}_LOW");

        if (rg?.Max != null &&v.Value > rg.Max.Value) reasons.Add($"TMP{i + 1}_HIGH");

    }

        // HUM1..HUM2

        for (int i = 0; i< 2; i++)

        {
            var v = i < hums.Length ? hums[i] : null;

            if (!v.HasValue) continue;


            var rg = (rule.HumRanges != null && i<rule.HumRanges.Length) ? rule.HumRanges[i] : null;

            if (rg?.Min != null && v.Value<rg.Min.Value) reasons.Add($"HUM{i + 1}_LOW");

            if (rg?.Max != null && v.Value > rg.Max.Value) reasons.Add($"HUM{i + 1}_HIGH");

        }

        return (reasons.Count > 0, string.Join(",", reasons));

    }

}
