// See https://aka.ms/new-console-template for more information
using System.Diagnostics;

Console.WriteLine("Hello, World!");




// load the file and convert it to XML using openaai


// 




var psi = new ProcessStartInfo
{
    FileName = "java",
    Arguments = "-jar fop.jar -xml input.xml -xsl stylesheet.xsl -pdf output.pdf",
    RedirectStandardOutput = true,
    UseShellExecute = false
};
Process.Start(psi);

