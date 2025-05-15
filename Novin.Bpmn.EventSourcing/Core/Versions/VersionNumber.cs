public class VersionNumber : IComparable<VersionNumber>
{
    public int Major { get; private set; }
    public int Minor { get; private set; }
    public int Patch { get; private set; }

    public VersionNumber(int major = 1, int minor = 0, int patch = 0)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
    }

    public static VersionNumber Parse(string version)
    {
        var parts = version.Split('.');
        if (parts.Length != 3)
            throw new FormatException("Version must be in format x.y.z");

        return new VersionNumber(
            int.Parse(parts[0]),
            int.Parse(parts[1]),
            int.Parse(parts[2]));
    }

    public VersionNumber Next()
    {
        var next = new VersionNumber(Major, Minor, Patch + 1);
        if (next.Patch > 9)
        {
            next.Patch = 0;
            next.Minor += 1;
        }

        if (next.Minor > 9)
        {
            next.Minor = 0;
            next.Major += 1;
        }

        return next;
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    public int CompareTo(VersionNumber? other)
    {
        if (other == null) return 1;
        if (Major != other.Major) return Major.CompareTo(other.Major);
        if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
        return Patch.CompareTo(other.Patch);
    }

    public override bool Equals(object? obj) =>
        obj is VersionNumber v &&
        v.Major == Major && v.Minor == Minor && v.Patch == Patch;

    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch);
    public static bool TryParse(string version, out VersionNumber? result)
    {
        try
        {
            result = Parse(version);
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }
}