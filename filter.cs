using System.Text.RegularExpressions;

public static class UsernameFilter
{
    private static List<Regex> _patterns = new();

    public static void Load(string path, bool asciiOnly = true)
    {
        var list = new List<Regex>();

        foreach (var line in File.ReadLines(path))
        {
            string p = line.Trim();
            if (p.Length == 0 || p.StartsWith('#')) continue;

            // usernames are limited to [A-Za-z0-9._-], so non-ASCII patterns can never match
            if (asciiOnly && p.Any(c => c > 127)) continue;

            try
            {
                list.Add(new Regex(p,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(100)));
            }
            catch (ArgumentException)
            {
                Console.WriteLine($"Skipping invalid blocklist pattern: {p}");
            }
        }

        _patterns = list;
    }

    public static bool IsBlocked(string username)
    {
        // TODO: maybe this is too strict and may flag words that don't actually have bad words but just contain the characters (ex: nazim276 would get flagged for "nazi")

        // also test with separators removed so "b.a.d.w.o.r.d" is still caught
        string stripped = Regex.Replace(username, @"[._-]", "");

        foreach (var r in _patterns)
        {
            try
            {
                if (r.IsMatch(username) || r.IsMatch(stripped)) return true;
            }
            catch (RegexMatchTimeoutException) { }
        }
        return false;
    }
}