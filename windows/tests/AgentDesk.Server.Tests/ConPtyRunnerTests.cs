using System.Runtime.InteropServices;
using System.Text;
using AgentDesk.Server.Usage;
using Xunit;

namespace AgentDesk.Server.Tests;

public class ConPtyRunnerTests
{
    [Fact]
    public void CreateUnicodeEnvironmentBlock_CreatesValidNullTerminatedUnicodeBlock()
    {
        var env = new Dictionary<string, string>
        {
            { "AGY_CLI_HIDE_ACCOUNT_INFO", "1" },
            { "TEST_VAR", "Hello" }
        };

        IntPtr pEnv = ConPtyAntigravityRunner.CreateUnicodeEnvironmentBlock(env);
        Assert.NotEqual(IntPtr.Zero, pEnv);

        try
        {
            // Read unicode string back from pointer until double null
            var result = new List<string>();
            int offset = 0;
            while (true)
            {
                string? str = Marshal.PtrToStringUni(pEnv + offset);
                if (string.IsNullOrEmpty(str)) break;
                result.Add(str);
                offset += (str.Length + 1) * sizeof(char);
            }

            Assert.Contains("AGY_CLI_HIDE_ACCOUNT_INFO=1", result);
            Assert.Contains("TEST_VAR=Hello", result);
        }
        finally
        {
            Marshal.FreeHGlobal(pEnv);
        }
    }

    [Theory]
    [InlineData("agy.exe", false)]
    [InlineData("agy.cmd", true)]
    [InlineData("C:\\path\\to\\custom.exe", false)]
    [InlineData("C:\\path\\to\\custom.bat", true)]
    public void BuildCommandLine_ResolvesApplicationAndCommandLineSafely(string inputCli, bool expectsCmdExe)
    {
        var (appName, cmdLine) = ConPtyAntigravityRunner.BuildCommandLine(inputCli);

        Assert.NotEmpty(appName);
        Assert.NotEmpty(cmdLine);

        if (expectsCmdExe)
        {
            Assert.EndsWith("cmd.exe", appName, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("/c", cmdLine);
        }
        else
        {
            Assert.DoesNotContain("cmd.exe", appName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
