using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Utility.Notifications.Internal
{
    internal static class OptionalRendererLoader
    {
        private const string AssemblyFileName = "Utility.Notifications.Ugui.dll";
        private const string AssemblyName = "Utility.Notifications.Ugui";
        private const string RendererTypeName = "Utility.Notifications.Ugui.UguiToastRenderer";

        internal static IFrameToastRenderer? TryCreateUgui(Transform host, out Exception? error)
        {
            error = null;

            try
            {
                Assembly? assembly = FindLoadedAssembly();
                if (assembly == null)
                {
                    string? directory = Path.GetDirectoryName(typeof(Toast).Assembly.Location);
                    if (string.IsNullOrEmpty(directory))
                        return null;

                    string path = Path.Combine(directory, AssemblyFileName);
                    if (!File.Exists(path))
                        return null;

                    assembly = Assembly.LoadFrom(path);
                }

                Type rendererType =
                    assembly.GetType(RendererTypeName, throwOnError: true)
                    ?? throw new TypeLoadException(RendererTypeName);
                object? instance = Activator.CreateInstance(
                    rendererType,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    args: new object[] { host },
                    culture: null
                );
                return instance as IFrameToastRenderer
                    ?? throw new InvalidCastException(
                        $"{RendererTypeName} does not implement the Utility renderer contract."
                    );
            }
            catch (Exception exception)
            {
                error = exception;
                return null;
            }
        }

        private static Assembly? FindLoadedAssembly()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                if (assemblies[i].GetName().Name == AssemblyName)
                    return assemblies[i];
            }

            return null;
        }
    }
}
