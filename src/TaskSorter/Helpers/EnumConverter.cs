namespace TaskSorter.Helpers;

public static class EnumConverter {
    public static TEnum? TryParseEnum<TEnum>(string input) where TEnum : struct, Enum {
        if (Enum.TryParse(typeof(TEnum), input.Trim(), true, out var result) && result is TEnum enumValue) {
            return enumValue;
        }

        return null;
    }

    public static string EnumToString<TEnum>() where TEnum : Enum {
        return string.Join(", ", Enum.GetNames(typeof(TEnum)));
    }
}