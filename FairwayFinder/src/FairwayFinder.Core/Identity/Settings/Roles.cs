using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public static class Roles
{
    public const string Admin = "Admin";
    public const string User = "User";

    public static List<string> GetAllRoles()
    {
        return typeof(Roles)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(fi => fi.IsLiteral && !fi.IsInitOnly) // Ensure it's a constant
            .Select(fi => fi.GetValue(null).ToString())
            .ToList();
    }
}