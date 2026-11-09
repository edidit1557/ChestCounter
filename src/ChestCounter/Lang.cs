using System.Globalization;

namespace ChestCounter;

/// <summary>
/// Windows 설정에 따라 한국어 또는 영어로 표시합니다.
/// data.json에 저장된 상자와 보물 이름은 호환성을 위해 유지하고 표시할 때 번역합니다 (<see cref="Name"/>).
/// </summary>
public static class L
{
    /// <summary>인터페이스가 영어이면 true입니다.</summary>
    /// <remarks>
    /// Windows 언어 대신 영어 또는 한국어를 사용하려면 CHESTCOUNTER_LANG을 "en" 또는 "ko"로 설정합니다.
    /// </remarks>
    private static string Language { get; } = Environment.GetEnvironmentVariable("CHESTCOUNTER_LANG") is { Length: > 0 } forced
        ? (forced[..Math.Min(2, forced.Length)].ToLowerInvariant() == "en" ? "en" : "ko")
        : (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "en" : "ko");
    public static bool En => Language == "en";
    public static bool Ko => Language == "ko";

    public static CultureInfo Culture { get; } = CultureInfo.GetCultureInfo(Ko ? "ko-KR" : "en-US");

    /// <summary>HTML 페이지의 lang 속성에 사용할 언어 코드입니다.</summary>
    public static string Code => Ko ? "ko" : "en";

    public static string T(string fallback, string en, string? ko = null) => En ? en : ko ?? en;

    /// <summary>Entero sin separador de miles ("1409"): las cifras son cantidades, no importes.</summary>
    public static string N(int n) => n.ToString("0", Culture);

    /// <summary>"3 cofres" / "1 chest" (plural según la cifra, en el idioma actual).</summary>
    public static string Count(int n, string esOne, string esMany, string enOne, string enMany)
    {
        if (Ko)
        {
            var noun = esOne switch
            {
                "cofre" => "개 상자", "partida" => "경기", "replay" => "개 리플레이", "tesoro" => "개 보물",
                "cofre abierto" => "개 상자 개봉", "cofre del equipo" => "개 팀 상자",
                "replay analizado" => "개 리플레이 분석", _ => $"개 {esOne}"
            };
            return $"{N(n)}{noun}";
        }
        return $"{N(n)} {(n == 1 ? enOne : enMany)}";
    }

    /// <summary>"12.º" / "12th".</summary>
    public static string Ordinal(int n)
    {
        if (Ko) return $"{n}위";
        var suffix = (n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return $"{n}{suffix}";
    }

    /// <summary>Short date and time for a match.</summary>
    public static string DateTimeShort(DateTime d) => d.ToString(Ko ? "M월 d일 HH:mm" : "MMM d HH:mm", Culture);

    // data.json에 저장된 내부 이름(영어)을 화면 표시 이름으로 변환합니다.
    private static readonly Dictionary<string, (string Es, string En, string Ko)> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Chest"] = ("Cofre", "Chest", "상자"),
        ["Rare chest"] = ("Cofre raro", "Rare chest", "희귀 상자"),
        ["Buried chest"] = ("Cofre enterrado", "Buried chest", "매장된 상자"),
        ["Extraction / relic chest"] = ("Cofre de extracción / reliquias", "Extraction / relic chest", "추출 / 유물 상자"),
        ["Spirit poster chest"] = ("Cofre de los carteles de espíritu", "Spirit poster chest", "영혼 포스터 상자"),
        ["Other chest"] = ("Otro cofre", "Other chest", "기타 상자"),
        ["Loot cannon"] = ("Cañón de botín", "Loot cannon", "전리품 대포"),
        ["Ammo box"] = ("Caja de munición", "Ammo box", "탄약 상자"),
        ["Food box"] = ("Caja de comida", "Food box", "식량 상자"),
        ["Other container"] = ("Otro contenedor", "Other container", "기타 보관함"),
        ["Treasure"] = ("Tesoro", "Treasure", "보물"),
        ["Treasure (unknown type)"] = ("Tesoro (tipo desconocido)", "Treasure (unknown type)", "보물 (알 수 없는 유형)"),
        ["Treasure gizmo"] = ("Artilugio de tesoro", "Treasure gizmo", "보물 장치"),
        ["Golden firework"] = ("Fuego artificial dorado", "Golden firework", "황금 불꽃놀이"),
        ["Portable extractor"] = ("Extractor portátil", "Portable extractor", "휴대용 추출기"),
        ["Spirit locator"] = ("Localizador de espíritu", "Spirit locator", "영혼 탐지기"),
        ["Lucky locator"] = ("Localizador afortunado", "Lucky locator", "행운 탐지기"),
        ["Extraction accelerator"] = ("Acelerador de extracción", "Extraction accelerator", "추출 가속기"),
        ["unknown"] = ("desconocido", "unknown", "알 수 없음"),
    };

    /// <summary>
    /// 2.0.9 이전 버전의 내부 이름(프랑스어)을 현재 내부 이름으로 변환합니다.
    /// 이전 기록을 불러올 때만 사용합니다.
    /// </summary>
    private static readonly Dictionary<string, string> Legacy = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Coffre"] = "Chest",
        ["Coffre rare"] = "Rare chest",
        ["Coffre enterr\u00e9"] = "Buried chest",
        ["Coffre d'extraction / reliques"] = "Extraction / relic chest",
        ["Coffre des affiches d'esprit"] = "Spirit poster chest",
        ["Autre coffre"] = "Other chest",
        ["Canon \u00e0 butin"] = "Loot cannon",
        ["Caisse de munitions"] = "Ammo box",
        ["Caisse de nourriture"] = "Food box",
        ["Autre conteneur"] = "Other container",
        ["Tr\u00e9sor"] = "Treasure",
        ["Tr\u00e9sor (type inconnu)"] = "Treasure (unknown type)",
        ["Gizmo de tr\u00e9sor"] = "Treasure gizmo",
        ["Feu d'artifice dor\u00e9"] = "Golden firework",
        ["Extracteur portable"] = "Portable extractor",
        ["Localisateur d'esprit"] = "Spirit locator",
        ["Localisateur chanceux"] = "Lucky locator",
        ["Acc\u00e9l\u00e9rateur d'extraction"] = "Extraction accelerator",
        ["inconnu"] = "unknown",
    };

    /// <summary>이전 버전에서 저장한 이름을 현재 내부 이름으로 변환합니다.</summary>
    public static string Upgrade(string stored) => Legacy.TryGetValue(stored, out var current) ? current : stored;

    /// <summary>data.json에 저장된 상자 또는 아이템의 화면 표시 이름입니다.</summary>
    public static string Name(string? stored)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return stored ?? "";
        }
        return Names.TryGetValue(Upgrade(stored), out var pair) ? (Ko ? pair.Ko : En ? pair.En : pair.Es) : stored;
    }
}
