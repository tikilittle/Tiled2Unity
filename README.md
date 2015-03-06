This is a fork from the original project:

https://github.com/Seanba/Tiled2Unity

Intended to addapt the source so it would compile fine on linux

Changes:
=========
1. Removed Shell32 COM reference from Ookii.Dialogs project since COM interops are not available on linux
2. Removed all Shell32 references inside VistaFolderBrowserDialogEvents.cs
3. Added "PresentationCore.dll" and "System.Deployment.dll" since those are not available on mono
4. Made the "Export to" textBox not read only, since the "Choose Export Folder" does not work well on linux
5. Replaced the RichText50W on the about dialog by a default System.Windows.Forms.RichTextBox since "kernel32.dll" and "msftedit.dll" are windows only assemblies
6. Replaced "System.Windows.Media.Media3D.Vector3D" by a handwritten replacement, since "System.Windows.Media.Media3D.Vector3D" does not work well on linux (throws exception while instantiating a new object)



Tiled2Unity
=========
Tiled2Unity is made up of two parts:

1) The Tiled2Unity Utility that exports TMX files into Unity (tool directory)
2) The Unity scripts that import the output of the Tiled2Unity Utility (unity directory)
