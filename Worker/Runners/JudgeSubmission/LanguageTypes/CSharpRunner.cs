using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Worker.Models;

namespace Worker.Runners.JudgeSubmission.LanguageTypes
{
    public class CSharpRunner : Base.Runner
    {
        public CSharpRunner(Contest contest, Problem problem, Submission submission, IServiceProvider provider)
            : base(contest, problem, submission, provider)
        {
            Logger = provider.GetRequiredService<ILogger<CSharpRunner>>();
        }

        protected override async Task<JudgeResult> CompileAsync()
        {
            var file = Path.Combine(Jail, "main.cs");
            var program = Convert.FromBase64String(Submission.Program.Code);
            await using (var stream = new FileStream(file, FileMode.Create, FileAccess.Write))
            {
                await stream.WriteAsync(program);
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "isolate",
                    Arguments = $"--cg -b {BoxId} -s -E PATH=/bin:/usr/bin -d /etc" +
                                " -c jail -i /dev/null" +
                                " -p120 -f 409600 --cg-timing -t 15.0 -x 0 -w 20.0 -k 128000 --cg-mem=512000" +
                                " --run -- /usr/bin/csc -errorlog compiler_output" +
                                LanguageOptions.LanguageOptionsDict[Language.CSharp].CompilerOptions +
                                " -out:main.exe main.cs"
                }
            };
            process.Start();
            await process.WaitForExitAsync();
            if (process.ExitCode == 0)
            {
                return null;
            }
            else if (process.ExitCode != 1)
            {
                throw new Exception($"Isolate error ExitCode={process.ExitCode}.");
            }

            Logger.LogInformation($"Compilation ERROR gcc exited with non-zero code.");
            var compilerOutputFile = Path.Combine(Box, "compiler_output");
            await using var compilerOutputStream = new FileStream(compilerOutputFile, FileMode.Open);
            using var compilerOutputReader = new StreamReader(compilerOutputStream);
            var compilerOutputString = await compilerOutputReader.ReadToEndAsync();
            if (compilerOutputString.Length > 4096)
            {
                compilerOutputString = compilerOutputString.Substring(0, 4096)
                    + "\n\n*** Output trimmed due to excessive length of 4096 characters. ***";
            }

            return new JudgeResult
            {
                Verdict = Verdict.CompilationError,
                Time = null,
                Memory = null,
                FailedOn = 0,
                Score = 0,
                Message = compilerOutputString
            };
        }

        protected override async Task ExecuteProgramAsync(string meta, int bytes)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "isolate",
                    Arguments = $"--cg -b {BoxId} -s -M {meta} -c jail -d /box={Box}:norec -d /box/jail={Jail}:rw" +
                                $" -i jail/input -o jail/output -r jail/stderr -p20 -f {bytes}" +
                                $" --cg-timing -t {TimeLimit} -x 0 -w {TimeLimit + 3.0f} -k 128000 --cg-mem={MemoryLimit}" +
                                " --run -- /usr/bin/csr main.exe"
                }
            };
            process.Start();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0 && process.ExitCode != 1)
            {
                throw new Exception($"Isolate error ExitCode={process.ExitCode}.");
            }
        }
    }
}