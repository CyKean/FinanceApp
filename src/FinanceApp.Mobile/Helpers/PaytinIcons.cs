namespace FinanceApp.Mobile.Helpers;

/// <summary>
/// App-wide line icons, using the Lucide icon set (ISC license, https://lucide.dev.
/// 24x24 stroke icons, stroke-width 2, round caps — converted to single path-data
/// strings so they render identically on every device with no font dependency.
/// Pinned source: lucide-static 0.469.0.
/// Resolution: stored emoji (or direct key) first, then owner-name keywords.
/// </summary>
public static class PaytinIcons
{
    private static readonly Dictionary<string, string> EmojiMap = new(StringComparer.Ordinal)
    {
        ["🍔"] = "sandwich", ["🚌"] = "bus", ["🏠"] = "home", ["⚡"] = "bolt",
        ["🛍"] = "bag", ["🎮"] = "game", ["🏥"] = "health", ["📚"] = "cap",
        ["📄"] = "doc", ["🔄"] = "sync", ["📦"] = "box", ["💼"] = "briefcase",
        ["💻"] = "laptop", ["🏢"] = "building", ["💰"] = "banknote", ["📈"] = "trendup",
        ["💵"] = "banknote", ["💴"] = "banknote", ["💶"] = "banknote", ["💷"] = "banknote",
        ["🪙"] = "banknote", ["💳"] = "card", ["🏦"] = "bank", ["🏧"] = "card",
        ["💸"] = "banknote", ["📉"] = "trenddown", ["🧾"] = "receipt", ["👛"] = "wallet",
        ["🐷"] = "piggy", ["🎯"] = "target", ["✈"] = "plane", ["☕"] = "cup",
        ["🚗"] = "car", ["⛽"] = "fuel", ["🏡"] = "home", ["💡"] = "bulb",
        ["📱"] = "phone", ["🎬"] = "film", ["🛒"] = "cart", ["👕"] = "shirt",
        ["💊"] = "pill", ["🎓"] = "cap", ["🎁"] = "gift", ["❤"] = "heart",
        ["⭐"] = "star", ["🎨"] = "palette", ["⚽"] = "ball", ["🐶"] = "dog",
        ["👶"] = "baby", ["🔧"] = "wrench", ["💧"] = "drop", ["🌙"] = "moon",
        ["🛡"] = "shield", ["🎵"] = "music", ["🏨"] = "building", ["🍕"] = "pizza",
        ["📊"] = "chart",
    };

    // Ordered: first keyword hit wins, so keep specific entries before general ones.
    private static readonly (string[] Words, string Key)[] KeywordMap = new[]
    {
        (new[] { "restaurant", "dining", "coffee", "café", "cafe", "burger", "pizza", "lunch", "dinner", "breakfast", "meal", "food", "eat" }, "food"),
        (new[] { "mortgage", "apartment", "housing", "house", "home", "rent" }, "building"),
        (new[] { "jeep", "grab", "taxi", "commute", "fare", "bus", "transport" }, "bus"),
        (new[] { "vehicle", "parking" }, "car"),
        (new[] { "bike", "cycling" }, "bike"),
        (new[] { "fuel", "gas", "petrol" }, "fuel"),
        (new[] { "car" }, "car"),
        (new[] { "grocer", "grocery", "supermarket" }, "basket"),
        (new[] { "cart" }, "cart"),
        (new[] { "cloth", "shirt", "apparel", "gadget", "mall", "purchase", "retail", "store", "shop" }, "bag"),
        (new[] { "payroll" }, "briefcase"),
        (new[] { "freelance" }, "laptop"),
        (new[] { "laptop", "computer" }, "laptop"),
        (new[] { "gym", "workout", "dumbbell" }, "dumbbell"),
        (new[] { "salary", "wage", "paycheck", "pay", "income", "business", "work", "job" }, "briefcase"),
        (new[] { "allowance", "pocket", "cash", "money" }, "banknote"),
        (new[] { "piggy", "saving" }, "piggy"),
        (new[] { "invest", "stock", "dividend" }, "trendup"),
        (new[] { "clinic", "doctor", "hospital", "pharma", "medical", "health", "fitness" }, "health"),
        (new[] { "tuition", "school", "course", "study", "student", "graduat", "educ" }, "cap"),
        (new[] { "stream", "netflix", "spotify", "subscri" }, "sync"),
        (new[] { "electric", "water bill", "utilit" }, "bolt"),
        (new[] { "receipt", "invoice" }, "receipt"),
        (new[] { "internet", "phone bill", "bill" }, "doc"),
        (new[] { "tv", "television" }, "tv"),
        (new[] { "cinema", "movie", "film" }, "film"),
        (new[] { "game", "gaming" }, "game"),
        (new[] { "music", "concert", "song" }, "music"),
        (new[] { "entertain", "leisure", "fun", "party" }, "ticket"),
        (new[] { "dog", "pet", "puppy", "cat" }, "dog"),
        (new[] { "baby", "kid", "child", "infant" }, "baby"),
        (new[] { "donat", "charity", "love", "family" }, "heart"),
        (new[] { "gift" }, "gift"),
        (new[] { "flight", "hotel", "vacation", "trip", "travel" }, "plane"),
        (new[] { "insur", "secur", "emergency", "fund", "shield" }, "shield"),
        (new[] { "telecom", "load", "mobile", "phone" }, "phone"),
        (new[] { "calendar" }, "calendar"),
        (new[] { "forecast", "predict" }, "forecast"),
        (new[] { "target", "goal" }, "target"),
        (new[] { "bank", "bdo", "bpi", "metrobank", "unionbank", "savings", "checking" }, "bank"),
        (new[] { "gcash", "maya", "ewallet", "e-wallet", "wallet" }, "wallet"),
        (new[] { "credit", "debit", "card" }, "card"),
        (new[] { "chart", "analytic" }, "chart"),
        (new[] { "trash", "waste" }, "trash"),
        (new[] { "sport", "ball", "basketball", "football", "trophy" }, "ball"),
        (new[] { "art", "design", "paint" }, "palette"),
        (new[] { "sleep", "night", "moon" }, "moon"),
        (new[] { "water", "drop" }, "drop"),
        (new[] { "idea", "bulb" }, "bulb"),
        (new[] { "tool", "repair", "wrench", "fix", "maintenance" }, "wrench"),
        (new[] { "star", "favo" }, "star"),
        (new[] { "box", "package", "parcel", "misc", "other" }, "box"),
    };

    public static string Resolve(string? icon, string? name, string? directKey = null)
    {
        if (!string.IsNullOrWhiteSpace(directKey) && Paths.ContainsKey(directKey))
            return directKey;

        if (!string.IsNullOrWhiteSpace(icon))
        {
            var normalized = icon.Trim().Replace("\uFE0F", string.Empty);
            if (EmojiMap.TryGetValue(normalized, out var key))
                return key;
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            var lower = name.ToLowerInvariant();
            foreach (var (words, key) in KeywordMap)
            {
                foreach (var word in words)
                {
                    if (lower.Contains(word))
                        return key;
                }
            }
        }

        return "tag";
    }

    public static readonly Dictionary<string, string> Paths = new(StringComparer.OrdinalIgnoreCase)
    {
        ["food"] = "M3 2v7c0 1.1.9 2 2 2h4a2 2 0 0 0 2-2V2 M7 2v20 M21 15V2a5 5 0 0 0-5 5v6c0 1.1.9 2 2 2h3Zm0 0v7",
        ["home"] = "M15 21v-8a1 1 0 0 0-1-1h-4a1 1 0 0 0-1 1v8 M3 10a2 2 0 0 1 .709-1.528l7-5.999a2 2 0 0 1 2.582 0l7 5.999A2 2 0 0 1 21 10v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z",
        ["building"] = "M6 22V4a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v18Z M6 12H4a2 2 0 0 0-2 2v6a2 2 0 0 0 2 2h2 M18 9h2a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2h-2 M10 6h4 M10 10h4 M10 14h4 M10 18h4",
        ["bank"] = "M 3 22 L 21 22 M 6 18 L 6 11 M 10 18 L 10 11 M 14 18 L 14 11 M 18 18 L 18 11 M 12 2 L 20 7 L 4 7 Z",
        ["bus"] = "M8 6v6 M15 6v6 M2 12h19.6 M18 18h3s.5-1.7.8-2.8c.1-.4.2-.8.2-1.2 0-.4-.1-.8-.2-1.2l-1.4-5C20.1 6.8 19.1 6 18 6H4a2 2 0 0 0-2 2v10h3 M 5 18 A 2 2 0 1 0 9 18 A 2 2 0 1 0 5 18 M9 18h5 M 14 18 A 2 2 0 1 0 18 18 A 2 2 0 1 0 14 18",
        ["car"] = "M19 17h2c.6 0 1-.4 1-1v-3c0-.9-.7-1.7-1.5-1.9C18.7 10.6 16 10 16 10s-1.3-1.4-2.2-2.3c-.5-.4-1.1-.7-1.8-.7H5c-.6 0-1.1.4-1.4.9l-1.4 2.9A3.7 3.7 0 0 0 2 12v4c0 .6.4 1 1 1h2 M 5 17 A 2 2 0 1 0 9 17 A 2 2 0 1 0 5 17 M9 17h6 M 15 17 A 2 2 0 1 0 19 17 A 2 2 0 1 0 15 17",
        ["fuel"] = "M 3 22 L 15 22 M 4 9 L 14 9 M14 22V4a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v18 M14 13h2a2 2 0 0 1 2 2v2a2 2 0 0 0 2 2a2 2 0 0 0 2-2V9.83a2 2 0 0 0-.59-1.42L18 5",
        ["plane"] = "M17.8 19.2 16 11l3.5-3.5C21 6 21.5 4 21 3c-1-.5-3 0-4.5 1.5L13 8 4.8 6.2c-.5-.1-.9.1-1.1.5l-.3.5c-.2.5-.1 1 .3 1.3L9 12l-2 3H4l-1 1 3 2 2 3 1-1v-3l3-2 3.5 5.3c.3.4.8.5 1.3.3l.5-.2c.4-.3.6-.7.5-1.2z",
        ["bag"] = "M6 2 3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4Z M3 6h18 M16 10a4 4 0 0 1-8 0",
        ["cart"] = "M 7 21 A 1 1 0 1 0 9 21 A 1 1 0 1 0 7 21 M 18 21 A 1 1 0 1 0 20 21 A 1 1 0 1 0 18 21 M2.05 2.05h2l2.66 12.42a2 2 0 0 0 2 1.58h9.78a2 2 0 0 0 1.95-1.57l1.65-7.43H5.12",
        ["basket"] = "m15 11-1 9 m19 11-4-7 M2 11h20 m3.5 11 1.6 7.4a2 2 0 0 0 2 1.6h9.8a2 2 0 0 0 2-1.6l1.7-7.4 M4.5 15.5h15 m5 11 4-7 m9 11 1 9",
        ["banknote"] = "M 4 6 H 20 A 2 2 0 0 1 22 8 V 16 A 2 2 0 0 1 20 18 H 4 A 2 2 0 0 1 2 16 V 8 A 2 2 0 0 1 4 6 Z M 10 12 A 2 2 0 1 0 14 12 A 2 2 0 1 0 10 12 M6 12h.01M18 12h.01",
        ["card"] = "M 4 5 H 20 A 2 2 0 0 1 22 7 V 17 A 2 2 0 0 1 20 19 H 4 A 2 2 0 0 1 2 17 V 7 A 2 2 0 0 1 4 5 Z M 2 10 L 22 10",
        ["wallet"] = "M19 7V4a1 1 0 0 0-1-1H5a2 2 0 0 0 0 4h15a1 1 0 0 1 1 1v4h-3a2 2 0 0 0 0 4h3a1 1 0 0 0 1-1v-2a1 1 0 0 0-1-1 M3 5v14a2 2 0 0 0 2 2h15a1 1 0 0 0 1-1v-4",
        ["briefcase"] = "M16 20V4a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16 M 4 6 H 20 A 2 2 0 0 1 22 8 V 18 A 2 2 0 0 1 20 20 H 4 A 2 2 0 0 1 2 18 V 8 A 2 2 0 0 1 4 6 Z",
        ["laptop"] = "M20 16V7a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v9m16 0H4m16 0 1.28 2.55a1 1 0 0 1-.9 1.45H3.62a1 1 0 0 1-.9-1.45L4 16",
        ["health"] = "M4 9a2 2 0 0 0-2 2v2a2 2 0 0 0 2 2h4a1 1 0 0 1 1 1v4a2 2 0 0 0 2 2h2a2 2 0 0 0 2-2v-4a1 1 0 0 1 1-1h4a2 2 0 0 0 2-2v-2a2 2 0 0 0-2-2h-4a1 1 0 0 1-1-1V4a2 2 0 0 0-2-2h-2a2 2 0 0 0-2 2v4a1 1 0 0 1-1 1z",
        ["dumbbell"] = "M14.4 14.4 9.6 9.6 M18.657 21.485a2 2 0 1 1-2.829-2.828l-1.767 1.768a2 2 0 1 1-2.829-2.829l6.364-6.364a2 2 0 1 1 2.829 2.829l-1.768 1.767a2 2 0 1 1 2.828 2.829z m21.5 21.5-1.4-1.4 M3.9 3.9 2.5 2.5 M6.404 12.768a2 2 0 1 1-2.829-2.829l1.768-1.767a2 2 0 1 1-2.828-2.829l2.828-2.828a2 2 0 1 1 2.829 2.828l1.767-1.768a2 2 0 1 1 2.829 2.829z",
        ["pill"] = "m10.5 20.5 10-10a4.95 4.95 0 1 0-7-7l-10 10a4.95 4.95 0 1 0 7 7Z m8.5 8.5 7 7",
        ["book"] = "M4 19.5v-15A2.5 2.5 0 0 1 6.5 2H19a1 1 0 0 1 1 1v18a1 1 0 0 1-1 1H6.5a1 1 0 0 1 0-5H20",
        ["cap"] = "M21.42 10.922a1 1 0 0 0-.019-1.838L12.83 5.18a2 2 0 0 0-1.66 0L2.6 9.08a1 1 0 0 0 0 1.832l8.57 3.908a2 2 0 0 0 1.66 0z M22 10v6 M6 12.5V16a6 3 0 0 0 12 0v-3.5",
        ["doc"] = "M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z M14 2v4a2 2 0 0 0 2 2h4 M10 9H8 M16 13H8 M16 17H8",
        ["receipt"] = "M4 2v20l2-1 2 1 2-1 2 1 2-1 2 1 2-1 2 1V2l-2 1-2-1-2 1-2-1-2 1-2-1-2 1Z M16 8h-6a2 2 0 1 0 0 4h4a2 2 0 1 1 0 4H8 M12 17.5v-11",
        ["sync"] = "M21 12a9 9 0 0 0-9-9 9.75 9.75 0 0 0-6.74 2.74L3 8 M3 3v5h5 M3 12a9 9 0 0 0 9 9 9.75 9.75 0 0 0 6.74-2.74L21 16 M16 16h5v5",
        ["calendar"] = "M8 2v4 M16 2v4 M 5 4 H 19 A 2 2 0 0 1 21 6 V 20 A 2 2 0 0 1 19 22 H 5 A 2 2 0 0 1 3 20 V 6 A 2 2 0 0 1 5 4 Z M3 10h18",
        ["bolt"] = "M4 14a1 1 0 0 1-.78-1.63l9.9-10.2a.5.5 0 0 1 .86.46l-1.92 6.02A1 1 0 0 0 13 10h7a1 1 0 0 1 .78 1.63l-9.9 10.2a.5.5 0 0 1-.86-.46l1.92-6.02A1 1 0 0 0 11 14z",
        ["ticket"] = "M2 9a3 3 0 0 1 0 6v2a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-2a3 3 0 0 1 0-6V7a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2Z M13 5v2 M13 17v2 M13 11v2",
        ["film"] = "M20.2 6 3 11l-.9-2.4c-.3-1.1.3-2.2 1.3-2.5l13.5-4c1.1-.3 2.2.3 2.5 1.3Z m6.2 5.3 3.1 3.9 m12.4 3.4 3.1 4 M3 11h18v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2Z",
        ["game"] = "M 6 11 L 10 11 M 8 9 L 8 13 M 15 12 L 15.01 12 M 18 10 L 18.01 10 M17.32 5H6.68a4 4 0 0 0-3.978 3.59c-.006.052-.01.101-.017.152C2.604 9.416 2 14.456 2 16a3 3 0 0 0 3 3c1 0 1.5-.5 2-1l1.414-1.414A2 2 0 0 1 9.828 16h4.344a2 2 0 0 1 1.414.586L17 18c.5.5 1 1 2 1a3 3 0 0 0 3-3c0-1.545-.604-6.584-.685-7.258-.007-.05-.011-.1-.017-.151A4 4 0 0 0 17.32 5z",
        ["music"] = "M9 18V5l12-2v13 M 3 18 A 3 3 0 1 0 9 18 A 3 3 0 1 0 3 18 M 15 16 A 3 3 0 1 0 21 16 A 3 3 0 1 0 15 16",
        ["cup"] = "M10 2v2 M14 2v2 M16 8a1 1 0 0 1 1 1v8a4 4 0 0 1-4 4H7a4 4 0 0 1-4-4V9a1 1 0 0 1 1-1h14a4 4 0 1 1 0 8h-1 M6 2v2",
        ["pizza"] = "m12 14-1 1 m13.75 18.25-1.25 1.42 M17.775 5.654a15.68 15.68 0 0 0-12.121 12.12 M18.8 9.3a1 1 0 0 0 2.1 7.7 M21.964 20.732a1 1 0 0 1-1.232 1.232l-18-5a1 1 0 0 1-.695-1.232A19.68 19.68 0 0 1 15.732 2.037a1 1 0 0 1 1.232.695z",
        ["sandwich"] = "m2.37 11.223 8.372-6.777a2 2 0 0 1 2.516 0l8.371 6.777 M21 15a1 1 0 0 1 1 1v2a1 1 0 0 1-1 1h-5.25 M3 15a1 1 0 0 0-1 1v2a1 1 0 0 0 1 1h9 m6.67 15 6.13 4.6a2 2 0 0 0 2.8-.4l3.15-4.2 M 3 11 H 21 A 1 1 0 0 1 22 12 V 14 A 1 1 0 0 1 21 15 H 3 A 1 1 0 0 1 2 14 V 12 A 1 1 0 0 1 3 11 Z",
        ["gift"] = "M 4 8 H 20 A 1 1 0 0 1 21 9 V 11 A 1 1 0 0 1 20 12 H 4 A 1 1 0 0 1 3 11 V 9 A 1 1 0 0 1 4 8 Z M12 8v13 M19 12v7a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2v-7 M7.5 8a2.5 2.5 0 0 1 0-5A4.8 8 0 0 1 12 8a4.8 8 0 0 1 4.5-5 2.5 2.5 0 0 1 0 5",
        ["heart"] = "M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z",
        ["star"] = "M11.525 2.295a.53.53 0 0 1 .95 0l2.31 4.679a2.123 2.123 0 0 0 1.595 1.16l5.166.756a.53.53 0 0 1 .294.904l-3.736 3.638a2.123 2.123 0 0 0-.611 1.878l.882 5.14a.53.53 0 0 1-.771.56l-4.618-2.428a2.122 2.122 0 0 0-1.973 0L6.396 21.01a.53.53 0 0 1-.77-.56l.881-5.139a2.122 2.122 0 0 0-.611-1.879L2.16 9.795a.53.53 0 0 1 .294-.906l5.165-.755a2.122 2.122 0 0 0 1.597-1.16z",
        ["shield"] = "M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z",
        ["target"] = "M 2 12 A 10 10 0 1 0 22 12 A 10 10 0 1 0 2 12 M 6 12 A 6 6 0 1 0 18 12 A 6 6 0 1 0 6 12 M 10 12 A 2 2 0 1 0 14 12 A 2 2 0 1 0 10 12",
        ["chart"] = "M3 3v16a2 2 0 0 0 2 2h16 M18 17V9 M13 17V5 M8 17v-3",
        ["trendup"] = "M 22 7 L 13.5 15.5 L 8.5 10.5 L 2 17 M 16 7 L 22 7 L 22 13",
        ["trenddown"] = "M 22 17 L 13.5 8.5 L 8.5 13.5 L 2 7 M 16 17 L 22 17 L 22 11",
        ["phone"] = "M 7 2 H 17 A 2 2 0 0 1 19 4 V 20 A 2 2 0 0 1 17 22 H 7 A 2 2 0 0 1 5 20 V 4 A 2 2 0 0 1 7 2 Z M12 18h.01",
        ["bulb"] = "M15 14c.2-1 .7-1.7 1.5-2.5 1-.9 1.5-2.2 1.5-3.5A6 6 0 0 0 6 8c0 1 .2 2.2 1.5 3.5.7.7 1.3 1.5 1.5 2.5 M9 18h6 M10 22h4",
        ["shirt"] = "M20.38 3.46 16 2a4 4 0 0 1-8 0L3.62 3.46a2 2 0 0 0-1.34 2.23l.58 3.47a1 1 0 0 0 .99.84H6v10c0 1.1.9 2 2 2h8a2 2 0 0 0 2-2V10h2.15a1 1 0 0 0 .99-.84l.58-3.47a2 2 0 0 0-1.34-2.23z",
        ["trash"] = "M3 6h18 M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6 M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2 M 10 11 L 10 17 M 14 11 L 14 17",
        ["storefront"] = "m2 7 4.41-4.41A2 2 0 0 1 7.83 2h8.34a2 2 0 0 1 1.42.59L22 7 M4 12v8a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-8 M15 22v-4a2 2 0 0 0-2-2h-2a2 2 0 0 0-2 2v4 M2 7h20 M22 7v3a2 2 0 0 1-2 2a2.7 2.7 0 0 1-1.59-.63.7.7 0 0 0-.82 0A2.7 2.7 0 0 1 16 12a2.7 2.7 0 0 1-1.59-.63.7.7 0 0 0-.82 0A2.7 2.7 0 0 1 12 12a2.7 2.7 0 0 1-1.59-.63.7.7 0 0 0-.82 0A2.7 2.7 0 0 1 8 12a2.7 2.7 0 0 1-1.59-.63.7.7 0 0 0-.82 0A2.7 2.7 0 0 1 4 12a2 2 0 0 1-2-2V7",
        ["forecast"] = "M9.937 15.5A2 2 0 0 0 8.5 14.063l-6.135-1.582a.5.5 0 0 1 0-.962L8.5 9.936A2 2 0 0 0 9.937 8.5l1.582-6.135a.5.5 0 0 1 .963 0L14.063 8.5A2 2 0 0 0 15.5 9.937l6.135 1.581a.5.5 0 0 1 0 .964L15.5 14.063a2 2 0 0 0-1.437 1.437l-1.582 6.135a.5.5 0 0 1-.963 0z M20 3v4 M22 5h-4 M4 17v2 M5 18H3",
        ["bot"] = "M12 8V4H8 M 6 8 H 18 A 2 2 0 0 1 20 10 V 18 A 2 2 0 0 1 18 20 H 6 A 2 2 0 0 1 4 18 V 10 A 2 2 0 0 1 6 8 Z M2 14h2 M20 14h2 M15 13v2 M9 13v2",
        ["tag"] = "M12.586 2.586A2 2 0 0 0 11.172 2H4a2 2 0 0 0-2 2v7.172a2 2 0 0 0 .586 1.414l8.704 8.704a2.426 2.426 0 0 0 3.42 0l6.58-6.58a2.426 2.426 0 0 0 0-3.42z M 7 7.5 A 0.5 0.5 0 1 0 8 7.5 A 0.5 0.5 0 1 0 7 7.5",
        ["box"] = "M11 21.73a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73z M12 22V12 m3.3 7 7.703 4.734a2 2 0 0 0 1.994 0L20.7 7 m7.5 4.27 9 5.15",
        ["paw"] = "M 9 4 A 2 2 0 1 0 13 4 A 2 2 0 1 0 9 4 M 16 8 A 2 2 0 1 0 20 8 A 2 2 0 1 0 16 8 M 18 16 A 2 2 0 1 0 22 16 A 2 2 0 1 0 18 16 M9 10a5 5 0 0 1 5 5v3.5a3.5 3.5 0 0 1-6.84 1.045Q6.52 17.48 4.46 16.84A3.5 3.5 0 0 1 5.5 10Z",
        ["palette"] = "M 13 6.5 A 0.5 0.5 0 1 0 14 6.5 A 0.5 0.5 0 1 0 13 6.5 M 17 10.5 A 0.5 0.5 0 1 0 18 10.5 A 0.5 0.5 0 1 0 17 10.5 M 8 7.5 A 0.5 0.5 0 1 0 9 7.5 A 0.5 0.5 0 1 0 8 7.5 M 6 12.5 A 0.5 0.5 0 1 0 7 12.5 A 0.5 0.5 0 1 0 6 12.5 M12 2C6.5 2 2 6.5 2 12s4.5 10 10 10c.926 0 1.648-.746 1.648-1.688 0-.437-.18-.835-.437-1.125-.29-.289-.438-.652-.438-1.125a1.64 1.64 0 0 1 1.668-1.668h1.996c3.051 0 5.555-2.503 5.555-5.554C21.965 6.012 17.461 2 12 2z",
        ["moon"] = "M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z",
        ["drop"] = "M12 22a7 7 0 0 0 7-7c0-2-1-3.9-3-5.5s-3.5-4-4-6.5c-.5 2.5-2 4.9-4 6.5C6 11.1 5 13 5 15a7 7 0 0 0 7 7z",
        ["wrench"] = "M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z",
        ["ball"] = "M6 9H4.5a2.5 2.5 0 0 1 0-5H6 M18 9h1.5a2.5 2.5 0 0 0 0-5H18 M4 22h16 M10 14.66V17c0 .55-.47.98-.97 1.21C7.85 18.75 7 20.24 7 22 M14 14.66V17c0 .55.47.98.97 1.21C16.15 18.75 17 20.24 17 22 M18 2H6v7a6 6 0 0 0 12 0V2Z",
        ["dog"] = "M11.25 16.25h1.5L12 17z M16 14v.5 M4.42 11.247A13.152 13.152 0 0 0 4 14.556C4 18.728 7.582 21 12 21s8-2.272 8-6.444a11.702 11.702 0 0 0-.493-3.309 M8 14v.5 M8.5 8.5c-.384 1.05-1.083 2.028-2.344 2.5-1.931.722-3.576-.297-3.656-1-.113-.994 1.177-6.53 4-7 1.923-.321 3.651.845 3.651 2.235A7.497 7.497 0 0 1 14 5.277c0-1.39 1.844-2.598 3.767-2.277 2.823.47 4.113 6.006 4 7-.08.703-1.725 1.722-3.656 1-1.261-.472-1.855-1.45-2.239-2.5",
        ["baby"] = "M9 12h.01 M15 12h.01 M10 16c.5.3 1.2.5 2 .5s1.5-.2 2-.5 M19 6.3a9 9 0 0 1 1.8 3.9 2 2 0 0 1 0 3.6 9 9 0 0 1-17.6 0 2 2 0 0 1 0-3.6A9 9 0 0 1 12 3c2 0 3.5 1.1 3.5 2.5s-.9 2.5-2 2.5c-.8 0-1.5-.4-1.5-1",
        ["dots"] = "M 8 12 A 1 1 0 1 0 10 12 A 1 1 0 1 0 8 12 M 8 5 A 1 1 0 1 0 10 5 A 1 1 0 1 0 8 5 M 8 19 A 1 1 0 1 0 10 19 A 1 1 0 1 0 8 19 M 14 12 A 1 1 0 1 0 16 12 A 1 1 0 1 0 14 12 M 14 5 A 1 1 0 1 0 16 5 A 1 1 0 1 0 14 5 M 14 19 A 1 1 0 1 0 16 19 A 1 1 0 1 0 14 19",
        ["bell"] = "M10.268 21a2 2 0 0 0 3.464 0 M3.262 15.326A1 1 0 0 0 4 17h16a1 1 0 0 0 .74-1.673C19.41 13.956 18 12.499 18 8A6 6 0 0 0 6 8c0 4.499-1.411 5.956-2.738 7.326",
        ["grid"] = "M 4 3 H 9 A 1 1 0 0 1 10 4 V 9 A 1 1 0 0 1 9 10 H 4 A 1 1 0 0 1 3 9 V 4 A 1 1 0 0 1 4 3 Z M 15 3 H 20 A 1 1 0 0 1 21 4 V 9 A 1 1 0 0 1 20 10 H 15 A 1 1 0 0 1 14 9 V 4 A 1 1 0 0 1 15 3 Z M 15 14 H 20 A 1 1 0 0 1 21 15 V 20 A 1 1 0 0 1 20 21 H 15 A 1 1 0 0 1 14 20 V 15 A 1 1 0 0 1 15 14 Z M 4 14 H 9 A 1 1 0 0 1 10 15 V 20 A 1 1 0 0 1 9 21 H 4 A 1 1 0 0 1 3 20 V 15 A 1 1 0 0 1 4 14 Z",
        ["swap"] = "m16 3 4 4-4 4 M20 7H4 m8 21-4-4 4-4 M4 17h16",
        ["pie"] = "M21 12c.552 0 1.005-.449.95-.998a10 10 0 0 0-8.953-8.951c-.55-.055-.998.398-.998.95v8a1 1 0 0 0 1 1z M21.21 15.89A10 10 0 1 1 8 2.83",
        ["sliders"] = "M 21 4 L 14 4 M 10 4 L 3 4 M 21 12 L 12 12 M 8 12 L 3 12 M 21 20 L 16 20 M 12 20 L 3 20 M 14 2 L 14 6 M 8 10 L 8 14 M 16 18 L 16 22",
        ["piggy"] = "M19 5c-1.5 0-2.8 1.4-3 2-3.5-1.5-11-.3-11 5 0 1.8 0 3 2 4.5V20h4v-2h3v2h4v-4c1-.5 1.7-1 2-2h2v-4h-2c0-1-.5-1.5-1-2V5z M2 9v1c0 1.1.9 2 2 2h1 M16 11h.01",
        ["bike"] = "M 15 17.5 A 3.5 3.5 0 1 0 22 17.5 A 3.5 3.5 0 1 0 15 17.5 M 2 17.5 A 3.5 3.5 0 1 0 9 17.5 A 3.5 3.5 0 1 0 2 17.5 M 14 5 A 1 1 0 1 0 16 5 A 1 1 0 1 0 14 5 M12 17.5V14l-3-3 4-3 2 3h2",
        ["tv"] = "M 4 7 H 20 A 2 2 0 0 1 22 9 V 20 A 2 2 0 0 1 20 22 H 4 A 2 2 0 0 1 2 20 V 9 A 2 2 0 0 1 4 7 Z M 17 2 L 12 7 L 7 2",
        ["pencil"] = "M21.174 6.812a1 1 0 0 0-3.986-3.987L3.842 16.174a2 2 0 0 0-.5.83l-1.321 4.352a.5.5 0 0 0 .623.622l4.353-1.32a2 2 0 0 0 .83-.497z m15 5 4 4",
        ["settings"] = "M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z M 9 12 A 3 3 0 1 0 15 12 A 3 3 0 1 0 9 12",
        ["mail"] = "M 4 4 H 20 A 2 2 0 0 1 22 6 V 18 A 2 2 0 0 1 20 20 H 4 A 2 2 0 0 1 2 18 V 6 A 2 2 0 0 1 4 4 Z m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7",
        ["plus"] = "M5 12h14 M12 5v14",
        ["check"] = "M20 6 9 17l-5-5",
        ["alert"] = "m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3 M12 9v4 M12 17h.01",
        ["alertCircle"] = "M 2 12 A 10 10 0 1 0 22 12 A 10 10 0 1 0 2 12 M 12 8 L 12 12 M 12 16 L 12.01 16",
        ["sparkle"] = "M9.937 15.5A2 2 0 0 0 8.5 14.063l-6.135-1.582a.5.5 0 0 1 0-.962L8.5 9.936A2 2 0 0 0 9.937 8.5l1.582-6.135a.5.5 0 0 1 .963 0L14.063 8.5A2 2 0 0 0 15.5 9.937l6.135 1.581a.5.5 0 0 1 0 .964L15.5 14.063a2 2 0 0 0-1.437 1.437l-1.582 6.135a.5.5 0 0 1-.963 0z",
        ["lock"] = "M7 11V7a5 5 0 0 1 10 0v4 M 5 11 H 19 A 2 2 0 0 1 21 13 V 20 A 2 2 0 0 1 19 22 H 5 A 2 2 0 0 1 3 20 V 13 A 2 2 0 0 1 5 11 Z",
        ["arrowright"] = "M5 12h14 m7 -5 7 7-7 7",
    };

    /// <summary>
    /// Freshly parsed geometry for an icon key (falls back to "tag").
    /// NOTE: intentionally not cached — sharing one Geometry instance across
    /// multiple Path elements breaks rendering on Android, so every call
    /// returns its own instance.
    /// </summary>
    public static Microsoft.Maui.Controls.Shapes.Geometry GetGeometry(string? key)
    {
        var data = Paths["tag"];
        if (!string.IsNullOrWhiteSpace(key) && Paths.TryGetValue(key, out var found))
            data = found;

        try
        {
            return (Microsoft.Maui.Controls.Shapes.Geometry)new Microsoft.Maui.Controls.Shapes.PathGeometryConverter().ConvertFromString(data);
        }
        catch
        {
            return (Microsoft.Maui.Controls.Shapes.Geometry)new Microsoft.Maui.Controls.Shapes.PathGeometryConverter().ConvertFromString(Paths["tag"]);
        }
    }
}
