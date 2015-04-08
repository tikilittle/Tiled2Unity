﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Tiled2Unity
{
    static partial class Program
    {
        public delegate void WriteLineDelegate(string line);
        public static event WriteLineDelegate OnWriteLine;

        public delegate void WriteWarningDelegate(string line);
        public static event WriteWarningDelegate OnWriteWarning;

        public delegate void WriteErrorDelegate(string line);
        public static event WriteErrorDelegate OnWriteError;

        public delegate void WriteSuccessDelegate(string line);
        public static event WriteSuccessDelegate OnWriteSuccess;

        public delegate void WriteVerboseDelegate(string line);
        public static event WriteVerboseDelegate OnWriteVerbose;

        static private readonly float DefaultTexelBias = 8192.0f;

        static public bool AutoExport { get; private set; }
        static public float Scale { get; set; }
        static public float TexelBias { get; private set; }
        static public bool Verbose { get; private set; }
        static public bool Help { get; private set; }

        static public string TmxPath { get; private set; }
        static public string ExportUnityProjectDir { get; private set; }

        static public string LogFilePath { get; private set; }

        static private NDesk.Options.OptionSet Options = new NDesk.Options.OptionSet()
            {
                { "a|auto-export", "Automatically export to UNITYDIR and close.", ae => Program.AutoExport = true },
                { "s|scale=", "Scale the output vertices by a value.\nA value of 0.01 is popular for many Unity projects that use 'Pixels Per Unit' of 100 for sprites.\nDefault is 1 (no scaling).", s => Program.Scale = ParseFloatDefault(s, 1.0f) },
                { "t|texel-bias=", "Bias for texel sampling.\nTexels are offset by 1 / value.\nDefault value is 8192.\nA value of 2048 has been useful for shaders that show seams.", t => Program.TexelBias = ParseFloatDefault(t, DefaultTexelBias) },
                { "v|verbose", "Print verbose messages.", v => Program.Verbose = true },
                { "h|help", "Display this help message.", h => Program.Help = true },
            };

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (PrintVersionOnly(args))
            {
                return;
            }

            SetCulture();

            // Default options
            Program.AutoExport = false;
            Program.Scale = -1.0f;
            Program.TexelBias = DefaultTexelBias;
            Program.Verbose = false;
            Program.Help = false;
            Program.TmxPath = "";
            Program.ExportUnityProjectDir = "";

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (Tiled2UnityForm form = new Tiled2UnityForm(args))
            {
                StartLogging(args);
                Application.Run(form);
            }
        }

        public static bool ParseOptions(string[] args)
        {
            // Parse the options
            List<string> extra = Program.Options.Parse(args);

            // If we didn''t overide scale then use the old value
            if (Program.Scale <= 0.0f)
            {
                if (Properties.Settings.Default.LastVertexScale > 0)
                {
                    Program.Scale = Properties.Settings.Default.LastVertexScale;
                }
                else
                {
                    Program.Scale = 1.0f;
                }
            }
            else
            {
                // Save our new value
                Properties.Settings.Default.LastVertexScale = Program.Scale;
                Properties.Settings.Default.Save();
            }

            // First left over option must exist and it is the TMX file we are exporting
            if (extra.Count() == 0)
            {
                Program.WriteLine("Missing TMXPATH argument.");
                Program.WriteLine("  If using the GUI, try opening a TMX file now");
                Program.WriteLine("  If using the command line, provide a path to a TMX file");
                Program.WriteLine("  If using from Tiled Map Editor, try adding %mapfile to the command");
                PrintHelp();
                return false;
            }
            else
            {
                Program.TmxPath = Path.GetFullPath(extra[0]);

                if (!File.Exists(Program.TmxPath))
                {
                    Program.WriteError("TMXPATH file '{0}' does not exist.", Program.TmxPath);
                    PrintHelp();
                    return false;
                }

                extra.RemoveAt(0);
            }

            // The next 'left over' option is the Unity project that we are exporting to
            if (extra.Count() > 0)
            {
                Program.ExportUnityProjectDir = Path.GetFullPath(extra[0]);

                if (!Directory.Exists(Program.ExportUnityProjectDir))
                {
                    Program.WriteError("UNITYDIR Unity Project Directory '{0}' does not exist", Program.ExportUnityProjectDir);
                    PrintHelp();
                    return false;
                }
                if (!Directory.Exists(Path.Combine(Program.ExportUnityProjectDir, "Assets")))
                {
                    Program.WriteError("UNITYDIR '{0}' is not a Unity Project folder", Program.ExportUnityProjectDir);
                    PrintHelp();
                    return false;
                }

                extra.RemoveAt(0);
            }
            else if (Program.AutoExport)
            {
                // If we are auto-exporting then this arugment *must* be present (and it isn't so bail)
                Program.WriteError("Auto-exporting is enabled but UNITYDIR is missing");
                PrintHelp();
                return false;
            }

            // Do we have any other options left over? We shouldn't.
            if (extra.Count() > 0)
            {
                Program.WriteError("Too many arguments. Can't parse '{0}'", extra[0]);
                PrintHelp();
                return false;
            }

            // Did we ask for help?
            if (Program.Help)
                Program.PrintHelp();

            // Success
            return true;
        }

        public static void PrintHelp()
        {
            Program.WriteLine("Tiled2Unity Utility, Version: {0}", GetVersion());
            Program.WriteLine("Usage: Tiled2Unity [OPTIONS]+ TMXPATH [UNITYDIR]");
            Program.WriteLine("Example: Tiled2Unity --verbose -s=0.01 MyTiledMap.tmx ../../MyUnityProjectFolder");
            Program.WriteLine("");
            Program.WriteLine("Options:");

            TextWriter writer = new StringWriter();
            Program.Options.WriteOptionDescriptions(writer);
            Program.WriteLine(writer.ToString());

            Program.WriteLine("Prefab object properties (set in TMX file for each layer/object)");
            Program.WriteLine("  unity:sortingLayerName");
            Program.WriteLine("  unity:sortingOrder");
            Program.WriteLine("  unity:layer");
            Program.WriteLine("  unity:tag");
            Program.WriteLine("  unity:scale");
            Program.WriteLine("  unity:isTrigger");
            Program.WriteLine("  unity:ignore");
            Program.WriteLine("  unity:collisionOnly");
            Program.WriteLine("  (Other properties are exported for custom scripting in your Unity project)");
        }

        public static void WriteLine()
        {
            WriteLine("");
        }

        public static void WriteLine(string line)
        {
            line += "\n";
            if (OnWriteLine != null)
                OnWriteLine(line);
            Console.Write(line);
            Log(line);
        }

        public static void WriteLine(string fmt, params object[] args)
        {
            WriteLine(String.Format(fmt, args));
        }

        public static void WriteWarning(string warning)
        {
            warning += "\n";
            if (OnWriteWarning != null)
                OnWriteWarning(warning);
            Console.Write(warning);
            Log(warning);
        }

        public static void WriteWarning(string fmt, params object[] args)
        {
            WriteWarning(String.Format(fmt, args));
        }

        public static void WriteError(string error)
        {
            error += "\n";
            if (OnWriteError != null)
                OnWriteError(error);
            Console.Write(error);
            Log(error);
        }

        public static void WriteError(string fmt, params object[] args)
        {
            WriteError(String.Format(fmt, args));
        }

        public static void WriteSuccess(string success)
        {
            success += "\n";
            if (OnWriteSuccess != null)
                OnWriteSuccess(success);
            Console.Write(success);
            Log(success);
        }

        public static void WriteSuccess(string fmt, params object[] args)
        {
            WriteSuccess(String.Format(fmt, args));
        }

        public static void WriteVerbose(string line)
        {
            if (!Program.Verbose)
                return;

            line += "\n";
            if (OnWriteVerbose != null)
                OnWriteVerbose(line);
            Console.Write(line);
            Log(line);
        }

        public static void WriteVerbose(string fmt, params object[] args)
        {
            WriteVerbose(String.Format(fmt, args));
        }

        public static string GetExportedFilename(TmxMap tmxMap)
        {
            return String.Format("{0}.tiled2unity.xml", tmxMap.Name);
        }

        public static string GetVersion()
        {
            var thisApp = Assembly.GetExecutingAssembly();
            AssemblyName name = new AssemblyName(thisApp.FullName);
            return name.Version.ToString();
        }

        static private void StartLogging(string[] args)
        {
            // Create the directory if need be
            Program.LogFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tiled2Unity");
            if (!Directory.Exists(Program.LogFilePath))
            {
                Directory.CreateDirectory(Program.LogFilePath);
            }

            // Start off the log empty
            Program.LogFilePath = Path.Combine(Program.LogFilePath, "tiled2unity.log");
            File.WriteAllText(Program.LogFilePath, String.Empty);

            // Write our very first entries into the log
            Program.WriteLine(DateTime.Now.ToString());
            Program.WriteLine("Tiled2Unity {0}", String.Join(" ", args));
            Program.WriteLine("Log path: {0}", Program.LogFilePath);
        }

        static private void Log(string line)
        {
            using (StreamWriter writer = File.AppendText(Program.LogFilePath))
            {
                writer.Write(line);
            }
        }

        static private void SetCulture()
        {
            // Force decimal numbers to use '.' as the decimal separator
            System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";
            System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;
        }

        static private bool PrintVersionOnly(string[] args)
        {
            if (args != null && args.Count() == 1)
            {
                // This is so stupid
                if (args[0] == "--write-version-file")
                {
                    File.WriteAllText("t2u-version.txt", Program.GetVersion());
                    return true;
                }
            }

            return false;
        }

        static private float ParseFloatDefault(string str, float defaultValue)
        {
            float resultValue = 0;
            if (float.TryParse(str, out resultValue))
            {
                return resultValue;
            }
            return defaultValue;
        }

    } // end class
} // end namespace
