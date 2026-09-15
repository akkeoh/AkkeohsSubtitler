using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using ScriptPortal.Vegas;

public class EntryPoint
{
    public void FromVegas(Vegas vegas)
    {
        try
        {

            string corePath = FindAssembly("AkkeohsVegas.Core.dll");
            string extPath = FindAssembly("AkkeohsVegas.Extension.dll");
            if (corePath == null || extPath == null)
            {
                MessageBox.Show(
                    "AkkeohsVegas assemblies not found. Run AkkeohsSubtitlesSetup.exe first.",
                    "Akkeoh's Subtitler");
                return;
            }

            System.Reflection.Assembly.LoadFrom(corePath);
            var extAsm = System.Reflection.Assembly.LoadFrom(extPath);

            Type moduleType = extAsm.GetType("AkkeohsVegas.Extension.AkkeohsCommandModule");
            object module = Activator.CreateInstance(moduleType);
            moduleType.GetMethod("InitializeModule").Invoke(module, new object[] { vegas });

            Type dockType = extAsm.GetType("AkkeohsVegas.Extension.AkkeohsDockView");
            object dock = Activator.CreateInstance(dockType, new object[] { vegas });
            vegas.LoadDockView((DockableControl)dock);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Akkeoh's Subtitler");
        }
    }

    private static string FindAssembly(string fileName)
    {
        string[] roots =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Vegas Pro", "15.0", "Application Extensions"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Vegas Pro", "Application Extensions"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Vegas Application Extensions"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "AkkeohsSubtitlesForVegas")
        };

        foreach (string root in roots)
        {
            string path = Path.Combine(root, fileName);
            if (File.Exists(path))
                return path;
        }
        return null;
    }
}
