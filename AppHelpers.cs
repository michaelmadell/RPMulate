using System;
using System.Reflection;

namespace RPMulate
{
    internal static class AppHelpers
    {
        public static readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;
    }
}