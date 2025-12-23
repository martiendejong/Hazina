// See https://aka.ms/new-console-template for more information
using System.Diagnostics;

public static class BuildOutput
{
    public static string buildFile = "runquasar.bat";
    public static string buildOutputFile = "build.log";
    public static string buildErrorsFile = "build_errors.log";

    public static string GetBuildOutput(string workingDirectory, string file, string logfile)
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            WorkingDirectory = workingDirectory,
            FileName = workingDirectory + "\\" + file,
            //Arguments = "build",
            //RedirectStandardOutput = true,
            //RedirectStandardError = true,
            UseShellExecute = true
        };

        using (Process process = new Process { StartInfo = psi })
        {
            process.Start();

            // Read output and errors
            //string output = process.StandardOutput.ReadToEnd();
            //string error = process.StandardError.ReadToEnd();
            //var output = "";
            //var error = "";

            process.WaitForExit();

            var output = File.ReadAllText(workingDirectory + "\\" + logfile);

            return output;
        }
    }
}
