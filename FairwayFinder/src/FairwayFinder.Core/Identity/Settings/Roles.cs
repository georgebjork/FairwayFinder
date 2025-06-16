using System.Reflection;
namespace FairwayFinder.Core.Identity.Settings;

public static class Roles
{
    public const string Admin = "Admin";
    public const string User = "User";

    private static readonly Lazy<List<string>> _all_roles = new(() =>
    {
        var roles = new List<string>();
        var fields = typeof(Roles).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

        foreach (var field in fields)
        {
            if (field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
            {
                if (field.GetValue(null) is string value)
                {
                    roles.Add(value);
                }
            }
        }

        return roles;
    });

    public static List<string> GetAllRoles()
    {
        return _all_roles.Value;
    }
}